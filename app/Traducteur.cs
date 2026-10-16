using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BATraduction;

public class Traducteur
{
    public const int Port = 5050;
    static readonly string AncienDossier =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BATraduction");
    public static readonly string DossierDonnees = ChoisirDossier();

    static string ChoisirDossier()
    {
        string exe = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        string portable = Path.Combine(exe, "Données");
        try
        {
            Directory.CreateDirectory(portable);
            string essai = Path.Combine(portable, ".ecriture");
            File.WriteAllText(essai, "");
            File.Delete(essai);
        }
        catch (Exception) { return AncienDossier; }
        if (!File.Exists(Path.Combine(portable, "cache.jsonl")) && Directory.Exists(AncienDossier) &&
            !string.Equals(Path.GetFullPath(AncienDossier), Path.GetFullPath(portable), StringComparison.OrdinalIgnoreCase))
            foreach (var f in Directory.GetFiles(AncienDossier))
            {
                string nom = Path.GetFileName(f);
                if (nom.Equals("BATraduction.exe", StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Copy(f, Path.Combine(portable, nom), false); } catch (IOException) { }
            }
        return portable;
    }
    static string CheminCache => Path.Combine(DossierDonnees, "cache.jsonl");
    static string CheminGlossaire => Path.Combine(DossierDonnees, "glossaire.md");
    public static string CheminCorrections(string langue) =>
        Path.Combine(DossierDonnees, langue == "fr" ? "corrections.tsv" : $"corrections.{langue}.tsv");
    const string Ollama = "http://127.0.0.1:11434";

    public ReglagesMoteur Reglages { get; private set; } = ReglagesMoteur.Charger();
    public string ErreurMoteur { get; private set; }
    public event Action<string> Journal;
    public event Action<EntreeTraduction> Traduit;
    public int TailleCache { get { lock (_cache) return _cache.Keys.Count(k => k.Item2 == Reglages.Langue); } }

    readonly Dictionary<(string, string, string), string> _cache = new();
    readonly List<(long seq, string src, string to, string text, string trad)> _changements = new();
    readonly List<(DateTime quand, string src, string to, string text, string trad)> _historique = new();
    long _seq;
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    IMoteur _moteur;
    SemaphoreSlim _file = new(1, 1);
    TcpListener _ecoute;

    static readonly Regex Japonais = new(@"[\u3040-\u30ff\u4e00-\u9fff]");
    static readonly Regex Kana = new(@"[\u3040-\u30ff]");
    static readonly Dictionary<string, string> Sources = new() { ["en"] = "anglais", ["ja"] = "japonais" };

    void Log(string m) => Journal?.Invoke(m);

    public void AppliquerReglages(ReglagesMoteur r)
    {
        Reglages = r;
        try
        {
            _moteur = Moteurs.Creer(r);
            _file = new SemaphoreSlim(_moteur.Paralleles, _moteur.Paralleles);
            ErreurMoteur = null;
        }
        catch (InvalidOperationException e)
        {
            _moteur = null;
            ErreurMoteur = e.Message;
        }
    }

    void Changer(string src, string to, string text, string trad)
    {
        lock (_cache)
        {
            if (trad == null) _cache.Remove((src, to, text));
            else _cache[(src, to, text)] = trad;
            _changements.Add((++_seq, src, to, text, trad ?? ""));
            if (_changements.Count > 2000) _changements.RemoveRange(0, 1000);
            File.WriteAllLines(CheminCache, _cache.Select(kv => Json(kv.Key, kv.Value)), new UTF8Encoding(false));
        }
    }

    public void Oublier(EntreeTraduction e)
    {
        Changer(e.Source, e.Langue, e.Original, null);
        Log($"Traduction oubliée : elle sera refaite au prochain affichage ({Court(e.Original)})");
    }

    public void Corriger(EntreeTraduction e, string correction)
    {
        string chemin = CheminCorrections(e.Langue);
        string ligne = e.Original.Replace("\r", "").Replace("\n", "\\n") + "\t" + correction.Replace("\r", "").Replace("\n", "\\n");
        File.AppendAllText(chemin, (File.Exists(chemin) ? "" : "# Corrections manuelles\r\n") + ligne + "\r\n", new UTF8Encoding(false));
        string propre = Nettoyer(e.Original, correction);
        foreach (var src in new[] { "en", "ja" }) Changer(src, e.Langue, e.Original, propre);
        Log($"Correction enregistrée : {Court(e.Original)} → {Court(propre)}");
    }

    public async Task<string> Retraduire(EntreeTraduction e)
    {
        var moteur = _moteur ?? throw new InvalidOperationException(ErreurMoteur ?? "Aucun moteur de traduction.");
        var cible = LanguesCibles.Trouver(e.Langue);
        string t = await Traduire(moteur, e.Source, cible, e.Original, Contexte(e.Source, cible.Code, e.Original));
        Changer(e.Source, e.Langue, e.Original, t);
        Traduit?.Invoke(e with { Traduction = t });
        return t;
    }

    List<(string text, string trad)> Contexte(string src, string to, string text)
    {
        if (text.Length < 12) return new();
        lock (_historique)
            return _historique
                .Where(h => h.src == src && h.to == to && h.text != text && h.text.Length >= 12 && DateTime.Now - h.quand < TimeSpan.FromSeconds(90))
                .TakeLast(3).Select(h => (h.text, h.trad)).ToList();
    }

    void Memoriser(string src, string to, string text, string trad)
    {
        lock (_historique)
        {
            _historique.Add((DateTime.Now, src, to, text, trad));
            if (_historique.Count > 20) _historique.RemoveAt(0);
        }
    }

    public void ViderCache()
    {
        lock (_cache)
        {
            string langue = Reglages.Langue;
            if (File.Exists(CheminCache)) File.Copy(CheminCache, CheminCache + ".bak", true);
            foreach (var cle in _cache.Keys.Where(k => k.Item2 == langue).ToList()) _cache.Remove(cle);
            File.WriteAllLines(CheminCache, _cache.Select(kv => Json(kv.Key, kv.Value)), new UTF8Encoding(false));
            AjouterCorrections();
        }
    }

    public void Demarrer()
    {
        Directory.CreateDirectory(DossierDonnees);
        if (!File.Exists(CheminGlossaire))
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("glossaire.md")!;
            using var f = File.Create(CheminGlossaire);
            s.CopyTo(f);
        }
        if (!File.Exists(CheminCorrections("fr")))
            File.WriteAllText(CheminCorrections("fr"),
                "# Corrections manuelles, prioritaires sur les traductions automatiques.\r\n" +
                "# Une ligne par correction : texte original<TAB>traduction voulue   (\\n = retour à la ligne)\r\n" +
                "# Pour une autre langue : corrections.<code>.tsv (ex. corrections.es.tsv).\r\n" +
                "# Après modification : redémarrer BA Traduction, puis le jeu.\r\n", new UTF8Encoding(false));
        ChargerCache();
        AppliquerReglages(Reglages);
        _ecoute = new TcpListener(IPAddress.Loopback, Port);
        _ecoute.Start();
        _ = Task.Run(BoucleEcoute);
    }

