namespace Math2Tex.Backend;

public sealed class LlmBackendOptions
{
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";

    public string Model { get; init; } = "gpt-4o-mini";

    public string? ApiKey { get; init; }

    public double Temperature { get; init; } = 0.1;

    public int MaxTokens { get; init; } = 512;

    public double TopP { get; init; } = 1.0;

    public double PresencePenalty { get; init; } = 0.0;

    public double FrequencyPenalty { get; init; } = 0.0;

    public string ExtraRequestBodyJson { get; init; } = "";

    public static LlmBackendOptions FromEnvironment()
    {
        return new LlmBackendOptions
        {
            BaseUrl = GetEnvironmentValue("MATH2TEX_LLM_BASE_URL") ?? "https://api.openai.com/v1",
            Model = GetEnvironmentValue("MATH2TEX_LLM_MODEL") ?? "gpt-4o-mini",
            ApiKey = GetEnvironmentValue("MATH2TEX_LLM_API_KEY") ?? GetEnvironmentValue("OPENAI_API_KEY"),
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
