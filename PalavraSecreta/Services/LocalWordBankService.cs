using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PalavraSecreta.Models;

namespace PalavraSecreta.Services;

public sealed class LocalWordBankService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private const string DefaultBankAssetPath = "default_word_bank.json";

    private string BankPath => Path.Combine(FileSystem.AppDataDirectory, "word_bank.json");

    private sealed class BankRoot
    {
        public int Version { get; set; } = 1;
        public List<BankItem> Items { get; set; } = new();
    }

    private sealed class BankItem
    {
        public string Theme { get; set; } = "Outros";

        public string BaseWord { get; set; } = "";
        public string RevealedWord { get; set; } = "";

        public int HiddenStart { get; set; }
        public int HiddenLength { get; set; }

        public string HiddenColor { get; set; } = "red";

        public string Source { get; set; } = "default"; // default | user
        public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public async Task EnsureBankAsync()
    {
        bool needSeed = true;

        if (File.Exists(BankPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(BankPath, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var root = JsonSerializer.Deserialize<BankRoot>(json, JsonOpts);
                    if (root?.Items != null && root.Items.Count > 0)
                        needSeed = false;
                }
            }
            catch
            {
                needSeed = true;
            }
        }

        if (!needSeed)
            return;

        try
        {
            // copia o template para o banco real
            await using var stream = await FileSystem.OpenAppPackageFileAsync(DefaultBankAssetPath);
            using var sr = new StreamReader(stream, Encoding.UTF8);
            var json = await sr.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(json))
                json = JsonSerializer.Serialize(new BankRoot(), JsonOpts);

            await File.WriteAllTextAsync(BankPath, json, Encoding.UTF8);
        }
        catch
        {
            var json = JsonSerializer.Serialize(new BankRoot(), JsonOpts);
            await File.WriteAllTextAsync(BankPath, json, Encoding.UTF8);
        }
    }



    public async Task<int> GetCountAsync(string? theme = null)
    {
        var root = await LoadAsync();
        var list = root.Items ?? new List<BankItem>();

        if (string.IsNullOrWhiteSpace(theme) || theme.Equals("Aleatório", StringComparison.OrdinalIgnoreCase))
            return list.Count;

        return list.Count(x => string.Equals(NormTheme(x.Theme), NormTheme(theme), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<SecretWordItem>> GetRandomSetAsync(string theme, int count)
    {
        await EnsureBankAsync();

        // ✅ permite 12
        count = Math.Clamp(count, 1, 12);

        var root = await LoadAsync();
        var all = root.Items ?? new List<BankItem>();

        if (all.Count == 0)
            return new();

        var requested = NormTheme(theme);

        List<BankItem> src;

        if (requested == "ALEATORIO" || string.IsNullOrWhiteSpace(requested))
        {
            src = all;
        }
        else
        {
            src = all.Where(x => NormTheme(x.Theme) == requested).ToList();
            if (src.Count == 0) src = all; // fallback
        }

        // embaralha
        var rnd = new Random();
        src = src.OrderBy(_ => rnd.Next()).ToList();

        // ✅ se o tema não tiver itens suficientes, retorna o máximo possível
        var take = Math.Min(count, src.Count);

        var result = new List<SecretWordItem>(take);

        foreach (var x in src)
        {
            if (result.Count >= take) break;

            var item = new SecretWordItem
            {
                Theme = x.Theme,
                BaseWord = x.BaseWord,
                RevealedWord = x.RevealedWord,
                HiddenStart = x.HiddenStart,
                HiddenLength = x.HiddenLength,
                HiddenColor = x.HiddenColor
            };

            if (!item.IsStructurallyValid())
            {
                if (TryInferCut(item.BaseWord, item.RevealedWord, out var st, out var len) && len > 0)
                {
                    item.HiddenStart = st;
                    item.HiddenLength = len;

                    if (item.IsStructurallyValid())
                        result.Add(item);
                }

                continue;
            }

            result.Add(item);
        }

        return result;
    }

    public async Task<(bool ok, string msg)> AddUserItemAsync(string theme, string baseWord, string revealedWord, string color)
    {
        await EnsureBankAsync();

        theme = CleanTheme(theme);
        baseWord = NormalizeWord(baseWord);
        revealedWord = NormalizeWord(revealedWord);
        color = NormalizeColor(color);

        if (baseWord.Length < 4 || baseWord.Length > 10)
            return (false, "Palavra base deve ter 4 a 10 letras (sem acento).");

        if (revealedWord.Length < 2 || revealedWord.Length >= baseWord.Length)
            return (false, "Palavra revelada deve ser menor que a base (mínimo 2 letras).");

        if (!TryInferCut(baseWord, revealedWord, out var start, out var len))
            return (false, "A palavra revelada precisa ser exatamente a palavra base removendo UM trecho contínuo.");

        if (len is < 1 or > 6)
            return (false, "O trecho removido ficou estranho. Ajuste as palavras.");

        var computed = baseWord.Remove(start, len);
        if (!string.Equals(computed, revealedWord, StringComparison.OrdinalIgnoreCase))
            return (false, "Inconsistência interna: a palavra revelada não bate com o corte.");

        var root = await LoadAsync();
        root.Items ??= new List<BankItem>();

        var key = $"{NormTheme(theme)}|{baseWord}|{revealedWord}|{start}|{len}|{color}";
        var exists = root.Items.Any(x => $"{NormTheme(x.Theme)}|{NormalizeWord(x.BaseWord)}|{NormalizeWord(x.RevealedWord)}|{x.HiddenStart}|{x.HiddenLength}|{NormalizeColor(x.HiddenColor)}"
            .Equals(key, StringComparison.OrdinalIgnoreCase));

        if (exists)
            return (false, "Esse item já existe no banco.");

        root.Items.Add(new BankItem
        {
            Theme = theme,
            BaseWord = baseWord,
            RevealedWord = revealedWord,
            HiddenStart = start,
            HiddenLength = len,
            HiddenColor = "red",
            Source = "user",
            AddedAt = DateTimeOffset.UtcNow
        });

        await SaveAsync(root);
        return (true, "Item adicionado no banco.");
    }

    public static bool TryInferCut(string baseWord, string revealedWord, out int start, out int len)
    {
        start = -1; len = 0;

        var b = NormalizeWord(baseWord);
        var r = NormalizeWord(revealedWord);

        if (string.IsNullOrWhiteSpace(b) || string.IsNullOrWhiteSpace(r)) return false;
        if (r.Length >= b.Length) return false;

        for (int s = 0; s < b.Length; s++)
        {
            for (int l = 1; l <= b.Length - s; l++)
            {
                var cut = b.Remove(s, l);
                if (string.Equals(cut, r, StringComparison.OrdinalIgnoreCase))
                {
                    start = s;
                    len = l;
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<BankRoot> LoadAsync()
    {
        try
        {
            if (!File.Exists(BankPath))
                return new BankRoot();

            var json = await File.ReadAllTextAsync(BankPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return new BankRoot();

            return JsonSerializer.Deserialize<BankRoot>(json, JsonOpts) ?? new BankRoot();
        }
        catch
        {
            return new BankRoot();
        }
    }

    private async Task SaveAsync(BankRoot root)
    {
        try
        {
            var json = JsonSerializer.Serialize(root, JsonOpts);
            await File.WriteAllTextAsync(BankPath, json, Encoding.UTF8);
        }
        catch { }
    }

    private static string NormalizeWord(string? s)
    {
        var w = (s ?? "").Trim().ToUpperInvariant();
        w = RemoveDiacritics(w);
        w = Regex.Replace(w, @"[^A-Z]", "");
        return w;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeColor(string? c)
    {
        var x = (c ?? "").Trim().ToLowerInvariant();
        return x switch
        {
            "red" or "green" or "blue" or "yellow" or "purple" or "black" => x,
            _ => "red"
        };
    }

    private static string CleanTheme(string? theme)
    {
        var t = (theme ?? "").Trim();
        if (string.IsNullOrWhiteSpace(t)) return "Outros";

        t = t.ToLowerInvariant() switch
        {
            "animais" => "Animais",
            "frutas" => "Frutas",
            "objetos" => "Objetos",
            "outros" => "Outros",
            "aleatório" or "aleatorio" => "Aleatório",
            _ => "Outros"
        };

        return t;
    }

    private static string NormTheme(string? theme)
        => CleanTheme(theme).Trim().ToUpperInvariant();
}
