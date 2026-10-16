using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BATraduction;

public class MainForm : Form
{
    const int Largeur = 620;
    const int Interieur = Largeur - 32;

    readonly Traducteur _trad;
    readonly Jeu _jp, _global;
    readonly SurveillanceGpu _gpu = new();

    readonly Pastille _pastilleTrad = new();
    readonly Label _etatTrad = Theme.Etiquette("", Theme.Gras);
    readonly Label _detailTrad = Theme.Etiquette("", Theme.Normal, Theme.TexteDoux);
    readonly Label _alerte = Theme.Etiquette("", Theme.Gras, Theme.Alerte);
    readonly Button _btnModele = Theme.Bouton("Télécharger le modèle", true);
    readonly Button _btnOllama = Theme.Bouton("Installer Ollama", true);

    readonly Label _gpuNom = Theme.Etiquette("", Theme.Normal, Theme.TexteDoux);
    readonly Graphique _graphique = new() { Width = Interieur };
    readonly Label _gpuUtil = Theme.Etiquette("—", Theme.Grand);
    readonly Label _gpuMem = Theme.Etiquette("—", Theme.Grand);
    readonly Label _gpuTemp = Theme.Etiquette("—", Theme.Grand);
    readonly Barre _barreMem = new() { Width = 200 };
    readonly Label _modeleCharge = Theme.Etiquette("", Theme.Normal, Theme.TexteDoux);
    readonly Pastille _pastilleDiag = new();
    readonly Label _diagTitre = Theme.Etiquette("Analyse de la carte graphique…", Theme.Gras);
    readonly Label _diagDetail = Theme.Etiquette("", Theme.Normal, Theme.TexteDoux);
    MesureGpu _derniere;
    readonly Button _btnLiberer = Theme.Bouton("Libérer la VRAM");

    readonly ListBox _journal = new()
    {
        Width = Interieur, Height = 150, BorderStyle = BorderStyle.None, IntegralHeight = false, HorizontalScrollbar = true,
        BackColor = Theme.Carte, ForeColor = Theme.TexteDoux, Font = new Font("Consolas", 9f),
    };
    readonly NotifyIcon _tray = new();
    readonly System.Windows.Forms.Timer _minuteur = new() { Interval = 2000 };
    readonly List<Action> _rafraichir = new();
    static string CheminReglages => Path.Combine(Traducteur.DossierDonnees, "chemins.ini");
    static string OllamaApp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama app.exe");

    void DemarrerOllama()
    {
        try
        {
            Process.Start(new ProcessStartInfo(OllamaApp) { UseShellExecute = true });
            Ecrire("Démarrage d'Ollama…");
        }
        catch (Exception ex) { Ecrire("Impossible de démarrer Ollama : " + ex.Message); }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valeur, int taille);

    public MainForm(Traducteur trad)
    {
        _trad = trad;
        var reglages = LireReglages();
        _jp = new Jeu("Blue Archive JP (Yostar)", "ja", reglages.GetValueOrDefault("JP") ?? Jeu.TrouverJP());
        _global = new Jeu("Blue Archive Global (Steam)", "en", reglages.GetValueOrDefault("Global") ?? Jeu.TrouverGlobal());

        Text = "BA Traduction";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        BackColor = Theme.Fond;
        ForeColor = Theme.Texte;
        Font = Theme.Normal;
        ClientSize = new Size(Largeur + 56, 860);
        MinimumSize = new Size(Largeur + 72, 500);

        var page = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = true, Padding = new Padding(20, 18, 20, 18), BackColor = Theme.Fond,
        };
        Controls.Add(page);

        page.Controls.Add(Entete());
        page.Controls.Add(_alerte);
        page.Controls.Add(CarteTraducteur());
        page.Controls.Add(CarteGpu());
        page.Controls.Add(CarteJeu(_jp));
        page.Controls.Add(CarteJeu(_global));
        page.Controls.Add(CarteJournal());
        page.Controls.Add(Theme.Etiquette("Laisse cette fenêtre ouverte (ou réduite près de l'horloge) pendant que tu joues : c'est elle qui traduit.",
                                          Theme.Normal, Theme.TexteDoux));

