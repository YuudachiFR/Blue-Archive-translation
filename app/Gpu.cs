using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BATraduction;

public record MesureGpu(string Nom, float Utilisation, float MemoireUtiliseeMo, float MemoireTotaleMo, float Temperature);

public record ModeleCharge(string Nom, long OctetsVram, DateTimeOffset? Expiration, long OctetsTotal = 0);

public enum NiveauDiagnostic { Inconnu, Ideal, Correct, Limite, Insuffisant }

public record Diagnostic(NiveauDiagnostic Niveau, string Titre, string Detail);

public static class DiagnosticIa
{
    const double Go = 1073741824.0;

    public static Diagnostic Evaluer(MesureGpu gpu, long tailleModele, ModeleCharge charge, bool local, string nomModele)
    {
        if (gpu == null || gpu.MemoireTotaleMo <= 0)
            return new(NiveauDiagnostic.Inconnu, "Évaluation impossible",
                "Carte graphique non NVIDIA ou non détectée. Si les traductions sont lentes, passe sur une IA en ligne (Langue et moteur…).");

        double totale = gpu.MemoireTotaleMo / 1024.0;
        double besoin = tailleModele > 0 ? tailleModele / Go * 1.1 + 0.6 : 9.5;
        double occupeeParModele = charge != null ? charge.OctetsVram / Go : 0;
        double libre = totale - gpu.MemoireUtiliseeMo / 1024.0 + occupeeParModele;
        string modele = $"{nomModele} (≈ {besoin:0.0} Go)";

        if (!local)
            return totale >= besoin
                ? new(NiveauDiagnostic.Correct, "IA en ligne active — l'IA locale serait possible",
                      $"Ta carte ({totale:0.0} Go) peut faire tourner {modele} gratuitement : Langue et moteur… → Local (Ollama).")
                : new(NiveauDiagnostic.Correct, "IA en ligne active — bon choix pour cette carte",
                      $"Ta carte ({totale:0.0} Go) est trop juste pour {modele} : garde une IA en ligne.");

        if (totale < besoin * 0.6)
            return new(NiveauDiagnostic.Insuffisant, "Puissance insuffisante pour l'IA locale",
                $"{totale:0.0} Go de VRAM pour un modèle qui en demande {besoin:0.0}. Conseillé : une IA en ligne (DeepL gratuit, Gemini, Claude…) dans Langue et moteur….");

        if (totale < besoin)
            return new(NiveauDiagnostic.Limite, "Juste pour ce modèle : traductions lentes",
                $"Le modèle ne tient pas entièrement dans {totale:0.0} Go et débordera sur la mémoire du PC. Conseillé : une IA en ligne, ou un modèle plus léger (ex. qwen2.5:7b).");

        if (charge != null && charge.OctetsTotal > 0 && charge.OctetsVram < charge.OctetsTotal * 0.95)
            return new(NiveauDiagnostic.Limite, "Le modèle tourne en partie sur le processeur",
                $"Seulement {charge.OctetsVram / Go:0.0} Go sur {charge.OctetsTotal / Go:0.0} Go sont en VRAM : la mémoire vidéo est trop occupée. Ferme d'autres applications ou passe sur une IA en ligne.");

        if (libre < besoin)
            return new(NiveauDiagnostic.Limite, "Mémoire vidéo très occupée en ce moment",
                $"{Math.Max(0, libre):0.0} Go libres pour un modèle de {besoin:0.0} Go : risque de lenteurs avec le jeu ouvert. Ferme d'autres applications, ou passe sur une IA en ligne.");

        if (gpu.Temperature >= 85)
            return new(NiveauDiagnostic.Limite, "Carte graphique très chaude",
                $"{gpu.Temperature:0} °C : l'IA locale ajoute de la charge. Vérifie la ventilation, ou passe sur une IA en ligne le temps que ça refroidisse.");

        return totale >= besoin * 1.5
            ? new(NiveauDiagnostic.Ideal, "Idéal pour l'IA locale",
                  $"{totale:0.0} Go de VRAM, {Math.Max(0, libre):0.0} Go disponibles pour {modele} : traduction locale rapide et gratuite.")
            : new(NiveauDiagnostic.Correct, "Adapté à l'IA locale",
                  $"{totale:0.0} Go de VRAM pour {modele} : ça fonctionne, mais un jeu gourmand ouvert en même temps peut ralentir les traductions.");
    }
}

public sealed class SurveillanceGpu : IDisposable
{
    Process _smi;
    public event Action<MesureGpu> Mesure;
    public bool Disponible { get; private set; }

    public void Demarrer()
    {
        string chemin = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
        if (!File.Exists(chemin)) return;
        try
        {
            _smi = new Process
            {
                StartInfo = new ProcessStartInfo(chemin,
                    "--query-gpu=name,utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits -lms 1000")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true,
            };
            _smi.OutputDataReceived += (_, e) => Lire(e.Data);
            _smi.Start();
            _smi.BeginOutputReadLine();
            Disponible = true;
        }
        catch (Exception) { Disponible = false; }
    }

    void Lire(string ligne)
    {
        if (string.IsNullOrWhiteSpace(ligne)) return;
        var p = ligne.Split(',').Select(s => s.Trim()).ToArray();
        if (p.Length < 5) return;
        static float N(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        Mesure?.Invoke(new MesureGpu(p[0], N(p[1]), N(p[2]), N(p[3]), N(p[4])));
    }

    public void Dispose()
    {
        try { if (_smi is { HasExited: false }) _smi.Kill(); } catch (Exception) { }
        _smi?.Dispose();
    }
}

public static class OllamaVram
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    const string Adresse = "http://127.0.0.1:11434";

    public static async Task<List<ModeleCharge>> ModelesCharges()
    {
        var liste = new List<ModeleCharge>();
        try
        {
            using var d = JsonDocument.Parse(await Http.GetStringAsync($"{Adresse}/api/ps"));
            foreach (var m in d.RootElement.GetProperty("models").EnumerateArray())
            {
                DateTimeOffset? fin = m.TryGetProperty("expires_at", out var e) && DateTimeOffset.TryParse(e.GetString(), out var t) ? t : null;
                long vram = m.TryGetProperty("size_vram", out var v) ? v.GetInt64() : 0;
                long total = m.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                liste.Add(new ModeleCharge(m.GetProperty("name").GetString(), vram, fin, total));
            }
        }
        catch (Exception) { }
        return liste;
    }

    public static async Task<long> TailleModele(string nom)
    {
        try
        {
            using var d = JsonDocument.Parse(await Http.GetStringAsync($"{Adresse}/api/tags"));
            foreach (var m in d.RootElement.GetProperty("models").EnumerateArray())
                if (m.GetProperty("name").GetString() == nom) return m.GetProperty("size").GetInt64();
        }
        catch (Exception) { }
        return 0;
    }

    public static async Task Precharger(string modele, string conservation)
    {
        try
        {
            var corps = JsonSerializer.Serialize(new { model = modele, keep_alive = conservation });
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            await http.PostAsync($"{Adresse}/api/generate", new StringContent(corps, Encoding.UTF8, "application/json"));
        }
        catch (Exception) { }
    }

    public static async Task<int> Liberer()
    {
        int n = 0;
        foreach (var m in await ModelesCharges())
        {
            var corps = JsonSerializer.Serialize(new { model = m.Nom, keep_alive = 0 });
            var rep = await Http.PostAsync($"{Adresse}/api/generate", new StringContent(corps, Encoding.UTF8, "application/json"));
            if (rep.IsSuccessStatusCode) n++;
        }
        return n;
    }
}
