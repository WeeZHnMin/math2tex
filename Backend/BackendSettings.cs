using System.IO;
using System.Text.Json;

namespace Math2Tex.Backend;

public sealed class BackendSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public double Temperature { get; set; } = 0.1;
    public int MaxTokens { get; set; } = 512;
    public double TopP { get; set; } = 1.0;
    public double PresencePenalty { get; set; } = 0.0;
    public double FrequencyPenalty { get; set; } = 0.0;
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;
    public string ExtraRequestBodyJson { get; set; } = DefaultExtraRequestBodyJson;

    // Win32 RegisterHotKey: modifier bitmask + virtual key code. 0/0 = disabled.
    public uint HotkeyModifiers { get; set; } = 0;
    public uint HotkeyKey { get; set; } = 0;

    // Auto-convert: listen to clipboard text changes and call LLM automatically.
    public bool AutoConvert { get; set; } = true;
    public int AutoConvertMaxChars { get; set; } = 600;

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Math2Tex",
            "backend-settings.json");

    public static BackendSettings Load()
    {
        var settings = FromEnvironment();
        var path = SettingsPath;
        if (!File.Exists(path))
        {
            return settings;
        }

        try
        {
            var json = File.ReadAllText(path);
            var saved = JsonSerializer.Deserialize<BackendSettings>(json, JsonOptions);
            if (saved is not null)
            {
                settings = saved;
            }
        }
        catch
        {
            // Fall back to environment defaults.
        }

        return settings;
    }

    public void Save()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    public LlmBackendOptions ToBackendOptions()
    {
        return new LlmBackendOptions
        {
            BaseUrl = BaseUrl,
            ApiKey = ApiKey,
            Model = Model,
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            TopP = TopP,
            PresencePenalty = PresencePenalty,
            FrequencyPenalty = FrequencyPenalty,
            ExtraRequestBodyJson = ExtraRequestBodyJson
        };
    }

    public void ResetToDefaults()
    {
        BaseUrl = "https://api.openai.com/v1";
        ApiKey = string.Empty;
        Model = "gpt-4o-mini";
        Temperature = 0.1;
        MaxTokens = 512;
        TopP = 1.0;
        PresencePenalty = 0.0;
        FrequencyPenalty = 0.0;
        HotkeyModifiers = 0;
        HotkeyKey = 0;
        AutoConvert = true;
        AutoConvertMaxChars = 600;
        SystemPrompt = DefaultSystemPrompt;
        ExtraRequestBodyJson = DefaultExtraRequestBodyJson;
    }

    /// <summary>
    /// Extra fields merged into the chat/completions request body (override
    /// strongly-typed defaults). Default disables thinking-mode for providers
    /// that require it (e.g. mimo); empty {} also fine for standard OpenAI.
    /// </summary>
    public const string DefaultExtraRequestBodyJson = """
{
  "enable_thinking": false
}
""";

    public const string DefaultSystemPrompt = """
You are a clipboard math-to-LaTeX converter. STRICT OUTPUT PROTOCOL — no exceptions.

# RULES

1) If the input IS, or SUBSTANTIALLY IS, a mathematical expression / equation / formula / matrix / inline math (even when broken by OCR, line breaks, or surrounding annotations):
   → Output ONLY the LaTeX source. Nothing else.
   → NO Markdown fences (```), NO $ or $$ wrappers, NO \( \), NO "Here is", NO leading/trailing prose, NO trailing period unless it's part of the math.
   → Multi-line displays: separate with \\.
   → Use proper macros: \frac{}{}, \sum_{i=1}^{n}, \int, \sqrt{}, \log, \exp, \alpha \beta ...,
     \mathcal{N} for distributions, \mathbf / \boldsymbol for vectors/matrices,
     ^\top (not ^T) for transpose, \mid (not |) for conditional bars,
     \cdot for multiplication where appropriate, \le \ge \ne instead of <= >= !=.
   → Strip stray spaces / zero-width chars / "​" produced by OCR. Reconstruct stacked super/subscripts.

2) If the input is ANYTHING ELSE — plain sentences, code, URLs, file paths, error logs, single isolated numbers/letters, prose with no actual formula — output EXACTLY this literal token, nothing before or after, no newline:
   __SKIP__

# EDGE CASES
- Mixed Chinese / English annotations next to a formula → extract the formula only, drop the annotations.
- Programming code (Python / JS / shell / SQL) → __SKIP__ even if it contains +, -, *, /.
- Single isolated number ("42") or single variable ("x") with no operator/relation → __SKIP__.
- Already-valid LaTeX (e.g., starts with \frac or contains \sum) → still output, normalized.
- Plain ASCII formula like "F = ma" or "E=mc^2" → convert and output (F = ma  /  E = mc^2).
- Don't invent symbols you can't see; preserve the original variable names.

# EXAMPLES

INPUT:
L=−logP(y∣x)
OUTPUT:
L = -\log P(y \mid x)

INPUT:
今天天气真好，要不要去散步？
OUTPUT:
__SKIP__

INPUT:
def add(a, b): return a + b
OUTPUT:
__SKIP__

INPUT:
F = ma
OUTPUT:
F = ma

INPUT:
42
OUTPUT:
__SKIP__

# NOW PROCESS THE INPUT BELOW
Apply the rules exactly. Output ONLY LaTeX or ONLY __SKIP__.
""";

    public static BackendSettings FromEnvironment()
    {
        return new BackendSettings
        {
            BaseUrl = GetEnvironmentValue("MATH2TEX_LLM_BASE_URL") ?? "https://api.openai.com/v1",
            ApiKey = GetEnvironmentValue("MATH2TEX_LLM_API_KEY") ?? GetEnvironmentValue("OPENAI_API_KEY") ?? string.Empty,
            Model = GetEnvironmentValue("MATH2TEX_LLM_MODEL") ?? "gpt-4o-mini",
            Temperature = TryGetDouble("MATH2TEX_LLM_TEMPERATURE", 0.1),
            MaxTokens = TryGetInt("MATH2TEX_LLM_MAX_TOKENS", 512),
            TopP = TryGetDouble("MATH2TEX_LLM_TOP_P", 1.0),
            PresencePenalty = TryGetDouble("MATH2TEX_LLM_PRESENCE_PENALTY", 0.0),
            FrequencyPenalty = TryGetDouble("MATH2TEX_LLM_FREQUENCY_PENALTY", 0.0)
        };
    }

    private static string? GetEnvironmentValue(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static double TryGetDouble(string name, double fallback)
    {
        var value = GetEnvironmentValue(name);
        return double.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int TryGetInt(string name, int fallback)
    {
        var value = GetEnvironmentValue(name);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