        _tray.Icon = Icon;
        _tray.Text = "BA Traduction";
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => Restaurer();
        _tray.ContextMenuStrip = new ContextMenuStrip();
        _tray.ContextMenuStrip.Items.Add("Ouvrir", null, (_, _) => Restaurer());
        _tray.ContextMenuStrip.Items.Add("Libérer la VRAM", null, async (_, _) => await LibererVram());
        _tray.ContextMenuStrip.Items.Add("Quitter", null, (_, _) => Close());
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };

        _trad.Journal += m => BeginInvoke(() => Ecrire(m));
        _trad.Traduit += e => BeginInvoke(() => Ajouter(new LigneJournal(DateTime.Now, null, e)));
        Jeu.DelaiMs = _trad.Reglages.DelaiMs;
        PreparerMenuJournal();
        _gpu.Mesure += m => { if (IsHandleCreated) BeginInvoke(() => AfficherGpu(m)); };
        _minuteur.Tick += async (_, _) => await Rafraichir();
        _minuteur.Start();
        Shown += async (_, _) =>
        {
            _gpu.Demarrer();
            if (!_gpu.Disponible)
            {
                _gpuNom.Text = "Surveillance en temps réel disponible avec une carte NVIDIA.";
                _graphique.Visible = false;
            }
            if (_trad.Reglages.Type == TypeMoteur.Ollama && File.Exists(OllamaApp) && !(await _trad.VerifierOllama()).ollama)
            {
                DemarrerOllama();
                await Task.Delay(3000);
            }
            await Rafraichir();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int sombre = 1;
        DwmSetWindowAttribute(Handle, 20, ref sombre, sizeof(int));
    }

    static FlowLayoutPanel Colonne(int largeur = Interieur) => new()
    {
        FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
        BackColor = Theme.Carte, Margin = Padding.Empty, MinimumSize = new Size(largeur, 0), MaximumSize = new Size(largeur, 0),
    };

    static FlowLayoutPanel Rangee(int haut = 8) => new()
    {
        FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true,
        BackColor = Theme.Carte, Margin = new Padding(0, haut, 0, 0), MaximumSize = new Size(Interieur, 0),
    };

    static CarteUI Carte(params Control[] contenu)
    {
        var carte = new CarteUI { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(Largeur, 0) };
        var col = Colonne();
        col.Location = new Point(carte.Padding.Left, carte.Padding.Top);
        col.Controls.AddRange(contenu);
        carte.Controls.Add(col);
        return carte;
    }

    Control Entete()
    {
        var col = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 12) };
        col.Controls.Add(Theme.Etiquette("BA Traduction", Theme.Titre));
        col.Controls.Add(Theme.Etiquette("Blue Archive traduit en direct, dans ta langue", Theme.SousTitre, Theme.TexteDoux));
        var outils = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 12, 0, 0) };
        var btnMoteur = Theme.Bouton("Langue et moteur…", true);
        btnMoteur.Click += async (_, _) =>
        {
            using var f = new MoteurForm(_trad);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                Jeu.DelaiMs = _trad.Reglages.DelaiMs;
                foreach (var j in new[] { _jp, _global })
                    try { j.EcrireReglages(); } catch (IOException) { }
            }
            if (f.DialogResult == DialogResult.OK)
                Ecrire($"Traduction vers {LanguesCibles.Trouver(_trad.Reglages.Langue).Nom} avec {_trad.Reglages.Description}. Relance le jeu pour appliquer.");
            await Rafraichir();
        };
        var btnDonnees = Theme.Bouton("Dossier des données");
        btnDonnees.Click += (_, _) => Process.Start("explorer.exe", Traducteur.DossierDonnees);
        _btnModele.Visible = _btnOllama.Visible = false;
        _btnModele.Click += async (_, _) => await TelechargerModele();
        _btnOllama.Click += async (_, _) =>
        {
            if (File.Exists(OllamaApp)) DemarrerOllama();
            else Process.Start(new ProcessStartInfo("https://ollama.com/download") { UseShellExecute = true });
            await Task.Delay(3000);
            await Rafraichir();
        };
        outils.Controls.AddRange(new Control[] { btnMoteur, btnDonnees, _btnModele, _btnOllama });
        col.Controls.Add(outils);
        _alerte.MaximumSize = new Size(Largeur, 0);
        _alerte.Margin = new Padding(0, 0, 0, 10);
        return col;
    }

    Control CarteTraducteur()
    {
        var titre = Rangee(0);
        titre.Controls.Add(_pastilleTrad);
        titre.Controls.Add(Theme.Etiquette("Traducteur", Theme.TitreCarte));
        _etatTrad.MaximumSize = _detailTrad.MaximumSize = new Size(Interieur, 0);
        _etatTrad.Margin = new Padding(0, 8, 0, 0);
        _detailTrad.Margin = new Padding(0, 4, 0, 0);
        return Carte(titre, _etatTrad, _detailTrad);
    }

    Control CarteGpu()
    {
        var titre = Rangee(0);
        titre.Controls.Add(Theme.Etiquette("Carte graphique", Theme.TitreCarte));
        _gpuNom.Margin = new Padding(8, 3, 0, 0);
        titre.Controls.Add(_gpuNom);
        _graphique.Margin = new Padding(0, 10, 0, 0);

        Control Stat(string nom, Label valeur, Control extra = null)
        {
            var c = Colonne(Interieur / 3 - 4);
            c.Margin = new Padding(0, 0, 4, 0);
            c.Controls.Add(Theme.Etiquette(nom, Theme.Normal, Theme.TexteDoux));
            c.Controls.Add(valeur);
            if (extra != null) { extra.Margin = new Padding(2, 4, 0, 0); c.Controls.Add(extra); }
            return c;
        }
        var stats = Rangee(10);
        stats.Controls.Add(Stat("Utilisation", _gpuUtil));
        stats.Controls.Add(Stat("Mémoire vidéo", _gpuMem, _barreMem));
        stats.Controls.Add(Stat("Température", _gpuTemp));

        var diag = Rangee(14);
        _diagTitre.MaximumSize = new Size(Interieur - 24, 0);
        diag.Controls.Add(_pastilleDiag);
        diag.Controls.Add(_diagTitre);
        _diagDetail.MaximumSize = new Size(Interieur, 0);
        _diagDetail.Margin = new Padding(0, 2, 0, 0);

        var vram = Rangee(12);
        _modeleCharge.MaximumSize = new Size(Interieur - 150, 0);
        _modeleCharge.Margin = new Padding(0, 6, 12, 0);
        _btnLiberer.Click += async (_, _) => await LibererVram();
        vram.Controls.Add(_modeleCharge);
        vram.Controls.Add(_btnLiberer);
        return Carte(titre, _graphique, stats, diag, _diagDetail, vram);
    }

    void AfficherGpu(MesureGpu m)
    {
        _derniere = m;
        _gpuNom.Text = m.Nom;
        _graphique.Ajouter(m.Utilisation);
        _gpuUtil.Text = $"{m.Utilisation:0} %";
        _gpuMem.Text = $"{m.MemoireUtiliseeMo / 1024:0.0} / {m.MemoireTotaleMo / 1024:0.0} Go";
        _barreMem.Valeur = m.MemoireTotaleMo > 0 ? m.MemoireUtiliseeMo / m.MemoireTotaleMo : 0;
        _gpuTemp.Text = $"{m.Temperature:0} °C";
        _gpuTemp.ForeColor = m.Temperature >= 85 ? Theme.Erreur : m.Temperature >= 75 ? Theme.Alerte : Theme.Texte;
    }

    async Task LibererVram()
    {
        _btnLiberer.Enabled = false;
        int n = await OllamaVram.Liberer();
        Ecrire(n > 0 ? "VRAM libérée : modèle déchargé (il se rechargera tout seul à la prochaine traduction)."
                     : "Aucun modèle d'IA n'était chargé en VRAM.");
        await Rafraichir();
    }

    Control CarteJeu(Jeu jeu)
    {
        var chemin = Theme.Etiquette("", Theme.Normal, Theme.TexteDoux);
        chemin.AutoSize = false;
        chemin.Size = new Size(Interieur, 20);
        chemin.AutoEllipsis = true;
        chemin.Margin = new Padding(0, 4, 0, 0);
        var pastille = new Pastille();
        var etat = Theme.Etiquette("", Theme.Gras);
        etat.MaximumSize = new Size(Interieur - 20, 0);
        var ligneEtat = Rangee(6);
        ligneEtat.Controls.Add(pastille);
        ligneEtat.Controls.Add(etat);

        var btnPrincipal = Theme.Bouton("Installer", true);
        var btnDesinstaller = Theme.Bouton("Désinstaller");
        var btnDossier = Theme.Bouton("Changer de dossier…");
        var boutons = Rangee(12);
        boutons.Controls.AddRange(new Control[] { btnPrincipal, btnDesinstaller, btnDossier });

        btnPrincipal.Click += (_, _) => Agir(jeu, () =>
        {
            switch (jeu.Etat)
            {
                case EtatJeu.Active when jeu.MiseAJourDisponible: jeu.Installer(); return "mis à jour";
                case EtatJeu.Active: jeu.Desactiver(); return "désactivé";
                case EtatJeu.Desactive: jeu.Activer(); return "activé";
                default: jeu.Installer(); return "installé et activé";
            }
        });
        btnDesinstaller.Click += (_, _) =>
        {
            if (MessageBox.Show($"Retirer complètement la traduction de {jeu.Nom} ?", "BA Traduction",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Agir(jeu, () => { jeu.Desinstaller(); return "désinstallé"; });
        };
        btnDossier.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = $"Dossier de {jeu.Nom} (celui qui contient BlueArchive.exe)" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (!Jeu.EstUnDossierDeJeu(dlg.SelectedPath))
            {
                MessageBox.Show("BlueArchive.exe est introuvable dans ce dossier.", "BA Traduction", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            jeu.Dossier = dlg.SelectedPath;
            SauverReglages();
            _ = Rafraichir();
        };

        _rafraichir.Add(() =>
        {
            chemin.Text = jeu.Dossier ?? "Dossier du jeu introuvable : utilise « Changer de dossier… »";
            var e = jeu.Etat;
            bool maj = e == EtatJeu.Active && jeu.MiseAJourDisponible;
            (etat.Text, pastille.Couleur) = e switch
            {
                EtatJeu.Introuvable => ("Jeu introuvable", Theme.TexteDoux),
                EtatJeu.NonInstalle => ("Traduction non installée", Theme.TexteDoux),
                EtatJeu.Active when maj => ("Traduction activée — mise à jour disponible", Theme.Alerte),
                EtatJeu.Active => ("Traduction activée", Theme.Succes),
                EtatJeu.Desactive => ("Traduction désactivée", Theme.Erreur),
                _ => ("Une autre XInput1_3.dll est présente (LagoFast ?) — « Installer » la mettra de côté", Theme.Alerte),
            };
            btnPrincipal.Text = e switch
            {
                EtatJeu.Active when maj => "Mettre à jour",
                EtatJeu.Active => "Désactiver",
                EtatJeu.Desactive => "Activer",
                _ => "Installer",
            };
            btnPrincipal.Enabled = e != EtatJeu.Introuvable;
            btnDesinstaller.Enabled = e is EtatJeu.Active or EtatJeu.Desactive;
        });
        return Carte(Theme.Etiquette(jeu.Nom, Theme.TitreCarte), chemin, ligneEtat, boutons);
    }

    Control CarteJournal()
    {
        _journal.Margin = new Padding(0, 8, 0, 0);
        return Carte(Theme.Etiquette("Journal", Theme.TitreCarte), _journal);
    }

    void Restaurer()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    record LigneJournal(DateTime Quand, string Texte, EntreeTraduction Entree)
    {
        public override string ToString() => $"{Quand:HH:mm:ss}  {(Entree != null ? Entree.ToString() : Texte)}";
    }

    void Ecrire(string m) => Ajouter(new LigneJournal(DateTime.Now, m, null));

    void Ajouter(LigneJournal l)
    {
        _journal.Items.Add(l);
        if (_journal.Items.Count > 500) _journal.Items.RemoveAt(0);
        _journal.TopIndex = _journal.Items.Count - 1;
    }

    void PreparerMenuJournal()
    {
        var menu = new ContextMenuStrip { BackColor = Theme.Carte, ForeColor = Theme.Texte, ShowImageMargin = false };
        var retraduire = menu.Items.Add("Retraduire");
        var corriger = menu.Items.Add("Corriger…");
        var oublier = menu.Items.Add("Oublier (refaire au prochain affichage)");
        EntreeTraduction Choisie() => (_journal.SelectedItem as LigneJournal)?.Entree;
        _journal.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            int i = _journal.IndexFromPoint(e.Location);
            if (i >= 0) _journal.SelectedIndex = i;
        };
        menu.Opening += (_, e) =>
        {
            bool ok = Choisie() != null;
            retraduire.Enabled = corriger.Enabled = oublier.Enabled = ok;
            if (!ok) e.Cancel = true;
        };
        retraduire.Click += async (_, _) =>
        {
            var entree = Choisie();
            try { await _trad.Retraduire(entree); }
            catch (Exception ex) { Ecrire("Retraduction impossible : " + ex.Message); }
        };
        corriger.Click += (_, _) =>
        {
            var entree = Choisie();
            using var f = new CorrectionForm(entree);
            if (f.ShowDialog(this) == DialogResult.OK) _trad.Corriger(entree, f.Correction);
        };
        oublier.Click += (_, _) => _trad.Oublier(Choisie());
        _journal.ContextMenuStrip = menu;
    }

    bool _enJeuAvant;

    async Task SuivreLaPartie()
    {
        bool enJeu = (_jp.Etat == EtatJeu.Active && _jp.EnCours) || (_global.Etat == EtatJeu.Active && _global.EnCours);
        if (enJeu == _enJeuAvant) return;
        _enJeuAvant = enJeu;
        if (_trad.Reglages.Type != TypeMoteur.Ollama) return;
        if (enJeu)
        {
            MoteurOllama.Conservation = "60m";
            Ecrire("Jeu détecté : le modèle d'IA reste chargé pendant la partie.");
            await OllamaVram.Precharger(_trad.Reglages.ModeleOllama, "60m");
        }
        else
        {
            MoteurOllama.Conservation = "10m";
            if (await OllamaVram.Liberer() > 0) Ecrire("Jeu fermé : VRAM libérée automatiquement.");
        }
    }

    void Agir(Jeu jeu, Func<string> action)
    {
        try
        {
            Ecrire($"{jeu.Nom} : traduction {action()}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BA Traduction", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        _ = Rafraichir();
    }

    bool _rafraichissement;
    async Task Rafraichir()
    {
        if (_rafraichissement) return;
        _rafraichissement = true;
        try
        {
            foreach (var r in _rafraichir) r();
            var reglages = _trad.Reglages;
            bool local = reglages.Type == TypeMoteur.Ollama;
            var (ollama, modele) = local ? await _trad.VerifierOllama() : (true, true);
            if (_trad.ErreurMoteur != null)
                (_etatTrad.Text, _pastilleTrad.Couleur) = (_trad.ErreurMoteur, Theme.Erreur);
            else if (!ollama)
                (_etatTrad.Text, _pastilleTrad.Couleur) = (File.Exists(OllamaApp)
                    ? "Ollama est installé mais n'est pas lancé : clique sur « Démarrer Ollama »."
                    : "Ollama n'est pas installé : clique sur « Installer Ollama ».", Theme.Erreur);
            else if (!modele)
                (_etatTrad.Text, _pastilleTrad.Couleur) = ($"Le modèle {reglages.ModeleOllama} n'est pas encore téléchargé.", Theme.Alerte);
            else
                (_etatTrad.Text, _pastilleTrad.Couleur) = ("Actif — prêt à traduire", Theme.Succes);
            _detailTrad.Text = $"Langue : {LanguesCibles.Trouver(reglages.Langue).Nom}   ·   Moteur : {reglages.Description}   ·   {_trad.TailleCache} phrases en cache";
            _btnModele.Visible = local && ollama && !modele;
            _btnOllama.Visible = local && !ollama;
            _btnOllama.Text = File.Exists(OllamaApp) ? "Démarrer Ollama" : "Installer Ollama";

            var charges = await OllamaVram.ModelesCharges();
            if (charges.Count == 0)
                _modeleCharge.Text = local ? "Aucun modèle d'IA en VRAM : il se chargera à la prochaine traduction."
                                           : "Aucun modèle d'IA local en VRAM.";
            else
            {
                var m = charges[0];
                string reste = m.Expiration is { } fin && fin > DateTimeOffset.Now
                    ? $" · libéré automatiquement dans {Math.Max(1, (int)Math.Ceiling((fin - DateTimeOffset.Now).TotalMinutes))} min"
                    : "";
                _modeleCharge.Text = $"{m.Nom} occupe {m.OctetsVram / 1073741824.0:0.0} Go de VRAM{reste}";
            }
            _btnLiberer.Enabled = charges.Count > 0;

            long taille = await OllamaVram.TailleModele(reglages.ModeleOllama);
            var charge = charges.FirstOrDefault(c => c.Nom == reglages.ModeleOllama) ?? charges.FirstOrDefault();
            var d = DiagnosticIa.Evaluer(_gpu.Disponible ? _derniere : null, taille, charge, local, reglages.ModeleOllama);
            _diagTitre.Text = d.Titre;
            _diagDetail.Text = d.Detail;
            _pastilleDiag.Couleur = d.Niveau switch
            {
                NiveauDiagnostic.Ideal => Theme.Succes,
                NiveauDiagnostic.Correct => Theme.Accent,
                NiveauDiagnostic.Limite => Theme.Alerte,
                NiveauDiagnostic.Insuffisant => Theme.Erreur,
                _ => Theme.TexteDoux,
            };

            await SuivreLaPartie();

            bool lagofast = Process.GetProcessesByName("LagoFast").Length > 0;
            _alerte.Text = lagofast ? "⚠ LagoFast est ouvert : ferme-le avant de lancer le jeu, sinon il remplacera la traduction par la sienne." : "";
            _alerte.Visible = lagofast;
        }
        finally { _rafraichissement = false; }
    }

    async Task TelechargerModele()
    {
        string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe");
        if (!File.Exists(exe)) exe = "ollama";
        _btnModele.Enabled = false;
        Ecrire($"Téléchargement du modèle {_trad.Reglages.ModeleOllama} (environ 9 Go)…");
        try
        {
            var p = Process.Start(new ProcessStartInfo(exe, $"pull {_trad.Reglages.ModeleOllama}") { UseShellExecute = false, CreateNoWindow = true });
            await p!.WaitForExitAsync();
            Ecrire(p.ExitCode == 0 ? "Modèle téléchargé." : $"Échec du téléchargement (code {p.ExitCode}).");
        }
        catch (Exception ex) { Ecrire("Impossible de lancer Ollama : " + ex.Message); }
        _btnModele.Enabled = true;
        await Rafraichir();
    }

    static Dictionary<string, string> LireReglages()
    {
        var d = new Dictionary<string, string>();
        if (File.Exists(CheminReglages))
            foreach (var l in File.ReadAllLines(CheminReglages))
            {
                var p = l.Split('=', 2);
                if (p.Length == 2 && Jeu.EstUnDossierDeJeu(p[1])) d[p[0]] = p[1];
            }
        return d;
    }

    void SauverReglages()
    {
        File.WriteAllText(CheminReglages, $"JP={_jp.Dossier}\r\nGlobal={_global.Dossier}\r\n", new UTF8Encoding(false));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        bool enJeu = (_jp.Etat == EtatJeu.Active && _jp.EnCours) || (_global.Etat == EtatJeu.Active && _global.EnCours);
        if (enJeu && e.CloseReason == CloseReason.UserClosing &&
            MessageBox.Show("Un jeu traduit est ouvert : fermer BA Traduction arrêtera les nouvelles traductions. Quitter quand même ?",
                            "BA Traduction", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        _tray.Visible = false;
        _gpu.Dispose();
        _trad.Arreter();
        base.OnFormClosing(e);
    }
}
