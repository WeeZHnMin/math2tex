namespace Math2Tex.Backend;

public interface ILlmBackend
{
    LlmBackendOptions Options { get; }

    Task<LlmBackendResult> ConvertAsync(string systemPrompt, string userInput, CancellationToken cancellationToken);
}
