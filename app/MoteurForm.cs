namespace BATraduction;

public class MoteurForm : Form
{
    readonly Traducteur _trad;
    readonly ReglagesMoteur _r;
    readonly ComboBox _langue = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
    readonly ComboBox _type = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
    readonly ComboBox _fournisseur = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
    readonly ComboBox _modele = new() { Width = 340 };
    readonly TextBox _cle = new() { Width = 340, UseSystemPasswordChar = true };
    readonly TextBox _adresse = new() { Width = 340 };
    readonly Label _lblFournisseur = new() { Text = "Fournisseur", AutoSize = true };
    readonly Label _lblModele = new() { Text = "Modèle", AutoSize = true };
    readonly Label _lblCle = new() { Text = "Clé API", AutoSize = true };
    readonly Label _lblAdresse = new() { Text = "Adresse de l'API", AutoSize = true };
    readonly Label _aide = new() { AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = Theme.TexteDoux };
    readonly Label _resultat = new() { AutoSize = true, MaximumSize = new Size(460, 0) };
    readonly NumericUpDown _delai = new() { Minimum = 300, Maximum = 3000, Increment = 100, Width = 90 };
    readonly CheckBox _vider = new() { Text = "Retraduire les phrases déjà en cache pour cette langue", AutoSize = true };

    static readonly string[] ModelesClaude = { "claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5" };

    TypeMoteur _typeAffiche;
    string _fournisseurAffiche;

