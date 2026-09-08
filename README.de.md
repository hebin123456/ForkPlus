# ForkPlus

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus)](https://github.com/hebin123456/ForkPlus/releases)

Ein plattformübergreifender Git-Client: Die UI-Schicht ist auf .NET 10 + Avalonia 12 gebaut — eine einzige Codebasis läuft auf Windows / Linux / macOS. Die darunterliegende Rust-Engine (biturbo native), KI-gestützte Entwicklung, 8 Sprachen, 12 Theme-Skins, der git mm-Workflow sowie Visualisierungen wie Beitrags-Heatmaps und Repository-Treemaps stehen sofort zur Verfügung.

> Build-Umgebung und Kompilierungsschritte finden Sie in [BUILDING.md](BUILDING.md).

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## Hauptfunktionen

- **Plattformübergreifend**: Auf Avalonia 12 basierende UI-Schicht; die CI erzeugt parallel Builds für Windows x64 / Linux x64 / macOS arm64, veröffentlicht als eigenständige (self-contained) Builds (keine Installation der .NET-Runtime nötig)
- **Mehrsprachigkeit**: 8 integrierte Sprachen (Englisch, vereinfachtes Chinesisch, traditionelles Chinesisch, Japanisch, Koreanisch, Französisch, Deutsch, Spanisch), per JSON-Dateien erweiterbar
- **Mehrere Themes**: 12 eingebaute Skins (Light/Dark, Solarized, GitHub, Dracula, Monokai, Lila/Grün hell & dunkel) sowie benutzerdefinierte Farbüberschreibungen, die sofort angewendet werden
- **git mm-Workflow**: Mitgelieferter `git mm`-Unterbefehl für Lean-Branching-Workflows zur einheitlichen Verwaltung und Synchronisierung von Änderungen über mehrere Sub-Repositories
- **KI-gestützte Entwicklung**: Integrierte KI-Codeüberprüfung, automatische Commit-Nachrichten-Generierung und KI-gestützte Codeänderung
- **Beitrags-Heatmap**: GitHub-artige Commit-Heatmap über 53 Wochen × 7 Tage mit Farbskala-Legende und Statistikzusammenfassung (Gesamtcommits / längste Serie / aktivster Tag); beim Hovern werden Commits des Tages und Top-3-Autoren angezeigt
- **Repository-Treemap**: Visualisierung der Dateigrößen basierend auf dem Treemap-Algorithmus von biturbo native, mit Drilldown per Klick
- **Tracking entfernter Branches**: Der Rechtsklick-Befehl „Tracken“ ist ein zweistufiges, nach Remote gruppiertes Menü mit angedocktem Suchfeld für das schnelle Auffinden unter vielen Remote-Branches
- **Leistungsoptimierungen**: Gezielte Verbesserungen für Aktualisierung großer Repositorys, Diff-Rendering und Submodul-Verwaltung
- **Code-Statistiken**: Integriert tokei (Rust, 200+ Sprachen) für zeilenbasierte Statistiken pro Sprache (Dateien, Code, Kommentare, Leerzeilen) mit Tortendiagramm, unterstützt Workspace/Branch/Tag-Ref-Wechsel

## Repository-Struktur

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # Hauptanwendungsquelle (plattformübergreifende Avalonia-12-UI), XAML, Assets
│   │   ├── Biturbo/           # P/Invoke-Bindungen für die native biturbo-Bibliothek
│   │   ├── Languages/         # Mehrsprachige Übersetzungsdateien (JSON)
│   │   │   ├── zh-Hans.json   # Vereinfachtes Chinesisch
│   │   │   ├── zh-Hant.json   # Traditionelles Chinesisch
│   │   │   ├── ja-JP.json     # Japanisch
│   │   │   ├── ko-KR.json     # Koreanisch
│   │   │   ├── fr-FR.json     # Französisch
│   │   │   ├── de-DE.json     # Deutsch
│   │   │   ├── es-ES.json     # Spanisch
│   │   │   └── README.md      # Beschreibung des Sprachdateiformats
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH-Askpass-Hilfsprogramm
│   ├── ForkPlus.RI/           # Interaktiver Rebase-Editor-Hilfsprogramm
│   ├── ForkPlus.Tests/        # xUnit-Einheitentests (inkl. Avalonia.Headless-UI-Rauchtests)
│   ├── ForkPlus.AskPass.Tests/# Einheitentests des AskPass-Hilfsprogramms
│   └── ForkPlus.RI.Tests/     # Einheitentests des RI-Hilfsprogramms
├── third_party/               # Zur Build-Zeit bezogene native Binärdateien (siehe „Native biturbo-Bibliothek“ unten)
├── gitmm/                     # git mm-Workflow-Referenzdokumentation
└── .github/workflows/         # GitHub Actions CI-Konfiguration
```

## Kompilierung

### Voraussetzungen

- Windows 10 oder höher / Linux / macOS (plattformübergreifend)
- .NET 10 SDK
- IDE (optional): Visual Studio 2026 (Windows; die Lösung lässt sich per `OpenForkPlusInVS2026.cmd` im Repository-Stammverzeichnis mit einem Klick öffnen), Rider oder VS Code
- Git 2.40 oder höher (empfohlen; ältere Versionen lösen beim Start eine Warnung aus und einige Funktionen funktionieren möglicherweise nicht. Die App bevorzugt die mitgelieferte Git-Instanz 2.50.1 und fällt bei fehlender Instanz auf das System-Git zurück)
- git-mm 3.0 oder höher (erforderlich für den git mm-Workflow; Warnung beim Start bei älterer Version; ohne ihn sind die git-mm-Workspace-Funktionen nicht verfügbar; git-mm-Pfad in den Einstellungen konfigurierbar)

### Kompilierungsschritte

```bash
# Kompilieren (im Repository-Stammverzeichnis). Drittanbieter-Artefakte
# (OxyPlot.Avalonia, biturbo, tokei) werden zur Build-Zeit automatisch aus den
# jeweiligen latest Releases bezogen (siehe die „Drittanbieter-Quellen“-Abschnitte
# unten); erforderlich ist nur Netzwerkzugriff auf GitHub:
dotnet build ForkPlus.sln -c Release
```

Alternativ können Sie `ForkPlus.sln` im Repository-Stammverzeichnis direkt mit Visual Studio 2026 öffnen.

### Native biturbo-Bibliothek

Das native biturbo-Add-on (Rust) bietet Treemap-Layout, Commit-Graph-Caching, Revision-Header-Parsing und mehr. **Die Binärdatei wird nicht in dieses Repository eingecheckt**, sondern zur Build-Zeit automatisch aus dem neuesten Release des [Biturbo-Repositorys](https://github.com/hebin123456/Biturbo) bezogen — plattformabhängig:

| Plattform | Datei |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

Mechanismen (siehe [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)):

- `RestoreBiturbo`-Target (`BeforeTargets=Build`): lädt die fehlende native Bibliothek der aktuellen Plattform automatisch herunter (PowerShell unter Windows, bash + curl unter Linux/macOS mit Auswahl von `.so` / `.dylib` per `uname -s`, jeweils mit Wiederholungen)
- `CopyHelperExecutables` / `PublishHelperExecutables`-Targets (`AfterTargets=Build` / `Publish`): kopieren die native Bibliothek und die AskPass/RI-Hilfsprogrammausgaben in die Build-/Publish-Verzeichnisse
- `.gitignore` schließt diese Dateien unter `third_party/` bereits aus

Daher benötigt der erste Build Netzwerkzugriff auf GitHub; in der CI lädt der Workflow sie explizit herunter und prüft sie (nicht leer und > 1 MB), wobei `RestoreBiturbo` im csproj als Rückfallebene dient.

### tokei

[tokei](https://github.com/XAMPPRocky/tokei) (MIT-lizenziert) treibt das Panel „Codezeilen“ an. Zur Build-Zeit wird das **vorkompilierte Binärprogramm** aus dem neuesten Release des Repositorys [hebin123456/tokei](https://github.com/hebin123456/tokei) bezogen — keine lokale Rust-Toolchain nötig:

- Windows x64 → reine exe, gespeichert als `third_party/tokei.exe`
- Linux x64 / macOS → tar.gz (enthält das reine `tokei`-Binärprogramm), entpackt nach `third_party/tokei`
- Das macOS-Artefakt ist x86_64 und läuft auf Apple Silicon über Rosetta 2

Gleicher Mechanismus wie bei biturbo: das `RestoreTokei`-Target (`BeforeTargets=Build`) holt es automatisch, die CI lädt und prüft es explizit, und `.gitignore` schließt die Artefakte aus.

### Quelle für OxyPlot.Avalonia

Die Diagrammbibliothek [OxyPlot.Avalonia](https://github.com/oxyplot/oxyplot-avalonia) (MIT-lizenziert) liefert die Plotting-Steuerelemente des Statistik-Panels. **Das offizielle Repository hängt auf Avalonia 11 fest und veröffentlicht keine Binärdateien**; dieses Repo konsumiert das **vorkompilierte NuGet-Paket** des Forks [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) (nativ gegen Avalonia 12.1.1 kompiliert, Dual-Targeting net8.0 / net10.0):

- Die Paketversionen tragen ein Vorabrelease-Suffix `-avalonia12.x` (z. B. `2.1.2-avalonia12.1`) und kollidieren nie mit dem offiziellen `2.1.2` auf nuget.org (für Avalonia 11); die Wiederherstellung trifft ausschließlich das Fork-Paket
- Zur Buildzeit wird `OxyPlot.Avalonia.<Version>.nupkg` aus der neuesten Release des Forks nach `third_party/nuget/` heruntergeladen (vom `nuget.config` im Repo-Stamm registrierte lokale Verzeichnisquelle) und per `PackageReference` wiederhergestellt
- Abrufmechanismus: `RestoreOxyPlotAvalonia`-Target in ForkPlus.csproj (`BeforeTargets=Restore;Build`, für lokale Entwicklung) plus expliziter CI-Download-Schritt (build.yml, um MSBuild-Exec-Unterschieden auf macOS-Runnern auszuweichen); `.gitignore` schließt die Artefakte aus
- Upgrade = im Fork `AvaloniaVersion` ändern, ein `v*`-Tag setzen und eine neue Release veröffentlichen; dieses Repo ändert nur den einzelnen Punkt `OxyPlotAvaloniaPackageVersion` in `ForkPlus.csproj`

### Kontinuierliche Integration

Das Projekt ist mit GitHub Actions konfiguriert ([`.github/workflows/build.yml`](.github/workflows/build.yml)): Beim Push eines `v*`-Tags oder manuellem Start wird parallel auf drei Plattformen kompiliert, die Artefakte hochgeladen und die vollständige Unit- + E2E-Testsuite auf einem Linux-Runner ausgeführt; nachdem alles bestanden ist, wird automatisch ein Release veröffentlicht (Drei-Plattform-Zip-Anhänge):

| Matrix | Runner | RID |
|--------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

Die Artefakte sind **eigenständige (self-contained) Publishes** (die .NET-10-Runtime ist gebündelt; auf dem Zielrechner muss nichts installiert werden) und enthalten die Haupt-App, die AskPass/RI-Hilfsprogramme (ebenfalls eigenständig, damit die Git-Credential- und Interactive-Rebase-Abläufe auch ohne Runtime funktionieren), die biturbo-Nativbibliothek der Plattform, tokei und die Sprachdateien (linux-x64 ca. 125 MB). Die Zip-Anhänge der Releases finden Sie auf der [Releases-Seite](https://github.com/hebin123456/ForkPlus/releases); Artefakte manueller (ungetaggter) Builds können über den entsprechenden Lauf auf der [Actions](https://github.com/hebin123456/ForkPlus/actions)-Seite heruntergeladen werden (Artifacts, 14 Tage aufbewahrt).

## Tests

- Einheitentests: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj` (inkl. plattformübergreifender Avalonia.Headless-UI-Rauch- und End-to-End-Tests, zusammen mit den Einheitentests)
- Insgesamt 4400+ Fälle; jeder wichtige Fix hat eine Regressionssicherung (schlägt sie fehl, werden die Tests rot)
- Die CI führt bei Tag-Builds und manuellem Start auf einem Ubuntu-Runner die vollständige Suite (inkl. der Tests der AskPass-/RI-Hilfsprogramme) aus; sie hängt von gitflow-avh und git-lfs ab (siehe die Workflow-Kommentare)

## Mehrsprachigkeitsunterstützung

### Integrierte Sprachen

| Sprachcode | Anzeigename | Status |
|------------|-------------|--------|
| `en` | English | Quellsprache |
| `zh-Hans` | 简体中文 | Vollständig |
| `zh-Hant` | 繁體中文 | Vollständig |
| `ja-JP` | 日本語 | Vollständig |
| `ko-KR` | 한국어 | Vollständig |
| `fr-FR` | Français | Vollständig |
| `de-DE` | Deutsch | Vollständig |
| `es-ES` | Español | Vollständig |

### Hinzufügen einer neuen Sprache

Erstellen Sie eine neue `<sprachcode>.json`-Datei im Verzeichnis `src/ForkPlus/Languages/` — keine Codeänderungen erforderlich. Dateiformat:

```json
{
  "code": "ko",
  "name": "한국어",
  "translations": {
    "Preferences": "환경설정",
    "General": "일반",
    "Commit": "커밋"
  }
}
```

Siehe [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md) für Details.

### Internationalisierungs-API

Die Codebasis verwendet die folgenden APIs für die Internationalisierung:

- `PreferencesLocalization.Current("English text")` — Einfache Zeichenfolgenübersetzung
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — Parametrisierte Zeichenfolgenübersetzung
- `PreferencesLocalization.Translate(text, language)` — Übersetzung für eine bestimmte Sprache

## Download

- CI-Build-Artefakte (manuelle, ungetaggte Läufe): [Actions](https://github.com/hebin123456/ForkPlus/actions)-Seite → der entsprechende Build-Lauf → Artifacts (eigenständig, .NET-10-Runtime gebündelt, nichts zu installieren)
- Offizielle Releases: [Releases-Seite](https://github.com/hebin123456/ForkPlus/releases) (eigenständig, .NET-10-Runtime gebündelt, nichts zu installieren, Drei-Plattform-Zips)
- Die Änderungen der einzelnen Versionen finden Sie in den [Release Notes](RELEASE_NOTE.md) (inkl. der Geschichte der WPF-Edition)

## Entwicklungskonventionen

- Beim Ändern der Anwendung selbst bleiben Sie im Verzeichnis `src/ForkPlus`; Laufzeit-Binärdateien unter `third_party/` (native biturbo-Bibliothek, tokei) werden zur Build-Zeit automatisch bezogen — Binärdateien nicht manuell einchecken
- Zum Upgrade von biturbo / tokei genügt ein neues Release im jeweiligen Repository; der nächste Build dieses Repositorys holt es automatisch
- OxyPlot.Avalonia wird als vorkompiliertes nupkg aus dem Fork [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) konsumiert (kompiliert gegen Avalonia 12.1.1; das offizielle Paket steckt auf Avalonia 11 fest und sein XAML IL wirft zur Laufzeit auf 12 eine MissingMethodException), zur Build-Zeit aus dem latest Release dieses Forks bezogen

## Lizenz

Dieses Projekt ist Open Source unter der [MIT-Lizenz](LICENSE).

Copyright (c) 2026 hebin123456