    void ChargerCache()
    {
        bool modifie = false;
        if (File.Exists(CheminCache))
            foreach (var ligne in File.ReadLines(CheminCache))
            {
                try
                {
                    using var d = JsonDocument.Parse(ligne);
                    var r = d.RootElement;
                    string src = r.GetProperty("from").GetString();
                    string to = r.TryGetProperty("to", out var t) ? t.GetString() : "fr";
                    string text = r.GetProperty("text").GetString();
                    string trad = r.GetProperty("fr").GetString();
                    string propre = Nettoyer(text, trad);
                    modifie |= propre != trad;
                    _cache[(src, to, text)] = propre;
                }
                catch (Exception) { }
            }
        if (modifie)
            File.WriteAllLines(CheminCache, _cache.Select(kv => Json(kv.Key, kv.Value)), new UTF8Encoding(false));
        AjouterCorrections();
    }

    void AjouterCorrections()
    {
        foreach (var langue in LanguesCibles.Toutes.Select(l => l.Code))
        {
            string chemin = CheminCorrections(langue);
            if (!File.Exists(chemin)) continue;
            foreach (var ligne in File.ReadLines(chemin))
            {
                if (ligne.StartsWith("#") || !ligne.Contains('\t')) continue;
                var p = ligne.Split('\t', 2);
                foreach (var src in new[] { "en", "ja" })
                    _cache[(src, langue, p[0].Replace("\\n", "\n"))] = p[1].Replace("\\n", "\n");
            }
        }
    }

