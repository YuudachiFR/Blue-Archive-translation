using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using AnthropicBeta = Anthropic.Models.Beta.AnthropicBeta;

namespace BATraduction;

public enum TypeMoteur { Ollama, Claude, OpenAI, DeepL }

public record Echange(string Role, string Contenu);

public record EntreeTraduction(string Source, string Langue, string Original, string Traduction)
{
    public override string ToString() => Original.ReplaceLineEndings(" ") + "  →  " + Traduction.ReplaceLineEndings(" ");
}

public record Fournisseur(string Nom, string Adresse, string SiteCle)
{
    public override string ToString() => Nom;
}

public static class Fournisseurs
{
    public const string Personnalise = "Autre (adresse personnalisée)";

    public static readonly Fournisseur[] Tous =
    {
        new("OpenAI (ChatGPT)", "https://api.openai.com/v1", "platform.openai.com"),
        new("Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", "aistudio.google.com"),
        new("Mistral", "https://api.mistral.ai/v1", "console.mistral.ai"),
        new("DeepSeek", "https://api.deepseek.com/v1", "platform.deepseek.com"),
        new("xAI (Grok)", "https://api.x.ai/v1", "console.x.ai"),
        new("Groq", "https://api.groq.com/openai/v1", "console.groq.com"),
        new("OpenRouter (des centaines de modèles)", "https://openrouter.ai/api/v1", "openrouter.ai"),
        new("Together AI", "https://api.together.xyz/v1", "api.together.ai"),
        new("Qwen (Alibaba Cloud)", "https://dashscope-intl.aliyuncs.com/compatible-mode/v1", "alibabacloud.com"),
        new("Moonshot (Kimi)", "https://api.moonshot.ai/v1", "platform.moonshot.ai"),
        new("LM Studio (local)", "http://localhost:1234/v1", ""),
        new(Personnalise, "", ""),
    };

    public static Fournisseur Trouver(string nom) => Tous.FirstOrDefault(f => f.Nom == nom) ?? Tous[0];
}

public class ReglageFournisseur
{
    public string Adresse { get; set; } = "";
    public string Modele { get; set; } = "";
    public string CleChiffree { get; set; } = "";
    [JsonIgnore] public string Cle { get => Securite.Dechiffrer(CleChiffree); set => CleChiffree = Securite.Chiffrer(value); }
}

public static class Securite
{
    public static string Chiffrer(string s) => string.IsNullOrEmpty(s) ? "" :
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser));

    public static string Dechiffrer(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser)); }
        catch (Exception e) when (e is CryptographicException or FormatException) { return ""; }
    }
}

