# Security Policy

## Supported Versions

We provide security updates for the following versions of **FreeFlow Studio**:

| Version | Supported          |
| ------- | ------------------ |
| 1.6.x   | :white_check_mark: |
| 1.5.x   | :white_check_mark: |
| < 1.5.0 | :x:                |

---

## Response SLA & Vulnerability Handling

We take security issues seriously. When a vulnerability report is received:

- **Initial Response SLA**: Within **24 hours** of submission.
- **Triage & Impact Assessment**: Within **48 hours**.
- **Fix & Patch Target**: Critical vulnerabilities patched within **7 business days**.

---

## Key Management & Confidentiality

FreeFlow Studio communicates with third-party APIs (e.g. Groq Cloud, ElevenLabs) and stores configuration data locally.

- **Local Storage Security**: Sensitive configurations and API keys are stored in `%AppData%\FreeFlowWindows\settings.json`. Keys are protected using Windows Data Protection API (**DPAPI**) via `ProtectedData.Protect` / `Unprotect` scoped to `DataProtectionScope.CurrentUser`.
- **Environment Variables**: Alternatively, keys can be injected safely in CI/CD or local test environments via standard environment variables:
  - `GROQ_API_KEY`: API key for Groq Cloud Whisper transcription.
  - `ELEVENLABS_API_KEY`: API key for ElevenLabs voice generation.
- **Model Checksums**: Downloaded GGML Whisper binaries are verified against known **SHA256** checksums before execution to prevent tamper or corrupt execution.
- **Privacy Boundary**: Full privacy & local storage details are documented in [PRIVACY.md](PRIVACY.md).

---

## Antivirus & Hooking Advisories (`SetWindowsHookEx` & `SendInput`)

FreeFlow Studio uses standard Windows P/Invoke calls:
1. `SetWindowsHookEx` for low-level global hotkey detection.
2. `SendInput` / `keybd_event` for simulated text pasting (`Ctrl+V`) into active text fields.

Some third-party Antivirus or Windows SmartScreen suites may flag unsigned executables utilizing global hooks as false positives.
- **Code Signing**: Official release packages are signed with a digital code-signing certificate when published.
- **Self-Building**: If building from source, ensure your local build output path is excluded from real-time AV scanning if blocked.

---

## Reporting a Vulnerability

If you discover a security vulnerability in FreeFlow Studio, please report it privately:

1. **Do NOT** open a public GitHub issue for security vulnerabilities.
2. Send an email describing the issue, impact, and steps to reproduce to `security@freeflow-studio.org` (or report via GitHub Private Vulnerability Reporting).
3. If encrypting your report, request our GPG public key at `security@freeflow-studio.org`.
