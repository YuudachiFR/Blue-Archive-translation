namespace BATraduction;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var unique = new Mutex(true, "BATraduction-instance-unique", out bool premier);
        if (!premier)
        {
            MessageBox.Show("BA Traduction est déjà ouvert (regarde près de l'horloge, dans la zone de notification).",
                            "BA Traduction", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ApplicationConfiguration.Initialize();
        var trad = new Traducteur();
        try { trad.Demarrer(); }
        catch (System.Net.Sockets.SocketException)
        {
            MessageBox.Show($"Le port {Traducteur.Port} est déjà utilisé : ferme l'ancien serveur (lancer_serveur.bat) puis relance BA Traduction.",
                            "BA Traduction", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Application.Run(new MainForm(trad));
    }
}
