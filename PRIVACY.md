# Privacy Policy & Telemetry Boundary

**FreeFlow Studio** is engineered with a privacy-first architecture. This document explains how local data is handled, stored, and protected.

---

## 🔒 100% Local Data & Productivity Telemetry

- **No Remote Telemetry or Analytics**: FreeFlow Studio contains **zero** third-party analytics trackers, telemetry SDKs, or background telemetry ping services (e.g. No Google Analytics, Sentry, Mixpanel, or Segment).
- **Local Productivity Statistics**: Productivity metrics (daily/weekly dictated word counts, voice speedup factor, and hours saved) are stored **100% locally** on your device at:
  `%AppData%\FreeFlowWindows\stats.json`
- **Data Retention & Reset**: You can clear or inspect your local telemetry at any time by deleting `%AppData%\FreeFlowWindows\stats.json`.

---

## 🎙️ Audio Data Processing

### Local Mode (Whisper.net)
- When **Local Mode** is enabled, audio recorded from your microphone is processed **entirely offline** on your computer.
- Audio data never leaves your local machine, and no network requests are sent during dictation.

### Cloud API Mode (Groq / ElevenLabs)
- When using **Cloud Mode**, audio samples are transmitted directly to the Groq Cloud API endpoint (`api.groq.com`) using TLS encryption over HTTPS.
- Audio samples are discarded immediately after transcription processing according to Groq's data retention policy.

---

## 🔑 Credential Protection

- API Keys (Groq Cloud, ElevenLabs) are encrypted using **Windows Data Protection API (DPAPI)** before being written to `%AppData%\FreeFlowWindows\settings.json`.
- Encrypted credentials can only be decrypted by your current Windows user account on your local machine.

---

## 📩 Contact & Inquiries

For privacy questions or policy clarification, open an issue on GitHub or contact `privacy@freeflow-studio.org`.
