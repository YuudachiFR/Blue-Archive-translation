using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace BATraduction;

public enum EtatJeu { Introuvable, NonInstalle, Active, Desactive, AutreDll }

public class Jeu
{
    public string Nom { get; }
    public string Langue { get; }
    public string Dossier { get; set; }

    const string Dll = "XInput1_3.dll";
    const string DllOff = "XInput1_3.dll.off";
    const string Ini = "BAtrad.ini";

    public Jeu(string nom, string langue, string dossier)
    {
        Nom = nom;
        Langue = langue;
        Dossier = dossier;
    }

    string Chemin(string f) => Path.Combine(Dossier ?? "", f);

    public static bool EstUnDossierDeJeu(string dossier) =>
        !string.IsNullOrEmpty(dossier) && File.Exists(Path.Combine(dossier, "BlueArchive.exe"));

    static bool EstNotreDll(string chemin)
    {
        try { return Encoding.ASCII.GetString(File.ReadAllBytes(chemin)).Contains("BAtrad"); }
        catch { return false; }
    }

    public EtatJeu Etat
    {
        get
        {
            if (!EstUnDossierDeJeu(Dossier)) return EtatJeu.Introuvable;
            if (File.Exists(Chemin(Dll))) return EstNotreDll(Chemin(Dll)) ? EtatJeu.Active : EtatJeu.AutreDll;
            if (File.Exists(Chemin(DllOff))) return EtatJeu.Desactive;
            return EtatJeu.NonInstalle;
        }
    }

    public bool MiseAJourDisponible
    {
        get
        {
            string f = File.Exists(Chemin(Dll)) ? Chemin(Dll) : Chemin(DllOff);
            if (!File.Exists(f) || !EstNotreDll(f)) return false;
            return !File.ReadAllBytes(f).AsSpan().SequenceEqual(DllEmbarquee());
        }
    }

    public bool EnCours => Process.GetProcessesByName("BlueArchive").Any(p =>
    {
        try { return p.MainModule?.FileName?.StartsWith(Dossier, StringComparison.OrdinalIgnoreCase) == true; }
        catch { return true; }
    });

    static byte[] DllEmbarquee()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(Dll)!;
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }

    void VerifierFerme()
    {
        if (EnCours) throw new InvalidOperationException($"Ferme d'abord {Nom}.");
    }

    public void Installer()
    {
        VerifierFerme();
        if (Etat == EtatJeu.AutreDll)
            File.Move(Chemin(Dll), Chemin("XInput1_3.dll.autre.bak"), true);
        if (File.Exists(Chemin(DllOff))) File.Delete(Chemin(DllOff));
        File.WriteAllBytes(Chemin(Dll), DllEmbarquee());
        EcrireReglages();
    }

    public static int DelaiMs { get; set; } = 800;

    public void EcrireReglages()
    {
        if (!EstUnDossierDeJeu(Dossier) || Etat is EtatJeu.NonInstalle or EtatJeu.Introuvable) return;
        File.WriteAllText(Chemin(Ini), $"[BAtrad]\r\nfrom={Langue}\r\nport={Traducteur.Port}\r\ndebounce_ms={DelaiMs}\r\n",
                          new UTF8Encoding(false));
    }

    public void Activer()
    {
        VerifierFerme();
        File.Move(Chemin(DllOff), Chemin(Dll), true);
    }

    public void Desactiver()
    {
        VerifierFerme();
        File.Move(Chemin(Dll), Chemin(DllOff), true);
    }

    public void Desinstaller()
    {
        VerifierFerme();
        foreach (var f in new[] { Dll, DllOff, Ini })
            if (File.Exists(Chemin(f))) File.Delete(Chemin(f));
    }

    public static string TrouverJP()
    {
        foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            string p = Path.Combine(d.RootDirectory.FullName, "YostarGames", "BlueArchive_JP");
            if (EstUnDossierDeJeu(p)) return p;
        }
        return null;
    }

    public static string TrouverGlobal()
    {
        var bibliotheques = new List<string>();
        string steam = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (steam != null)
        {
            steam = steam.Replace('/', '\\');
            bibliotheques.Add(steam);
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (var ligne in File.ReadAllLines(vdf))
                {
                    var parties = ligne.Trim().Split('"', StringSplitOptions.RemoveEmptyEntries);
                    if (parties.Length >= 3 && parties[0] == "path") bibliotheques.Add(parties[2].Trim().Replace(@"\\", @"\"));
                }
        }
        foreach (var b in bibliotheques)
        {
            string p = Path.Combine(b, "steamapps", "common", "BlueArchive");
            if (EstUnDossierDeJeu(p)) return p;
        }
        return null;
    }
}
