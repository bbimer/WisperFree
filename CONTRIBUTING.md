# Contributing to FreeFlow Studio

Thank you for your interest in contributing to **FreeFlow Studio**! We welcome contributions, bug reports, feature requests, and documentation improvements.

---

## 🛠️ Development Setup

### Prerequisites
- Windows 10 (1903+) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (with .NET Desktop Development workload) or JetBrains Rider / VS Code.

### Building the Project
```bash
# Clone the repository
git clone https://github.com/bbimer/whisper-freeflow-studio.git
cd whisper-freeflow-studio

# Build solution
dotnet build FreeFlowWin.slnx -c Debug
```

---

## 🧪 Running Tests & Validation

Before submitting a Pull Request, verify that all projects compile cleanly and test suites pass:

```bash
# Build test app
dotnet build FreeFlowWin.TestApp/FreeFlowWin.TestApp.csproj -c Release
```

---

## 📌 Submission Guidelines

1. **Fork & Branch**: Create a feature branch off `main` (e.g. `feature/my-new-feature` or `fix/issue-description`).
2. **Commit Messages**: Write clear, descriptive commit messages describing *what* changed and *why*.
3. **Code Style**:
   - Follow standard .NET C# coding conventions.
   - Use PascalCase for public members and `_camelCase` for private fields.
   - Maintain XML doc comments for core public interfaces in `FreeFlowWin.Core`.
4. **Pull Request**: Open a PR against `main`. Fill in the provided PR template checklist completely.
