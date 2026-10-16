namespace BATraduction;

public record LangueCible(string Code, string Nom, string NomConsigne)
{
    public override string ToString() => Nom;
}

public static class LanguesCibles
{
    public static readonly LangueCible[] Toutes =
    {
        new("fr", "Français", "français"),
        new("en", "English", "anglais"),
        new("es", "Español", "espagnol"),
        new("de", "Deutsch", "allemand"),
        new("it", "Italiano", "italien"),
        new("pt", "Português (Brasil)", "portugais du Brésil"),
        new("pl", "Polski", "polonais"),
        new("ru", "Русский", "russe"),
        new("tr", "Türkçe", "turc"),
        new("id", "Bahasa Indonesia", "indonésien"),
        new("vi", "Tiếng Việt", "vietnamien"),
        new("ko", "한국어", "coréen"),
        new("zh", "简体中文", "chinois simplifié"),
        new("zh-TW", "繁體中文", "chinois traditionnel"),
    };

    public static LangueCible Trouver(string code) =>
        Toutes.FirstOrDefault(l => l.Code == code) ?? Toutes[0];
}
