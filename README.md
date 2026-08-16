# sb0t Pyxis

Modern Ares chat server for inbizio web/mobile clients — **.NET 8**, with a cross-platform **Avalonia desktop GUI** and an optional **Blazor Server web panel**.

## Builds / versions

### New system (default branch: `master`)
- **.NET 8** with an **Avalonia desktop GUI** and an optional **Blazor Server web panel** (`sb0t.web`).
- Settings are stored in a JSON file (`SettingsStore`), cross-platform (Windows / Linux / macOS).
- Build: `dotnet build sb0t5.sln`
- Web panel: `dotnet run --project sb0t.web` → http://localhost:8080

### Legacy WPF + Windows Registry (branch: `legacy-wpf-registry`)
- **.NET Framework 4.7.2** with the **original WPF GUI**.
- Settings stored in the **Windows Registry** (`HKCU\Software\sb0t\...`).
- Windows only.
- For clients that want the original behavior and do not want the new JSON / cross-platform system.

To use the legacy version:

```
git checkout legacy-wpf-registry
```

Restore NuGet packages first (the project uses `packages.config` / old-style references):

```
nuget restore sb0t5.sln
```

Then open `sb0t5.sln` in Visual Studio and build, or build with MSBuild:

```
msbuild sb0t5.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

Note: this branch is an historical snapshot. The GUI is WPF and requires Windows + .NET Framework 4.7.2 (or later) to build and run. Settings are stored in the Windows Registry.