public class ReglagesMoteur
{
    public TypeMoteur Type { get; set; } = TypeMoteur.Ollama;
    public string Langue { get; set; } = "fr";
    public int DelaiMs { get; set; } = 800;
    public string ModeleOllama { get; set; } = "qwen2.5:14b";
    public string ModeleClaude { get; set; } = "claude-opus-5-5";
    public string CleClaudeChiffree { get; set; } = "";
    public string Fournisseur { get; set; } = Fournisseurs.Tous[0].Nom;
    public Dictionary<string, ReglageFournisseur> ReglagesFournisseurs { get; set; } = new();
    public string CleDeepLChiffree { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string ModeleOpenAI { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string AdresseOpenAI { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string CleOpenAIChiffree { get; set; }

    static string Chemin => Path.Combine(Traducteur.DossierDonnees, "moteur.json");

    [JsonIgnore] public string CleClaude { get => Securite.Dechiffrer(CleClaudeChiffree); set => CleClaudeChiffree = Securite.Chiffrer(value); }
    [JsonIgnore] public string CleDeepL { get => Securite.Dechiffrer(CleDeepLChiffree); set => CleDeepLChiffree = Securite.Chiffrer(value); }

    public ReglageFournisseur ReglageDe(string nom)
    {
        if (!ReglagesFournisseurs.TryGetValue(nom, out var r))
            ReglagesFournisseurs[nom] = r = new ReglageFournisseur { Adresse = Fournisseurs.Trouver(nom).Adresse };
        return r;
    }

    public static ReglagesMoteur Charger()
    {
        ReglagesMoteur r = null;
        try
        {
            if (File.Exists(Chemin)) r = JsonSerializer.Deserialize<ReglagesMoteur>(File.ReadAllText(Chemin));
        }
        catch (JsonException) { }
        r ??= new();
        if (!string.IsNullOrEmpty(r.ModeleOpenAI) || !string.IsNullOrEmpty(r.CleOpenAIChiffree))
        {
            var ancien = r.ReglageDe(Fournisseurs.Personnalise);
            ancien.Adresse = r.AdresseOpenAI ?? "";
            ancien.Modele = r.ModeleOpenAI ?? "";
            ancien.CleChiffree = r.CleOpenAIChiffree ?? "";
            if (r.Type == TypeMoteur.OpenAI) r.Fournisseur = Fournisseurs.Personnalise;
        }
        r.ModeleOpenAI = r.AdresseOpenAI = r.CleOpenAIChiffree = null;
        return r;
    }

    public void Sauver()
    {
        Directory.CreateDirectory(Traducteur.DossierDonnees);
        File.WriteAllText(Chemin, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    [JsonIgnore]
    public string Description => Type switch
    {
        TypeMoteur.Claude => $"Claude ({ModeleClaude})",
        TypeMoteur.OpenAI => $"{Fournisseur} ({ReglageDe(Fournisseur).Modele})",
        TypeMoteur.DeepL => "DeepL",
        _ => $"Local, Ollama ({ModeleOllama})",
    };
}

public interface IMoteur
{
    int Paralleles { get; }
    Task<string> Envoyer(string consigne, IReadOnlyList<Echange> echanges);
}

public interface ITraductionDirecte
{
    Task<string> TraduireDirect(string source, string cible, string texte, string contexte);
}

public static class Moteurs
{
    public static IMoteur Creer(ReglagesMoteur r)
    {
        switch (r.Type)
        {
            case TypeMoteur.Claude:
                return new MoteurClaude(r.CleClaude, r.ModeleClaude);
            case TypeMoteur.OpenAI:
                var f = r.ReglageDe(r.Fournisseur);
                return new MoteurOpenAI(f.Adresse, f.Cle, f.Modele);
            case TypeMoteur.DeepL:
                return new MoteurDeepL(r.CleDeepL);
            default:
                return new MoteurOllama(r.ModeleOllama);
        }
    }
}

public class MoteurOllama : IMoteur
{
    readonly string _modele;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };
    public static string Conservation { get; set; } = "10m";
    public MoteurOllama(string modele) => _modele = modele;
    public int Paralleles => 1;

    public async Task<string> Envoyer(string consigne, IReadOnlyList<Echange> echanges)
    {
        var messages = new List<object> { new { role = "system", content = consigne } };
        messages.AddRange(echanges.Select(e => (object)new { role = e.Role, content = e.Contenu }));
        var corps = JsonSerializer.Serialize(new { model = _modele, stream = false, keep_alive = MoteurOllama.Conservation, options = new { temperature = 0.2 }, messages });
        for (int essai = 1; ; essai++)
        {
            try
            {
                var rep = await Http.PostAsync("http://127.0.0.1:11434/api/chat", new StringContent(corps, Encoding.UTF8, "application/json"));
                rep.EnsureSuccessStatusCode();
                using var d = JsonDocument.Parse(await rep.Content.ReadAsStringAsync());
                return d.RootElement.GetProperty("message").GetProperty("content").GetString()!.Trim();
            }
            catch (HttpRequestException) when (essai < 3)
            {
                await Task.Delay(2000 * essai);
            }
        }
    }
}

public class MoteurClaude : IMoteur
{
    readonly AnthropicClient _client;
    readonly string _modele;
    static readonly string[] AvecRepli = { "claude-opus-5-5", "claude-opus-5", "claude-fable-5-1", "claude-sonnet-5-5" };

    public MoteurClaude(string cle, string modele)
    {
        if (string.IsNullOrWhiteSpace(cle)) throw new InvalidOperationException("Aucune clé API Claude n'est enregistrée.");
        _client = new AnthropicClient { ApiKey = cle };
        _modele = modele;
    }

    public int Paralleles => 4;

    public async Task<string> Envoyer(string consigne, IReadOnlyList<Echange> echanges)
    {
        var p = new MessageCreateParams
        {
            Model = _modele,
            MaxTokens = 4096,
            System = consigne,
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
            Messages = echanges.Select(e => new BetaMessageParam
            {
                Role = e.Role == "assistant" ? Role.Assistant : Role.User,
                Content = e.Contenu,
            }).ToList(),
        };
        if (AvecRepli.Contains(_modele))
            p = p with
            {
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default(),
            };
        try
        {
            BetaMessage r = await _client.Beta.Messages.Create(p);
            if (r.StopReason == "refusal") throw new InvalidOperationException("Claude a refusé de traduire ce texte.");
            var texte = new StringBuilder();
            foreach (var bloc in r.Content)
                if (bloc.TryPickText(out BetaTextBlock t)) texte.Append(t.Text);
            return texte.ToString().Trim();
        }
        catch (AnthropicUnauthorizedException) { throw new InvalidOperationException("Clé API Claude refusée (401) : vérifie la clé."); }
        catch (AnthropicNotFoundException) { throw new InvalidOperationException($"Modèle Claude introuvable : {_modele}."); }
        catch (AnthropicRateLimitException) { throw new InvalidOperationException("Limite de requêtes Claude atteinte (429) : réessaie dans un moment."); }
        catch (AnthropicApiException e) { throw new InvalidOperationException("Erreur de l'API Claude : " + e.Message); }
    }
}

public class MoteurOpenAI : IMoteur
{
    readonly string _adresse, _cle, _modele;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    public MoteurOpenAI(string adresse, string cle, string modele)
    {
        if (string.IsNullOrWhiteSpace(adresse)) throw new InvalidOperationException("Aucune adresse d'API n'est enregistrée.");
        if (string.IsNullOrWhiteSpace(modele)) throw new InvalidOperationException("Aucun nom de modèle n'est enregistré.");
        _adresse = adresse.Trim().TrimEnd('/');
        _cle = cle;
        _modele = modele.Trim();
    }

    public int Paralleles => 4;

    public async Task<string> Envoyer(string consigne, IReadOnlyList<Echange> echanges)
    {
        var messages = new List<object> { new { role = "system", content = consigne } };
        messages.AddRange(echanges.Select(e => (object)new { role = e.Role, content = e.Contenu }));
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_adresse}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { model = _modele, messages }), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(_cle)) req.Headers.Authorization = new("Bearer", _cle);
        var rep = await Http.SendAsync(req);
        string corps = await rep.Content.ReadAsStringAsync();
        if (!rep.IsSuccessStatusCode)
            throw new InvalidOperationException($"Erreur de l'API ({(int)rep.StatusCode}) : {corps[..Math.Min(200, corps.Length)]}");
        using var d = JsonDocument.Parse(corps);
        return d.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!.Trim();
    }
}

public class MoteurDeepL : IMoteur, ITraductionDirecte
{
    readonly string _cle;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(1) };