    public MoteurForm(Traducteur trad)
    {
        _trad = trad;
        _r = ReglagesMoteur.Charger();

        Text = "Langue et moteur de traduction";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = Theme.Normal;
        BackColor = Theme.Fond;
        ForeColor = Theme.Texte;

        _langue.Items.AddRange(LanguesCibles.Toutes);
        _langue.SelectedItem = LanguesCibles.Trouver(_r.Langue);
        _type.Items.AddRange(new object[]
        {
            "Local (Ollama) — gratuit, hors ligne",
            "Claude (Anthropic) — clé API",
            "Autre IA (ChatGPT, Gemini, Mistral, DeepSeek…) — clé API",
            "DeepL — clé API (offre gratuite disponible)",
        });
        _fournisseur.Items.AddRange(Fournisseurs.Tous);
        _fournisseur.SelectedItem = Fournisseurs.Trouver(_r.Fournisseur);
        _type.SelectedIndex = (int)_r.Type;

        var grille = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(12) };
        void Ligne(Control etiquette, Control champ)
        {
            etiquette.Anchor = AnchorStyles.Left;
            etiquette.Margin = new Padding(0, 6, 12, 6);
            grille.Controls.Add(etiquette);
            grille.Controls.Add(champ);
        }
        void Seul(Control c)
        {
            grille.Controls.Add(new Label());
            grille.Controls.Add(c);
        }
        Ligne(new Label { Text = "Traduire vers", AutoSize = true }, _langue);
        Seul(new Label
        {
            AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = Theme.TexteDoux,
            Text = "Les langues à alphabet non latin ou à nombreux accents (russe, coréen, chinois, vietnamien) peuvent mal s'afficher si la police du jeu ne contient pas leurs caractères.",
        });
        Ligne(new Label { Text = "Moteur", AutoSize = true }, _type);
        Ligne(_lblFournisseur, _fournisseur);
        Ligne(_lblModele, _modele);
        Ligne(_lblCle, _cle);
        Ligne(_lblAdresse, _adresse);
        Seul(_aide);
        Seul(_vider);
        var ligneDelai = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Fond };
        ligneDelai.Controls.Add(_delai);
        ligneDelai.Controls.Add(new Label { Text = "ms d'attente avant de traduire un texte qui change (plus haut = moins de morceaux de phrase)", AutoSize = true, ForeColor = Theme.TexteDoux, Margin = new Padding(6, 4, 0, 0), MaximumSize = new Size(360, 0) });
        Ligne(new Label { Text = "Pause", AutoSize = true }, ligneDelai);
        _delai.Value = Math.Clamp(_r.DelaiMs, 300, 3000);

        var btnTester = Theme.Bouton("Tester");
        var btnOk = Theme.Bouton("Enregistrer", true);
        var btnAnnuler = Theme.Bouton("Annuler");
        btnAnnuler.DialogResult = DialogResult.Cancel;
        var boutons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        boutons.Controls.AddRange(new Control[] { btnTester, btnOk, btnAnnuler });
        Seul(boutons);
        Seul(_resultat);
        grille.BackColor = Theme.Fond;
        foreach (var c in new Control[] { _langue, _type, _fournisseur, _modele, _cle, _adresse, _delai })
        {
            c.BackColor = Theme.Carte;
            c.ForeColor = Theme.Texte;
        }
        foreach (var c in new ComboBox[] { _langue, _type, _fournisseur, _modele }) c.FlatStyle = FlatStyle.Flat;
        foreach (var c in new TextBox[] { _cle, _adresse }) c.BorderStyle = BorderStyle.FixedSingle;
        _vider.ForeColor = Theme.Texte;
        Controls.Add(grille);
        CancelButton = btnAnnuler;

        _type.SelectedIndexChanged += (_, _) => { Memoriser(); Afficher(); };
        _fournisseur.SelectedIndexChanged += (_, _) => { Memoriser(); Afficher(); };
        btnTester.Click += async (_, _) => await Tester();
        btnOk.Click += (_, _) => Enregistrer();
        Afficher();
    }

    TypeMoteur Type => (TypeMoteur)_type.SelectedIndex;
    string FournisseurChoisi => ((Fournisseur)_fournisseur.SelectedItem).Nom;

    void Afficher()
    {
        _typeAffiche = Type;
        _fournisseurAffiche = FournisseurChoisi;
        _modele.Items.Clear();
        _lblFournisseur.Visible = _fournisseur.Visible = Type == TypeMoteur.OpenAI;
        _lblAdresse.Visible = _adresse.Visible = Type == TypeMoteur.OpenAI;
        _lblModele.Visible = _modele.Visible = Type != TypeMoteur.DeepL;
        _lblCle.Visible = _cle.Visible = Type != TypeMoteur.Ollama;
        switch (Type)
        {
            case TypeMoteur.Claude:
                _modele.Items.AddRange(ModelesClaude);
                _modele.Text = _r.ModeleClaude;
                _cle.Text = _r.CleClaude;
                _aide.Text = "Crée une clé sur console.anthropic.com (rubrique API Keys). L'utilisation est facturée par Anthropic " +
                             "selon le modèle choisi ; chaque phrase n'est traduite qu'une fois grâce au cache.";
                break;
            case TypeMoteur.OpenAI:
                var f = Fournisseurs.Trouver(FournisseurChoisi);
                var reglage = _r.ReglageDe(f.Nom);
                _modele.Text = reglage.Modele;
                _cle.Text = reglage.Cle;
                _adresse.Text = reglage.Adresse;
                _aide.Text = f.Nom == Fournisseurs.Personnalise
                    ? "Pour n'importe quel service compatible avec le format OpenAI : indique l'adresse de son API " +
                      "(souvent terminée par /v1), le nom exact du modèle et la clé."
                    : $"Crée une clé sur {(f.SiteCle == "" ? "le logiciel" : f.SiteCle)}, puis indique le nom exact du modèle " +
                      "tel qu'il apparaît chez le fournisseur. L'adresse est préremplie, tu peux la modifier si besoin.";
                break;
            case TypeMoteur.DeepL:
                _cle.Text = _r.CleDeepL;
                _aide.Text = "Crée une clé sur deepl.com (compte « DeepL API », l'offre gratuite suffit pour commencer). " +
                             "DeepL est un traducteur spécialisé : le glossaire ne s'applique pas, mais tes corrections oui.";
                break;
            default:
                _modele.Items.Add("qwen2.5:14b");
                _modele.Text = _r.ModeleOllama;
                _aide.Text = "Traduction sur ta carte graphique via Ollama. Gratuit et hors ligne.";
                break;
        }
        _resultat.Text = "";
    }

    void Memoriser()
    {
        switch (_typeAffiche)
        {
            case TypeMoteur.Claude:
                _r.ModeleClaude = _modele.Text.Trim();
                _r.CleClaude = _cle.Text.Trim();
                break;
            case TypeMoteur.OpenAI:
                var reglage = _r.ReglageDe(_fournisseurAffiche);
                reglage.Modele = _modele.Text.Trim();
                reglage.Cle = _cle.Text.Trim();
                reglage.Adresse = _adresse.Text.Trim();
                break;
            case TypeMoteur.DeepL:
                _r.CleDeepL = _cle.Text.Trim();
                break;
            default:
                _r.ModeleOllama = _modele.Text.Trim();
                break;
        }
    }

    void AppliquerChoix()
    {
        Memoriser();
        _r.Type = Type;
        _r.Fournisseur = FournisseurChoisi;
        _r.Langue = ((LangueCible)_langue.SelectedItem).Code;
        _r.DelaiMs = (int)_delai.Value;
    }

    async Task Tester()
    {
        AppliquerChoix();
        _resultat.ForeColor = Theme.TexteDoux;
        _resultat.Text = "Test en cours…";
        try
        {
            var moteur = Moteurs.Creer(_r);
            string trad = await _trad.Traduire(moteur, "ja", LanguesCibles.Trouver(_r.Langue), "先生、今日もよろしくお願いします！");
            _resultat.ForeColor = Theme.Succes;
            _resultat.Text = "✓ Ça marche : « " + trad + " »";
        }
        catch (Exception e)
        {
            _resultat.ForeColor = Theme.Erreur;
            _resultat.Text = "✗ " + e.Message;
        }
    }

    void Enregistrer()
    {
        AppliquerChoix();
        _r.Sauver();
        if (_vider.Checked) _trad.ViderCache();
        _trad.AppliquerReglages(_r);
        DialogResult = DialogResult.OK;
        Close();
    }
}
