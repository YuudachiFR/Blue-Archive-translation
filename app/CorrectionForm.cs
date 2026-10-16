namespace BATraduction;

public class CorrectionForm : Form
{
    readonly TextBox _traduction;
    public string Correction => _traduction.Text.Trim();

    public CorrectionForm(EntreeTraduction e)
    {
        Text = "Corriger une traduction";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Fond;
        ForeColor = Theme.Texte;
        Font = Theme.Normal;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        TextBox Zone(string texte, bool lecture) => new()
        {
            Text = texte.ReplaceLineEndings("\r\n"),
            Multiline = true,
            ReadOnly = lecture,
            ScrollBars = ScrollBars.Vertical,
            Size = new Size(460, 70),
            BackColor = lecture ? Theme.Fond : Theme.Carte,
            ForeColor = lecture ? Theme.TexteDoux : Theme.Texte,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 4, 0, 10),
        };
        _traduction = Zone(e.Traduction, false);

        var col = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(14), WrapContents = false };
        col.Controls.Add(Theme.Etiquette("Texte du jeu", Theme.Gras, Theme.TexteDoux));
        col.Controls.Add(Zone(e.Original, true));
        col.Controls.Add(Theme.Etiquette("Traduction", Theme.Gras));
        col.Controls.Add(_traduction);
        col.Controls.Add(Theme.Etiquette("La correction est enregistrée dans tes corrections et s'applique en jeu en quelques secondes.",
                                         Theme.Normal, Theme.TexteDoux));

        var boutons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
        var ok = Theme.Bouton("Enregistrer", true);
        var annuler = Theme.Bouton("Annuler");
        ok.Click += (_, _) =>
        {
            if (Correction.Length == 0) return;
            DialogResult = DialogResult.OK;
            Close();
        };
        annuler.DialogResult = DialogResult.Cancel;
        boutons.Controls.AddRange(new Control[] { ok, annuler });
        col.Controls.Add(boutons);
        Controls.Add(col);
        AcceptButton = ok;
        CancelButton = annuler;
    }
}