    static readonly Dictionary<string, string> Cibles = new()
    {
        ["fr"] = "FR", ["en"] = "EN-US", ["es"] = "ES", ["de"] = "DE", ["it"] = "IT", ["pt"] = "PT-BR",
        ["pl"] = "PL", ["ru"] = "RU", ["tr"] = "TR", ["id"] = "ID", ["vi"] = "VI", ["ko"] = "KO",
        ["zh"] = "ZH-HANS", ["zh-TW"] = "ZH-HANT",
    };

    public MoteurDeepL(string cle)
    {
        if (string.IsNullOrWhiteSpace(cle)) throw new InvalidOperationException("Aucune clé API DeepL n'est enregistrée.");
        _cle = cle.Trim();
    }

    public int Paralleles => 4;

    public Task<string> Envoyer(string consigne, IReadOnlyList<Echange> echanges) =>
        throw new NotSupportedException();

    public async Task<string> TraduireDirect(string source, string cible, string texte, string contexte)
    {
        string hote = _cle.EndsWith(":fx") ? "https://api-free.deepl.com" : "https://api.deepl.com";
        string balise = System.Text.RegularExpressions.Regex.Replace(
            System.Security.SecurityElement.Escape(texte),
            @"\[[0-9A-Fa-f]{6}\]|\[-\]|\[[a-z]\]|\{\d+\}",
            m => $"<x>{m.Value}</x>");
        var corps = new Dictionary<string, object>
        {
            ["text"] = new[] { balise },
            ["source_lang"] = source.ToUpperInvariant(),
            ["target_lang"] = Cibles.GetValueOrDefault(cible, cible.ToUpperInvariant()),
            ["tag_handling"] = "xml",
            ["ignore_tags"] = new[] { "x" },
            ["preserve_formatting"] = true,
        };
        if (!string.IsNullOrWhiteSpace(contexte)) corps["context"] = contexte;
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{hote}/v2/translate")
        {
            Content = new StringContent(JsonSerializer.Serialize(corps), Encoding.UTF8, "application/json"),
        };
        req.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + _cle);
        var rep = await Http.SendAsync(req);
        string reponse = await rep.Content.ReadAsStringAsync();
        if (rep.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Clé API DeepL refusée (403) : vérifie la clé.");
        if ((int)rep.StatusCode == 456)
            throw new InvalidOperationException("Quota DeepL épuisé pour ce mois (456).");
        if (!rep.IsSuccessStatusCode)
            throw new InvalidOperationException($"Erreur de l'API DeepL ({(int)rep.StatusCode}) : {reponse[..Math.Min(200, reponse.Length)]}");
        using var d = JsonDocument.Parse(reponse);
        string resultat = d.RootElement.GetProperty("translations")[0].GetProperty("text").GetString() ?? "";
        resultat = System.Text.RegularExpressions.Regex.Replace(resultat, "</?x>", "");
        return System.Net.WebUtility.HtmlDecode(resultat);
    }
}
