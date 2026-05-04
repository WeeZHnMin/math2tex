using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace Math2Tex.Backend;

public sealed class OpenAiCompatibleBackend : ILlmBackend, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public OpenAiCompatibleBackend(LlmBackendOptions options)
        : this(options, new HttpClient(), ownsHttpClient: true)
    {
    }

    public OpenAiCompatibleBackend(LlmBackendOptions options, HttpClient httpClient, bool ownsHttpClient = false)
    {
        Options = options;
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;

        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _httpClient.BaseAddress = BuildBaseAddress(options.BaseUrl);

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    }

    public LlmBackendOptions Options { get; }

    public async Task<LlmBackendResult> ConvertAsync(string systemPrompt, string userInput, CancellationToken cancellationToken)
    {
        // Build the request body as a JSON object so callers can override
        // any field via Options.ExtraRequestBodyJson (e.g. enable_thinking,
        // seed, response_format, custom provider-specific fields).
        var body = new JsonObject
        {
            ["model"] = Options.Model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userInput }
            },
            ["temperature"] = Options.Temperature,
            ["top_p"] = Options.TopP,
            ["presence_penalty"] = Options.PresencePenalty,
            ["frequency_penalty"] = Options.FrequencyPenalty,
            ["max_tokens"] = Options.MaxTokens
        };

        if (!string.IsNullOrWhiteSpace(Options.ExtraRequestBodyJson))
        {
            try
            {
                if (JsonNode.Parse(Options.ExtraRequestBodyJson) is JsonObject extras)
                {
                    foreach (var kvp in extras)
                    {
                        // user override wins over our defaults
                        body[kvp.Key] = kvp.Value?.DeepClone();
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"附加请求参数 JSON 解析失败：{ex.Message}", ex);
            }
        }

        var json = body.ToJsonString();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"LLM request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {responseBody}");
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("LLM response was empty.");

        var output = (completion.Choices ?? Array.Empty<ChatChoice>()).FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("LLM response did not contain choices[0].message.content.");
        }

        return new LlmBackendResult(output, stopwatch.Elapsed, completion.Model ?? Options.Model);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static Uri BuildBaseAddress(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/') + "/";
        return new Uri(trimmed, UriKind.Absolute);
    }

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
