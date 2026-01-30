using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PalavraSecreta.Models;

namespace PalavraSecreta.Services;

public sealed class GroqWordService
{
    private const string GroqApiKey = "##";
    private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string GroqModel = "llama-3.3-70b-versatile";

    private readonly HttpClient _http;

    public GroqWordService(HttpClient http) => _http = http;

    public async Task<bool> HasInternetAsync()
    {
        try
        {
            using var ping = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var resp = await ping.GetAsync("https://www.gstatic.com/generate_204");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<SecretWordItem>> GenerateValidSetAsync(string theme, int count, CancellationToken ct)
    {
        count = Math.Clamp(count, 1, 10);

        var result = new List<SecretWordItem>();
        var avoid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // tenta várias rodadas pra preencher
        for (int round = 1; round <= 6 && result.Count < count; round++)
        {
            var need = count - result.Count;

            var prompt = BuildPrompt(theme, need, avoid);

            var content = await CallGroqAsync(prompt, temperature: 0.2, ct);
            if (string.IsNullOrWhiteSpace(content)) continue;

            var items = ParseJsonList(content, theme);

            // primeira filtragem estrutural
            items = items
                .Where(x => x.IsStructurallyValid())
                .ToList();

            if (items.Count == 0) continue;

            // validação de “existência” via SIM/NAO (base e revealed)
            var validated = await ValidateWordsExistAsync(items, ct);

            foreach (var it in validated)
            {
                var key = $"{it.BaseWord}|{it.RevealedWord}|{it.HiddenStart}|{it.HiddenLength}|{it.HiddenColor}";
                if (avoid.Contains(key)) continue;

                avoid.Add(key);
                result.Add(it);

                if (result.Count >= count)
                    break;
            }
        }

        return result;
    }

    // ============================
    // Validação (SIM/NAO) — garante que não entra TRE/LO etc.
    // ============================
    private async Task<List<SecretWordItem>> ValidateWordsExistAsync(List<SecretWordItem> items, CancellationToken ct)
    {
        // Monta lista única de palavras a validar
        var uniqueWords = items
            .SelectMany(x => new[] { x.BaseWord, x.RevealedWord })
            .Select(x => (x ?? "").Trim().ToUpperInvariant())
            .Where(x => x.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueWords.Count == 0) return new();

        var prompt = BuildExistenceCheckPrompt(uniqueWords);

        var content = await CallGroqAsync(prompt, temperature: 0.0, ct);
        if (string.IsNullOrWhiteSpace(content)) return new();

        // Esperado: JSON { "PALAVRA": true/false, ... }
        var map = ParseBoolMap(content);

        bool Ok(string w)
        {
            w = (w ?? "").Trim().ToUpperInvariant();
            return map.TryGetValue(w, out var ok) && ok;
        }

        return items
            .Where(x => Ok(x.BaseWord) && Ok(x.RevealedWord))
            .ToList();
    }

    private static string BuildExistenceCheckPrompt(List<string> words)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Você é um verificador de dicionário do português do Brasil.");
        sb.AppendLine("Responda SOMENTE com JSON válido, sem markdown e sem texto extra.");
        sb.AppendLine("Para cada palavra em MAIÚSCULO, responda true se for uma palavra real e comum do português, senão false.");
        sb.AppendLine("Não aceite abreviações, siglas, pedaços de palavra, radicais ou palavras inventadas.");
        sb.AppendLine();
        sb.AppendLine("FORMATO:");
        sb.AppendLine(@"{ ""PALAVRA1"": true, ""PALAVRA2"": false }");
        sb.AppendLine();
        sb.AppendLine("PALAVRAS:");
        foreach (var w in words)
            sb.AppendLine("- " + w);
        return sb.ToString();
    }

    private static Dictionary<string, bool> ParseBoolMap(string raw)
    {
        var text = (raw ?? "").Trim()
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        // tenta extrair o objeto JSON
        var m = Regex.Match(text, @"\{\s*""[^""]+""\s*:\s*(true|false).*?\}", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (m.Success) text = m.Value;

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new();

            // normaliza chaves
            return dict.ToDictionary(
                kv => (kv.Key ?? "").Trim().ToUpperInvariant(),
                kv => kv.Value,
                StringComparer.OrdinalIgnoreCase
            );
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    // ============================
    // Groq call
    // ============================
    private async Task<string> CallGroqAsync(string prompt, double temperature, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(GroqApiKey) || GroqApiKey.Contains("COLE_SUA_CHAVE"))
            throw new InvalidOperationException("GroqApiKey não configurada em GroqWordService.");

        using var req = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GroqApiKey);

        var body = new
        {
            model = GroqModel,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = temperature
        };

        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }

    // ============================
    // Prompt de geração (mais rígido)
    // ============================
    private static string BuildPrompt(string theme, int count, HashSet<string> avoid)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Você gera itens do jogo escolar \"PALAVRA SECRETA COM LUPA\".");
        sb.AppendLine("Responda SOMENTE com JSON válido (sem markdown e sem texto extra).");
        sb.AppendLine();
        sb.AppendLine($"Gere EXATAMENTE {count} itens no formato:");
        sb.AppendLine(@"[
  { ""baseWord"":""ABACATE"", ""hiddenStart"":0, ""hiddenLength"":3, ""hiddenColor"":""red"", ""revealedWord"":""CATE"" }
]");
        sb.AppendLine();
        sb.AppendLine("REGRAS OBRIGATÓRIAS:");
        sb.AppendLine("1) baseWord e revealedWord devem ser PALAVRAS REAIS do português (comuns).");
        sb.AppendLine("2) baseWord: 4 a 10 letras, MAIÚSCULO, sem acento, sem espaços, só A-Z.");
        sb.AppendLine("3) hiddenLength: SOMENTE 2 ou 3 (trecho contínuo).");
        sb.AppendLine("4) revealedWord deve ter NO MÍNIMO 3 letras.");
        sb.AppendLine("5) revealedWord DEVE ser exatamente baseWord removendo o trecho [hiddenStart..hiddenStart+hiddenLength).");
        sb.AppendLine("6) NÃO use radicais (ex: TRE, LO), NÃO use pedaços de palavra, NÃO invente.");
        sb.AppendLine("7) hiddenColor: red|green|blue|yellow|purple|black.");
        sb.AppendLine();

