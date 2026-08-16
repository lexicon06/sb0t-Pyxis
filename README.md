# sb0t Pyxis

Modern Ares chat server for inbizio web/mobile clients — **.NET 8**, with a cross-platform **Avalonia desktop GUI** and an optional **Blazor Server web panel**.

## Builds / versions

### New system (default branch: `main`)
- **.NET 8** with an **Avalonia desktop GUI** and an optional **Blazor Server web panel** (`sb0t.web`).
- Settings are stored in a JSON file (`SettingsStore`), cross-platform (Windows / Linux / macOS).
- Build: `dotnet build sb0t5.sln`
- Web panel: `dotnet run --project sb0t.web` → http://localhost:8080

### Original WPF GUI (reference)
The original Windows-only WPF GUI is preserved for historical reference in [`gui/legacy_wpf`](gui/legacy_wpf).
- Built with **.NET Framework 4.7.2** + **WPF**, settings stored in the **Windows Registry** (`HKCU\Software\sb0t\...`).
- It is not part of the active build — the new Avalonia GUI replaces it.
- Kept only as a reference for the original UI/behaviors.
