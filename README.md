# Quick Claude Wake

<p align="center">
  <strong>A lightning-fast, lightweight auto-wake scheduler and session manager for Claude Code CLI on Windows 10 &amp; 11 built with WinUI 3 and .NET 8.</strong>
</p>

<p align="center">
  <a href="https://github.com/uponatime2019/QuickClaudeWake/releases/latest"><img src="https://img.shields.io/github/v/release/uponatime2019/QuickClaudeWake?style=flat-square&color=blue" alt="Latest Release" /></a>
  <img src="https://img.shields.io/badge/.NET-8.0-blueviolet?style=flat-square" alt=".NET 8" />
  <img src="https://img.shields.io/badge/UI-WinUI%203-0078D7?style=flat-square" alt="WinUI 3" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-00a4ef?style=flat-square" alt="Windows 10/11" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green?style=flat-square" alt="License: MIT" /></a>
  <img src="https://img.shields.io/badge/Packaging-Standalone%20Portable-success?style=flat-square" alt="Standalone Portable" />
</p>

---

## ⚡ Try It Now / Download

Get the standalone portable release with zero installation required:

👉 **[Download Latest Release (QuickClaudeWake-win-x64.zip)](https://github.com/uponatime2019/QuickClaudeWake/releases/latest)**

1. Download and extract the `.zip` archive to any directory.
2. Run `QuickClaudeWake.exe`.
3. Quick Claude Wake automatically discovers your local Claude Code sessions and monitors their rate-limit reset windows!

---

## 📸 Screenshot

![App Screenshot](Assets/screenshot.png)

---

## ✨ Features Overview

- **⏰ Smart Auto-Wake Rate Limit Scheduler**: Real-time scanner for Claude Code CLI sessions stopped by 5-hour rate limits (`~/.claude/projects/`). Automatically wakes all sessions at the exact reset moment using `claude --resume <id> --effort max --permission-mode auto -p "continue"`.
- **⏱️ Dedicated Global Cooldown Countdown**: Top-level visual timer continuously tracking the exact cooldown time remaining until global API rate limits reset.
- **🔄 Continuous Loop Mode**: Automatically detects when Claude Code encounters subsequent rate limits during long jobs and keeps monitoring and resuming until all tasks finish.
- **⚡ Instant Manual Continue**: Continue individual sessions or all ready sessions at once with one click.
- **💻 Direct Terminal Integration**: Launch Claude Code sessions interactively in Windows Terminal (`wt.exe`) or Cmd (`cmd.exe`) directly to the session's workspace.
- **📜 Recent Sessions Browser**: Inspect recent Claude Code conversations across projects with full prompt previews, timestamps, and one-click resumption.
- **📊 Optional Live Quota Monitor**: Optional GLM / Z.ai quota monitoring (cleanly configured via settings/UI, zero hardcoded credentials).
- **🔔 Windows Notifications & Optional Telegram Alerts**: Native Windows Toast notifications on wake events, plus optional Telegram bot alerts for mobile tracking.
- **🎨 Windows 11 Fluent Design**: Native Mica backdrop, smooth animations, and automatic System / Light / Dark theme support.
- **🚀 Portable & Single-Instance**: Runs unpackaged from any folder with automatic single-instance window restoration and optional Windows startup integration.

---

## 🏗️ Architecture & Technology Stack

| Component | Technology | Description |
| :--- | :--- | :--- |
| **Framework** | .NET 8.0 (`net8.0-windows10.0.19041.0`) | High-performance C# runtime with modern language features |
| **UI Layer** | WinUI 3 (Windows App SDK 2.4) | Native Windows Fluent Design UI with Mica material |
| **Agent CLI** | Anthropic Claude Code CLI (`claude.exe`) | Seamless CLI bridge for automatic resuming and interactive terminals |
| **Configuration** | System.Text.Json | Clean local settings in `%LOCALAPPDATA%\QuickClaudeWake\settings.json` |
| **Deployment** | Self-Contained Unpackaged (`WindowsPackageType=None`) | Portable standalone executable, no MSIX certificate or Store dependencies |

---

## 🛠️ Building & Running from Source

### Prerequisites
- **Windows 10 (1809+) or Windows 11**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with **.NET Desktop Development** workload

### Build & Run
```powershell
# Clone the repository
git clone https://github.com/uponatime2019/QuickClaudeWake.git
cd QuickClaudeWake

# Build the project
dotnet build "QuickClaudeWake.csproj" -p:Platform=x64

# Run the app locally
dotnet run --project "QuickClaudeWake.csproj"
```

### Self-Contained Publish
To create a standalone portable release folder:
```powershell
dotnet publish "QuickClaudeWake.csproj" -c Release -p:Platform=x64 -o "publish/QuickClaudeWake_Portable"
```

---

## 🗺️ Roadmap

- [x] Unpackaged standalone portable execution
- [x] Auto-detection for Claude Code session rate limits
- [x] Continuous loop scheduler until all tasks end
- [x] Native Windows toast notifications on session wake
- [x] Configurable GLM quota and Telegram alert credentials
- [ ] System tray minimization with background auto-wake
- [ ] Sound alert customization when a session resumes
- [ ] Multi-agent concurrent session execution limiter

---

## 🤝 Contributing & Community Welcome

Contributions are warmly welcome! Whether you are:
- Reporting an issue or edge case in session parsing
- Suggesting CLI compatibility improvements
- Submitting a pull request or fixing documentation

Please check out our [CONTRIBUTING.md](CONTRIBUTING.md) guide to get started. Don't forget to give this project a ⭐️ if you find it useful!

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
