# Math2Tex

A small WPF desktop shell for a future math-to-LaTeX workflow.

## Run

Install the .NET 8 SDK with desktop workload support, then run:

```powershell
dotnet run
```

## LLM Backend

The app includes a C# backend layer for OpenAI-compatible chat completion APIs.

Set these environment variables before launching the app:

```powershell
$env:MATH2TEX_LLM_BASE_URL = "https://api.openai.com/v1"
$env:MATH2TEX_LLM_MODEL = "gpt-4o-mini"
$env:MATH2TEX_LLM_API_KEY = "your_api_key"
dotnet run
```

For another OpenAI-format provider, point `MATH2TEX_LLM_BASE_URL` at its `/v1` base URL. The backend posts to `chat/completions` and reads `choices[0].message.content`.
`MATH2TEX_LLM_API_KEY` is sent as `Bearer` auth when present, but can stay empty for providers that do not require it.

## UI Notes

- WPF desktop app targeting `net8.0-windows`.
- Lightweight code-behind for the first prototype.
- Custom `FormulaPreview` renderer uses `DrawingContext` instead of many child controls.
- History list enables UI virtualization and item recycling.
- Per-monitor DPI awareness is enabled in `app.manifest`.
