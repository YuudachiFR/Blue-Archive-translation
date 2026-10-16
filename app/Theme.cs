using System.Drawing.Drawing2D;

namespace BATraduction;

public static class Theme
{
    public static readonly Color Fond = Color.FromArgb(22, 24, 29);
    public static readonly Color Carte = Color.FromArgb(32, 35, 43);
    public static readonly Color Bordure = Color.FromArgb(46, 50, 60);
    public static readonly Color Texte = Color.FromArgb(232, 234, 240);
    public static readonly Color TexteDoux = Color.FromArgb(154, 163, 178);
    public static readonly Color Accent = Color.FromArgb(45, 156, 255);
    public static readonly Color Succes = Color.FromArgb(61, 220, 151);
    public static readonly Color Alerte = Color.FromArgb(255, 181, 71);
    public static readonly Color Erreur = Color.FromArgb(255, 92, 108);
    public static readonly Color FondBouton = Color.FromArgb(48, 52, 63);

    public static readonly Font Titre = new("Segoe UI Semibold", 18f);
    public static readonly Font SousTitre = new("Segoe UI", 9.5f);
    public static readonly Font TitreCarte = new("Segoe UI Semibold", 11f);
    public static readonly Font Normal = new("Segoe UI", 9.5f);
    public static readonly Font Gras = new("Segoe UI Semibold", 9.5f);
    public static readonly Font Grand = new("Segoe UI Semibold", 16f);

    public static Button Bouton(string texte, bool principal = false)
    {
        var b = new Button
        {
            Text = texte,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = principal ? Accent : FondBouton,
            ForeColor = Color.White,
            Font = Gras,
            Cursor = Cursors.Hand,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(b.BackColor, 0.15f);
        b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(b.BackColor, 0.05f);
        b.EnabledChanged += (_, _) => b.ForeColor = b.Enabled ? Color.White : TexteDoux;
        return b;
    }

    public static Label Etiquette(string texte = "", Font police = null, Color? couleur = null) => new()
    {
        Text = texte,
        AutoSize = true,
        Font = police ?? Normal,
        ForeColor = couleur ?? Texte,
        BackColor = Color.Transparent,
    };

    public static GraphicsPath Arrondi(Rectangle r, int rayon)
    {
        var p = new GraphicsPath();
        int d = rayon * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

public class CarteUI : Panel
{
    public CarteUI()
    {
        DoubleBuffered = true;
        BackColor = Theme.Fond;
        Padding = new Padding(16, 14, 16, 14);
        Margin = new Padding(0, 0, 0, 12);
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var chemin = Theme.Arrondi(r, 10);
        using var fond = new SolidBrush(Theme.Carte);
        using var bord = new Pen(Theme.Bordure);
        e.Graphics.FillPath(fond, chemin);
        e.Graphics.DrawPath(bord, chemin);
    }
}

public class Pastille : Control
{
    Color _couleur = Theme.TexteDoux;
    public Color Couleur { get => _couleur; set { _couleur = value; Invalidate(); } }

    public Pastille()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Size = new Size(12, 12);
        Margin = new Padding(0, 6, 6, 0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var b = new SolidBrush(_couleur);
        e.Graphics.FillEllipse(b, 1, 1, Width - 3, Height - 3);
    }
}

public class Barre : Control
{
    float _valeur;
    public float Valeur { get => _valeur; set { _valeur = Math.Clamp(value, 0, 1); Invalidate(); } }
    public Color Couleur { get; set; } = Theme.Accent;

    public Barre()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Theme.Carte;
        Height = 10;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        var piste = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var p = Theme.Arrondi(piste, Height / 2 - 1))
        using (var b = new SolidBrush(Theme.Bordure))
            e.Graphics.FillPath(b, p);
        int largeur = (int)((Width - 1) * _valeur);
        if (largeur < Height) return;
        using (var p = Theme.Arrondi(new Rectangle(0, 0, largeur, Height - 1), Height / 2 - 1))
        using (var b = new SolidBrush(_valeur > 0.9f ? Theme.Erreur : _valeur > 0.75f ? Theme.Alerte : Couleur))
            e.Graphics.FillPath(b, p);
    }
}

public class Graphique : Control
{
    readonly Queue<float> _valeurs = new();
    const int Points = 60;

    public Graphique()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Carte;
        Height = 70;
    }

    public void Ajouter(float pourcentage)
    {
        _valeurs.Enqueue(Math.Clamp(pourcentage, 0, 100));
        while (_valeurs.Count > Points) _valeurs.Dequeue();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        using (var grille = new Pen(Theme.Bordure) { DashStyle = DashStyle.Dot })
            for (int i = 1; i < 4; i++)
                g.DrawLine(grille, 0, Height * i / 4f, Width, Height * i / 4f);
        if (_valeurs.Count < 2) return;
        var v = _valeurs.ToArray();
        float pas = (Width - 1f) / (Points - 1);
        float x0 = Width - 1 - pas * (v.Length - 1);
        var pts = v.Select((val, i) => new PointF(x0 + i * pas, Height - 2 - (Height - 4) * val / 100f)).ToArray();
        using (var zone = new GraphicsPath())
        {
            zone.AddLines(pts);
            zone.AddLine(pts[^1], new PointF(pts[^1].X, Height));
            zone.AddLine(new PointF(pts[^1].X, Height), new PointF(pts[0].X, Height));
            zone.CloseFigure();
            using var remplissage = new LinearGradientBrush(new Rectangle(0, 0, Width, Height),
                Color.FromArgb(110, Theme.Accent), Color.FromArgb(10, Theme.Accent), LinearGradientMode.Vertical);
            g.FillPath(remplissage, zone);
        }
        using var ligne = new Pen(Theme.Accent, 2f);
        g.DrawLines(ligne, pts);
    }
}
