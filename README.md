# FreeFlow Studio 🚀

> **Next-Gen AI Voice Dictation, Voiceover QA Validator & Acoustic Prosody Analyzer for Windows.**

![FreeFlow Studio Banner - High Performance Voice AI Suite](assets/logo.png)

[![Build Status](https://github.com/bbimer/whisper-freeflow-studio/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/bbimer/whisper-freeflow-studio/actions/workflows/ci.yml)
[![Latest Release](https://img.shields.io/github/v/release/bbimer/whisper-freeflow-studio?color=success)](https://github.com/bbimer/whisper-freeflow-studio/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Framework: .NET 8 WPF](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4.svg)](https://dotnet.microsoft.com/)
[![UI: Fluent Dark](https://img.shields.io/badge/UI-WPF.Ui%20Fluent%20Dark-0078D4.svg)](https://github.com/lepoco/wpfui)
[![STT: Groq Whisper](https://img.shields.io/badge/STT-Groq%20Whisper--v3--Turbo-f05023.svg)](https://groq.com/)
[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4.svg)](https://microsoft.com)

**FreeFlow Studio** is a high-performance Windows desktop suite engineered for video creators, crypto marketers, podcasters, and AI voice producers. It combines ultra-fast global voice dictation with automated **STT voiceover validation**, **acoustic human-likeness scoring (Prosody Analysis)**, and **batch audio conversion**.

---

## 📑 Table of Contents
- [🚀 Quick Start](#-quick-start)
- [🌟 Key Features](#-key-features)
- [💻 Offline Local Whisper AI](#-offline-local-whisper-ai-zero-vpn--zero-api)
- [🔒 Security & Key Protection](#-security--key-protection)
- [🛡️ Antivirus & SmartScreen Notes](#️-antivirus--smartscreen-notes)
- [🏗️ Architecture & Technology Stack](#️-architecture--technology-stack)
- [🛠️ Build & Installation Guide](#️-build--installation-guide)
- [⚖️ Third-Party Libraries & Licensing](#️-third-party-libraries--licensing)
- [📜 License](#-license)

---

## 🚀 Quick Start

### Option A: Pre-built Binary Release (Recommended)
1. **Download Release**: Head to [GitHub Releases](https://github.com/bbimer/whisper-freeflow-studio/releases) and download `FreeFlowStudio-win-x64.zip`.
2. **Extract & Launch**: Unpack the ZIP archive and run `FreeFlowWin.App.exe`.
3. **Start Dictating**: Hold **[F9]** (or **[Ctrl+Space]**) to speak, release to instantly transcribe and paste into your active window!

### Option B: Run from Source (.NET 8.0 SDK)
```bash
git clone https://github.com/bbimer/whisper-freeflow-studio.git
cd whisper-freeflow-studio
dotnet run --project FreeFlowWin.App/FreeFlowWin.App.csproj -c Release
```

---

## 🌟 Key Features

### 🎙️ 1. Global AI Voice Dictation
- **System-Wide Hotkey Activation**: Trigger dictation instantly from any app using hardware-level keyboard hooks (`GetAsyncKeyState`).
- **Groq Whisper STT Speed**: Powered by `whisper-large-v3-turbo` for near-instant speech recognition with custom domain vocabulary prompts (Crypto, Web3, Subtitles).
- **Auto-Paste**: Direct text injection into active fields, IDEs, text editors, or web browsers.

### 🎯 2. QA Voiceover & STT Script Validator
- **Batch Drag & Drop**: Drop dozens of audio files (`.mp3`, `.wav`, `.m4a`, `.flac`) simultaneously for bulk QA testing.
- **Word-by-Word Diff Matrix**: Visual color-coded token badges displaying exact text matches, mispronunciations, prompter deletions, and extra insertions.
- **Per-File Report Cards**: Dedicated breakdown cards for every audio take to easily compare performance.

![QA Voiceover Validator Interface showing word diff matrix and accuracy report](screenshots/qa%20voice.jpg)

---

### 🧠 3. Acoustic Prosody & AI Naturalness Detector
- **Human-Likeness Score (`NATURALNESS %`)**: Calculates RMS energy variance, micro-pause frequency, and pitch dynamics to distinguish organic human recordings from robotic AI audio.
- **Audience Protection**: Flags robotic monotonic voiceovers before publishing to Tier-1 traffic channels.
- **Actionable AI Feedback**: Recommends exact adjustment parameters for ElevenLabs voice generation.

---

### 🎚️ 4. ElevenLabs Voice Optimization & Syntax Hacks
- **Preset Cheat Sheet**: Recommended stability (35–45%), similarity (75–85%), and style exaggeration settings for realistic institutional voiceovers.
- **One-Click Humanizer**: Automatically injects breath dashes (`—`), pauses (`...`), and organic filler starters (`Look,`, `Well,`) into target scripts.

---

### ⚡ 5. Batch Media & Audio Converter
- **Multi-File Processing**: Convert audio and video files (`.mp3`, `.wav`, `.m4a`, `.flac`, `.mp4`, `.mov`, `.avi`) in parallel.
- **Audio Extraction**: Rip high-fidelity audio streams directly from video footage.
- **Custom DSP Parameters**: Adjustable sample rates (44.1kHz, 48kHz), bitrates (up to 320 kbps), and mono/stereo channels.

![Audio Converter Interface with progress bars and preset selectors](screenshots/audio%20converter.jpg)

---

### 📊 6. Telemetry & Productivity Tracking
- Tracks daily, weekly, and monthly dictated word counts.
- Displays voice-to-typing speedup factor (e.g. `3.8x faster`) and total hours saved.
- **100% Local Storage**: All stats are stored strictly on your local PC (`%AppData%\FreeFlowWindows\stats.json`). See [PRIVACY.md](PRIVACY.md).

![General Settings panel showing productivity statistics and local model controls](screenshots/general.jpg)

---

## 💻 Offline Local Whisper AI (Zero VPN / Zero API)

FreeFlow Studio features a **100% standalone offline transcription engine** powered by `Whisper.net` and `ggml` C++ runtime. 

### Why Use Local Mode?
* 🔒 **Complete Privacy**: Audio never leaves your computer.
* 🌐 **Bypass VPN / Network Blocks**: Zero dependence on cloud APIs or Cloudflare WAF restrictions.
* 💸 **Unlimited & Free**: No API quotas, usage limits, or subscription costs.

### Available GGML Models & SHA256 Integrity Hashes

| Model | File Size | VRAM / RAM | Speed | SHA256 Checksum | Best For |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`ggml-base.bin`** | ~140 MB | ~500 MB | ⚡⚡⚡ Instant | `60ed5bc226b64f19985ea1053e3047b21ac70d5df564c7ebd00b48f07bd546f8` | Quick dictation & low-spec laptops |
| **`ggml-small.bin`** *(Default)* | ~460 MB | ~1.0 GB | ⚡⚡ Very Fast | `1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b` | Balanced daily dictation (Russian & English) |
| **`ggml-large-v3-turbo.bin`** | ~1.5 GB | ~3.0 GB | ⚡ Fast | `5639644d6735a4d651a2d5e381023730e666a0d0a2185b306b4d375084931a54` | Technical terms, accents, & complex scripts |

#### Verification Procedure
The application automatically verifies the **SHA256 checksum** of downloaded model files prior to loading into memory. You can manually verify model file integrity in PowerShell:
```powershell
Get-FileHash "$env:APPDATA\FreeFlowWindows\Models\ggml-small.bin" -Algorithm SHA256
```

---

## 🔒 Security & Key Protection

- **Windows DPAPI Encryption**: Cloud API keys (Groq, ElevenLabs) stored in `%AppData%\FreeFlowWindows\settings.json` are encrypted using Windows Data Protection API (`ProtectedData.Protect` scoped to `CurrentUser`).
- **Environment Variables**: API keys can also be supplied via `GROQ_API_KEY` and `ELEVENLABS_API_KEY` environment variables.
- For full security disclosures and vulnerability reporting policies, see [SECURITY.md](SECURITY.md) and [PRIVACY.md](PRIVACY.md).

---

## 🛡️ Antivirus & SmartScreen Notes

FreeFlow Studio uses low-level Windows API hooks (`SetWindowsHookEx` and `SendInput`) to capture keypresses globally and simulate text pasting into active applications.

> [!NOTE]
> Some antivirus software or Windows SmartScreen may issue a warning for unsigned executables using global hooks. This is a false positive. Official release binaries are signed with a digital code-signing certificate. If compiling locally, you can safely add the build directory to your antivirus exclusions.

---

## 🏗️ Architecture & Technology Stack

- **Framework**: C# / .NET 8 WPF
- **Design System**: WPF.Ui (Fluent Dark Mode theme with native Windows 11 glassmorphism)
- **STT Engine**: Groq Cloud REST API (`whisper-large-v3-turbo`) & Whisper.net (Offline C++ runtime)
- **Audio DSP**: NAudio & MediaFoundation API
- **System Hooks**: Windows P/Invoke (`SetWindowsHookEx`, `SendInput`, `GetAsyncKeyState`)

```
FreeFlowWin.slnx
 ├── FreeFlowWin.App    (WPF Client Application, XAML views & UI controllers)
 ├── FreeFlowWin.Core   (STT API client, Prosody Analyzer, Audio Engine, Settings)
 └── FreeFlowWin.TestApp(Automated test harness for core audio & QA modules)
```

---

## 🛠️ Build & Installation Guide

### Prerequisites
- **Operating System**: Windows 10 (1903+) or Windows 11
- **Runtime**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Building from Source

```bash
# Clone the repository
git clone https://github.com/bbimer/whisper-freeflow-studio.git
cd whisper-freeflow-studio

# Restore and build the solution
dotnet build FreeFlowWin.slnx -c Release

# Publish self-contained executable
dotnet publish FreeFlowWin.App/FreeFlowWin.App.csproj -c Release -r win-x64 --self-contained false -o ./publish
```

---

## ⚖️ Third-Party Libraries & Licensing

FreeFlow Studio incorporates open-source components and cloud API integrations:

| Component | License / Terms | Commercial Usage | Description / Terms Link |
| :--- | :--- | :--- | :--- |
| **Whisper.net** | MIT License | Permitted | .NET binding for whisper.cpp offline engine |
| **WPF.Ui** | MIT License | Permitted | Modern Fluent UI design controls for WPF |
| **NAudio** | MIT License | Permitted | Audio recording, playback & DSP framework |
| **Groq Cloud API** | [Groq Terms](https://groq.com/terms-of-service/) | Subject to API ToS | Fast Cloud Speech-to-Text inference engine |
| **ElevenLabs API** | [ElevenLabs Terms](https://elevenlabs.io/terms) | Subject to Tier ToS | Voice generation preset & prosody optimization |

---

## 📜 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.