    static string Json((string src, string to, string text) cle, string trad) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["from"] = cle.src, ["to"] = cle.to, ["text"] = cle.text, ["fr"] = trad },
                                 new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    void Sauver((string, string, string) cle, string trad)
    {
        lock (_cache)
        {
            _cache[cle] = trad;
            File.AppendAllText(CheminCache, Json(cle, trad) + "\n", new UTF8Encoding(false));
        }
    }

    public static string Nettoyer(string text, string fr)
    {
        string nl = new[] { "\n\r", "\r\n", "\n", "\r" }.FirstOrDefault(text.Contains);
        if (!text.Contains('\\'))
        {
            fr = Regex.Replace(fr, @"(\\[nr])+", nl != null ? "\n" : " ").Replace("\\", "");
            fr = Regex.Replace(fr, " {2,}", " ");
        }
        var lignes = Regex.Split(fr, "\n\r|\r\n|\n|\r").Select(l => l.Trim());
        fr = nl != null ? string.Join(nl, lignes) : string.Join(" ", lignes.Where(l => l.Length > 0));
        return text == text.Trim() ? fr.Trim() : fr;
    }

    string Consigne(string src, LangueCible cible)
    {
        string glossaire = File.Exists(CheminGlossaire) ? File.ReadAllText(CheminGlossaire) : "";
        string langue = Sources.GetValueOrDefault(src, "la langue d'origine");
        string c = cible.NomConsigne;
        return
            $"Tu traduis l'interface et les dialogues du jeu Blue Archive du {langue} vers le {c}.\n" +
            "Contexte : le joueur est « Sensei », un adulte qui encadre des lycéennes dans la ville de Kivotos. " +
            "Les dialogues sont vivants et familiers entre élèves ; les élèves s'adressent poliment à Sensei.\n" +
            "Règles :\n" +
            $"- Réponds UNIQUEMENT par la traduction en {c} : pas de guillemets ajoutés, pas d'explication, pas de note.\n" +
            $"- Traduis tout : la réponse ne doit contenir aucun texte en {langue}.\n" +
            "- Garde la même mise en forme : mêmes retours à la ligne, mêmes balises comme [FF0000], [-], <color=...>, {0}.\n" +
            "- N'écris jamais de barre oblique inverse ni de séquence d'échappement.\n" +
            "- Les textes d'interface (boutons, onglets) doivent rester très courts.\n" +
            "- Les noms des élèves et des lieux gardent leur graphie officielle anglaise.\n" +
            $"- Ne translittère jamais une phrase en romaji : traduis toujours le sens en {c}.\n" +
            (cible.Code == "fr"
                ? "Glossaire à respecter :\n" + glossaire
                : "- Garde tels quels ces noms propres : Sensei, Kivotos, Schale, MomoTalk, Abydos, Gehenna, Trinity, " +
                  "Millennium, Hyakkiyako, Red Winter, Shanhaijing, Valkyrie, SRT, Arius, ainsi que les noms des élèves.\n" +
                  $"- Traduis les termes d'interface (boutique, missions, etc.) en {c} de façon cohérente d'une phrase à l'autre.\n");
    }

    static readonly Regex Prefixe = new(@"^\s*(traduction|translation|texte à traduire|réponse)\s*[:：]\s*", RegexOptions.IgnoreCase);

    static string Retoucher(string text, string trad)
    {
        trad = Prefixe.Replace(trad.Trim(), "");
        string guillemets = "\"«»“”「」『』";
        if (trad.Length > 2 && guillemets.Contains(trad[0]) && guillemets.Contains(trad[^1]) &&
            !(text.TrimStart().Length > 0 && guillemets.Contains(text.TrimStart()[0])))
            trad = trad[1..^1].Trim();
        return trad;
    }

    string Probleme(string src, LangueCible cible, string text, string trad)
    {
        if (string.IsNullOrWhiteSpace(trad)) return "La réponse est vide.";
        bool resteJaponais = cible.Code.StartsWith("zh") ? Kana.IsMatch(trad) : Japonais.IsMatch(trad);
        if (src == "ja" && resteJaponais)
            return $"Ta traduction contient encore du japonais. Traduis le sens de tout le texte en {cible.NomConsigne} (pas de romaji).";
        int max = (src == "ja" ? 6 : 3) * text.Length + 40;
        if (trad.Length > max)
            return "Ta réponse est beaucoup trop longue : donne seulement la traduction de ce texte, sans explication, sans note et sans le contexte.";
        if (text.Length >= 8 && trad.Length * 6 < text.Length)
            return "Ta réponse est beaucoup trop courte : traduis le texte en entier.";
        if (Regex.IsMatch(trad, @"(contexte|répliques précédentes|note\s*:|remarque\s*:)", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(text, @"(context|note|remark)", RegexOptions.IgnoreCase))
            return "N'ajoute ni note, ni remarque, ni le contexte : réponds uniquement par la traduction du texte.";
        return null;
    }

    public async Task<string> Traduire(IMoteur moteur, string src, LangueCible cible, string text,
                                       List<(string text, string trad)> contexte = null)
    {
        contexte ??= new();
        if (moteur is ITraductionDirecte direct)
            return Nettoyer(text, await direct.TraduireDirect(src, cible.Code, text, string.Join("\n", contexte.Select(c => c.text))));

        string demande = contexte.Count == 0
            ? text
            : "Répliques précédentes, pour le contexte uniquement (ne pas les traduire) :\n" +
              string.Join("\n", contexte.Select(c => $"- {c.text} → {c.trad}")) +
              "\n\nTexte à traduire (réponds uniquement par sa traduction) :\n" + text;
        string consigne = Consigne(src, cible);
        var echanges = new List<Echange> { new("user", demande) };
        string trad = Retoucher(text, await moteur.Envoyer(consigne, echanges));
        string probleme = Probleme(src, cible, text, trad);
        if (probleme != null)
        {
            echanges.Add(new("assistant", trad));
            echanges.Add(new("user", probleme + " Réponds uniquement par la traduction."));
            trad = Retoucher(text, await moteur.Envoyer(consigne, echanges));
            probleme = Probleme(src, cible, text, trad);
            if (probleme != null) throw new InvalidOperationException("Traduction rejetée par le contrôle qualité : " + probleme);
        }
        return Nettoyer(text, trad);
    }

    public async Task<string> Obtenir(string src, string text)
    {
        var cible = LanguesCibles.Trouver(Reglages.Langue);
        if (string.IsNullOrWhiteSpace(text) || src == cible.Code) return text;
        if (src == "ja" && !Japonais.IsMatch(text)) return text;
        var cle = (src, cible.Code, text);
        lock (_cache) if (_cache.TryGetValue(cle, out var connu)) return connu;
        var moteur = _moteur ?? throw new InvalidOperationException(ErreurMoteur ?? "Aucun moteur de traduction.");
        var file = _file;
        await file.WaitAsync();
        try
        {
            lock (_cache) if (_cache.TryGetValue(cle, out var deja)) return deja;
            string t = await Traduire(moteur, src, cible, text, Contexte(src, cible.Code, text));
            Sauver(cle, t);
            Memoriser(src, cible.Code, text, t);
            Traduit?.Invoke(new EntreeTraduction(src, cible.Code, text, t));
            return t;
        }
        finally { file.Release(); }
    }

    static string Court(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length > 40 ? s[..40] + "…" : s;
    }

    public async Task<(bool ollama, bool modele)> VerifierOllama()
    {
        try
        {
            using var d = JsonDocument.Parse(await _http.GetStringAsync($"{Ollama}/api/tags"));
            bool present = d.RootElement.GetProperty("models").EnumerateArray()
                .Any(m => m.GetProperty("name").GetString() == Reglages.ModeleOllama);
            return (true, present);
        }
        catch { return (false, false); }
    }

    async Task BoucleEcoute()
    {
        while (true)
        {
            TcpClient client;
            try { client = await _ecoute.AcceptTcpClientAsync(); }
            catch (ObjectDisposedException) { return; }
            _ = Task.Run(() => Servir(client));
        }
    }

    async Task Servir(TcpClient client)
    {
        using (client)
        {
            try
            {
                var flux = client.GetStream();
                var lecteur = new StreamReader(flux, Encoding.ASCII, false, 8192, leaveOpen: true);
                string requete = await lecteur.ReadLineAsync();
                while (!string.IsNullOrEmpty(await lecteur.ReadLineAsync())) { }
                var morceaux = requete?.Split(' ');
                if (morceaux == null || morceaux.Length < 2 || morceaux[0] != "GET") { await Repondre(flux, 400, ""); return; }

                string cible = morceaux[1];
                int iq = cible.IndexOf('?');
                string chemin = iq < 0 ? cible : cible[..iq];
                var q = Parametres(iq < 0 ? "" : cible[(iq + 1)..]);
                string src = q.GetValueOrDefault("from", "en");
                switch (chemin)
                {
                    case "/translate":
                        try { await Repondre(flux, 200, await Obtenir(src, q.GetValueOrDefault("text", ""))); }
                        catch (Exception e) { Log("Erreur de traduction : " + e.Message); await Repondre(flux, 500, "erreur"); }
                        break;
                    case "/changes":
                        long depuis = long.TryParse(q.GetValueOrDefault("since", "0"), out var n) ? n : 0;
                        var chg = new StringBuilder();
                        lock (_cache)
                        {
                            chg.Append("#seq\t").Append(_seq).Append('\n');
                            if (depuis > 0)
                                foreach (var c in _changements.Where(c => c.seq > depuis && c.src == src && c.to == Reglages.Langue))
                                    chg.Append(Echappe(c.text)).Append('\t').Append(Echappe(c.trad)).Append('\n');
                        }
                        await Repondre(flux, 200, chg.ToString());
                        break;
                    case "/cache":
                        string langue = Reglages.Langue;
                        var sb = new StringBuilder();
                        lock (_cache)
                            foreach (var kv in _cache.Where(kv => kv.Key.Item1 == src && kv.Key.Item2 == langue))
                                sb.Append(Echappe(kv.Key.Item3)).Append('\t').Append(Echappe(kv.Value)).Append('\n');
                        await Repondre(flux, 200, sb.ToString());
                        break;
                    default:
                        await Repondre(flux, 404, "introuvable");
                        break;
                }
            }
            catch (Exception) { }
        }
    }

    static Dictionary<string, string> Parametres(string query)
    {
        var d = new Dictionary<string, string>();
        foreach (var p in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = p.Split('=', 2);
            d[Uri.UnescapeDataString(kv[0])] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
        }
        return d;
    }

    static string Echappe(string s) =>
        s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

    static async Task Repondre(NetworkStream flux, int code, string corps)
    {
        byte[] data = Encoding.UTF8.GetBytes(corps);
        string entete = $"HTTP/1.1 {code} {(code == 200 ? "OK" : "Error")}\r\n" +
                        "Content-Type: text/plain; charset=utf-8\r\n" +
                        $"Content-Length: {data.Length}\r\nConnection: close\r\n\r\n";
        await flux.WriteAsync(Encoding.ASCII.GetBytes(entete));
        await flux.WriteAsync(data);
    }

    public void Arreter() => _ecoute?.Stop();
}
