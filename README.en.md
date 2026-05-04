<div align="center">

<img src="logo.png" alt="Math2Tex" width="120" />

# Math2Tex

**Copy garbled math from anywhere → get clean LaTeX in your clipboard, automatically.**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D4?logo=windows)](https://www.microsoft.com/windows)
![License](https://img.shields.io/badge/license-MIT-green.svg)
[![中文](https://img.shields.io/badge/lang-中文-red)](README.md)

</div>

---

## What is it

A small Windows tray app: when you copy mangled math (OCR-broken from PDFs, weird unicode, line-broken display equations), it **automatically** sends the clipboard text to an OpenAI-compatible LLM, gets clean LaTeX back, and **replaces** the clipboard so your next `Ctrl+V` pastes valid LaTeX source.

```text
You copied:    P(y∣x)=k=1∑K​πk​(x)⋅Zk​1​exp(−21​(y−μk​(x))⊤Σk−1​(y−μk​(x)))
App detects it as math,
You paste:     P(y \mid x) = \sum_{k=1}^{K} \pi_k(x) \cdot \frac{1}{Z_k} \exp\!\left(-\frac{1}{2}(y-\mu_k(x))^\top \Sigma_k^{-1}(y-\mu_k(x))\right)
```

---

## Features

- ✨ **Zero-interaction**: copy = convert. No hotkey, no popup
- 🧠 **Local pre-filter**: clipboard text without any math signal is skipped instantly — saves tokens
- 🛡 **Strict protocol**: LLM returns the literal token `__SKIP__` for non-math; clipboard untouched
- ⚙️ **OpenAI-compatible**: works with GPT, DeepSeek, Mimo, Qwen, Claude (via gateways), any provider
- 📋 **Editable request body**: tweak the JSON body in the UI to add `enable_thinking`, `seed`, `response_format`, etc.
- 📜 **History**: persisted JSON store with search / copy / delete
- 🪟 **Modern UI**: Fluent / Mica via [WPF-UI](https://wpfui.lepo.co/)
- 🔁 **Optional global hotkey** to show/hide the window
- 🚀 **Autostart**: one-click toggle from the tray menu
- 🪶 **Lightweight**: ~50MB RAM, 1-2s startup

---

## Quick start

### For end users — installer

1. Grab `Math2Tex-Setup-x.y.z.exe` from [Releases](https://github.com/WeeZHnMin/math2tex/releases)
2. Run the wizard (no admin needed; per-user install). Optionally tick "Start at login"
3. App launches automatically; desktop & start menu shortcuts created
4. Open **Settings tab** → fill in Base URL / API Key / Model, click **Save**
5. Click **Test** to verify
6. Copy a formula anywhere — it converts automatically ✨

### For end users — portable zip

1. Download `Math2Tex-Portable-x.y.z.zip` from Releases
2. Extract anywhere
3. Double-click `Math2Tex.exe`
4. Same setup steps as above

### For developers — run from source

```powershell
git clone https://github.com/WeeZHnMin/math2tex.git
cd math2tex
dotnet run
```

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

---

## Build it yourself

Three ready-made scripts. Just double-click the `.cmd`:

| File | Output | Use |
| --- | --- | --- |
| `install.cmd` | Installs to `%LocalAppData%\Math2Tex\` | Local use |
| `build-portable.cmd` | `portable\Math2Tex-Portable-*.zip` | Distribute as portable |
| `build-installer.cmd` | `installer\Math2Tex-Setup-*.exe` | Proper installer (requires [Inno Setup](https://jrsoftware.org/isdl.php)) |

`uninstall.cmd` removes the install (pass `-Purge` to also wipe user data).

---

## Where data lives

```text
%AppData%\Math2Tex\
  ├─ backend-settings.json     # API key / model / prompt / autostart flag
  ├─ history.json              # conversion history
  └─ debug.log                 # diagnostic log
```

⚠️ The API key is stored in **plain text**. Be aware on shared machines.

---

## Default prompt

A strict, protocol-shaped default prompt is embedded in [Backend/BackendSettings.cs](Backend/BackendSettings.cs). It tells the LLM:

- It IS math → output ONLY the LaTeX source (no ```` ``` ````, no `$`, no prose)
- It is NOT math → output ONLY the literal token `__SKIP__`

Editable in the UI under **Settings → System Prompt**.

---

## Configuration examples

For models like mimo that require `enable_thinking` to be off in non-streaming calls, set the **Extra request body** field to:

```json
{
  "enable_thinking": false
}
```

Want a deterministic seed and a structured response format:

```json
{
  "response_format": { "type": "text" },
  "seed": 42
}
```

These fields are merged into the `chat/completions` request body at call time.

---

## Implementation notes

- WPF + .NET 8 + WPF-UI 4.2
- Win32 `AddClipboardFormatListener` for event-driven clipboard monitoring (no polling)
- Win32 `OpenClipboard / SetClipboardData` for writes, paired with `AttachThreadInput` to share foreground priority for openings
- Clipboard ops run on a dedicated background STA thread so the UI never freezes
- Self-write guard: text we just wrote ourselves does not re-trigger conversion
- Single-instance Mutex prevents the autostart entry from spawning duplicates
- All settings persistence is read-modify-write — never overwrites unrelated fields

---

## License

MIT