        sb.AppendLine("TEMA (use palavras relacionadas quando possível):");
        sb.AppendLine(theme);

        if (avoid.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("NÃO REPETIR ESTES ITENS (chaves):");
            foreach (var k in avoid.Take(40))
                sb.AppendLine("- " + k);
        }

        return sb.ToString();
    }

    // ============================
    // Parse JSON de itens
    // ============================
    private static List<SecretWordItem> ParseJsonList(string raw, string theme)
    {
        var text = (raw ?? "").Trim()
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        // tenta capturar o array
        var m = Regex.Match(text, @"\[\s*\{.*\}\s*\]", RegexOptions.Singleline);
        if (m.Success) text = m.Value;

        try
        {
            var arr = JsonSerializer.Deserialize<List<TempItem>>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new();

            var result = new List<SecretWordItem>();

            foreach (var it in arr)
            {
                var baseW = NormalizeWord(it.baseWord);
                var revW = NormalizeWord(it.revealedWord);

                var item = new SecretWordItem
                {
                    Theme = theme,
                    BaseWord = baseW,
                    HiddenStart = it.hiddenStart,
                    HiddenLength = it.hiddenLength,
                    HiddenColor = NormalizeColor(it.hiddenColor) ?? "red",
                    RevealedWord = revW
                };

                result.Add(item);
            }

            return result;
        }
        catch
        {
            return new();
        }
    }

    private static string? NormalizeColor(string? c)
    {
        var x = (c ?? "").Trim().ToLowerInvariant();
        return x switch
        {
            "red" or "green" or "blue" or "yellow" or "purple" or "black" => x,
            _ => null
        };
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

    private sealed class TempItem
    {
        public string baseWord { get; set; } = "";
        public int hiddenStart { get; set; }
        public int hiddenLength { get; set; }
        public string hiddenColor { get; set; } = "red";
        public string revealedWord { get; set; } = "";
    }
}
