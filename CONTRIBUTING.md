# Contributing to Quick Claude Wake

Thank you for your interest in contributing to **Quick Claude Wake**! We appreciate bug reports, feature suggestions, documentation improvements, and code contributions.

---

## 🛠️ Development Setup

1. **Prerequisites**:
   - Windows 10 (version 1809+) or Windows 11
   - [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   - Visual Studio 2022 (v17.8+) with **.NET Desktop Development** workload

2. **Clone & Build**:
   ```powershell
   git clone https://github.com/uponatime2019/QuickClaudeWake.git
   cd QuickClaudeWake
   dotnet build QuickClaudeWake.csproj -p:Platform=x64
   ```

3. **Run Locally**:
   ```powershell
   dotnet run --project QuickClaudeWake.csproj
   ```

---

## 💡 Submitting Issues

- **Bug Reports**: Please include your Windows OS version, Claude Code CLI version, steps to reproduce, and any relevant log entries from `%LOCALAPPDATA%\QuickClaudeWake\logs\`.
- **Feature Requests**: Describe the problem you are solving, your suggested solution, and why it benefits users.

---

## 🔀 Submitting Pull Requests

1. Fork the repository and create your feature branch: `git checkout -b feature/my-new-feature`
2. Follow C# coding standards (file-scoped namespaces, clean separation of concerns, no hardcoded API tokens or personal paths).
3. Ensure the project builds cleanly without errors: `dotnet build -c Release -p:Platform=x64`
4. Commit your changes and open a Pull Request against the `main` branch.
