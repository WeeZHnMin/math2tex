namespace Math2Tex.Backend;

public sealed record LlmBackendResult(string Content, TimeSpan Latency, string Model);
