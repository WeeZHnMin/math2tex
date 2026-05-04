<div align="center">

<img src="logo.png" alt="Math2Tex" width="120" />

# Math2Tex

**剪贴板里复制的乱码数学公式 → LaTeX 源码，自动替换。**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D4?logo=windows)](https://www.microsoft.com/windows)
![License](https://img.shields.io/badge/license-MIT-green.svg)
[![English](https://img.shields.io/badge/lang-English-blue)](README.en.md)

</div>

---

## 它是什么

一个常驻系统托盘的小工具：你从 PDF / 网页随手复制的数学公式（带 OCR 排版乱码 / Unicode / 各种花活儿），它**自动**送给 OpenAI 兼容的 LLM 转成干净的 LaTeX，然后**直接替换**你剪贴板里的内容 —— 你 `Ctrl+V` 出来就是 LaTeX 源码。

```text
你看到的：     P(y∣x)=k=1∑K​πk​(x)⋅Zk​1​exp(−21​(y−μk​(x))⊤Σk−1​(y−μk​(x)))
你按 Ctrl+C，
程序判断这是公式，
你按 Ctrl+V：  P(y \mid x) = \sum_{k=1}^{K} \pi_k(x) \cdot \frac{1}{Z_k} \exp\!\left(-\frac{1}{2}(y-\mu_k(x))^\top \Sigma_k^{-1}(y-\mu_k(x))\right)
```

---

## 特性

- ✨ **零交互**：复制即转换，不用按热键、不用弹窗
- 🧠 **本地预筛**：不含数学字符的内容直接跳过，不浪费 token
- 🛡 **协议化提示词**：LLM 判断为非公式时返回 `__SKIP__`，剪贴板原样不动
- ⚙️ **OpenAI 兼容**：能用 GPT、DeepSeek、Mimo、Qwen、Claude（通过兼容网关）等任意服务商
- 📋 **请求体可定制**：UI 里直接改 JSON，加 `enable_thinking`、`seed`、`response_format` 等
- 📜 **历史记录**：本地 JSON 持久化，含搜索 / 复制 / 删除
- 🪟 **现代 UI**：基于 [WPF-UI](https://wpfui.lepo.co/) 的 Fluent / Mica 设计
- 🔁 **可选全局热键** 显示/隐藏窗口
- 🚀 **开机自启**：托盘菜单一键开关
- 🪶 **轻量**：占用 ~50MB 内存，启动 1-2s

---

## 快速开始

### 给最终用户：装个安装包

1. 去 [Releases](https://github.com/WeeZHnMin/math2tex/releases) 下载 `Math2Tex-Setup-x.y.z.exe`
2. 双击向导式安装，可选择"开机自启动"
3. 装完会自动运行；之后桌面快捷方式 / 开始菜单都有
4. 进**设置 Tab** 填 Base URL / API Key / Model，**保存**
5. 点**测试**确认连通
6. 复制公式，自动转换 ✨

### 给最终用户：免安装绿色版

1. 去 Releases 下 `Math2Tex-Portable-x.y.z.zip`
2. 解压到任意位置
3. 双击 `Math2Tex.exe`
4. 后续步骤同上

### 给开发者：源码运行

```powershell
git clone https://github.com/WeeZHnMin/math2tex.git
cd math2tex
dotnet run
```

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

---

## 自己打包

仓库里给了三套现成脚本，**双击 .cmd 文件**即可：

| 文件 | 产物 | 用途 |
| --- | --- | --- |
| `install.cmd` | 安装到 `%LocalAppData%\Math2Tex\` | 本机使用 |
| `build-portable.cmd` | `portable\Math2Tex-Portable-*.zip` | 分发绿色版 |
| `build-installer.cmd` | `installer\Math2Tex-Setup-*.exe` | 正式安装包（需先装 [Inno Setup](https://jrsoftware.org/isdl.php)） |

`uninstall.cmd` 可一键卸载（加 `-Purge` 参数同时清掉用户数据）。

---

## 数据存储位置

```text
%AppData%\Math2Tex\
  ├─ backend-settings.json     # API Key / 模型 / 提示词 / 自启状态
  ├─ history.json              # 转换历史
  └─ debug.log                 # 调试日志
```

⚠️ API Key **明文**存盘。多人共用电脑请留意。

---

## 默认提示词

[Backend/BackendSettings.cs](Backend/BackendSettings.cs) 里硬编码了一份严格协议化的默认提示词，让 LLM：

- 是公式 → 仅输出 LaTeX 源码（无 ```` ``` ````、无 `$`、无解释）
- 非公式 → 仅输出 `__SKIP__`

可以在 UI **设置 → 系统提示词** 里随时改。

---

## 配置示例

针对 mimo 之类要求 `enable_thinking` 的国产模型，**请求体附加参数**填：

```json
{
  "enable_thinking": false
}
```

针对要 `response_format` 的：

```json
{
  "response_format": { "type": "text" },
  "seed": 42
}
```

UI 上保存即生效，会合并到 `chat/completions` 请求体。

---

## 实现细节

- WPF + .NET 8 + WPF-UI 4.2
- Win32 `AddClipboardFormatListener` 监听剪贴板变化（不轮询）
- Win32 `OpenClipboard / SetClipboardData` 写入，配合 `AttachThreadInput` 蹭前台优先级
- `Clipboard.SetDataObject` 在专属 STA 后台线程跑，UI 永不冻结
- 防自我回环：本程序写入的内容不会再次触发自身
- 单实例 Mutex 防多开冲突
- 配置 read-modify-write，从不全量覆盖

---

## License

MIT
