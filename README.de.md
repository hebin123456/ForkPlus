# ForkPlus

Ein erweitertes grafisches Git-Werkzeug basierend auf Fork, mit in Rust geschriebenen zugrundeliegenden Fähigkeiten, Mehrsprachigkeitsunterstützung, git mm- und git repo-Workflows, optimierter Leistung für große Repositorys und zahlreichen Fehlerbehebungen.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## Hauptfunktionen

- **Mehrsprachigkeitsunterstützung**: Integriertes Englisch, vereinfachtes Chinesisch, traditionelles Chinesisch und Japanisch, mit JSON-basierter Erweiterbarkeit für weitere Sprachen
- **git mm-Workflow**: Mitgelieferte `git mm`-Unterbefehle bereitstellen Lean Branching-Workflows
- **KI-gestützte Entwicklung**: Integrierte KI-Codeüberprüfung, automatische Commit-Nachrichten-Generierung und KI-gestützte Codeänderung
- **Leistungsoptimierungen**: Gezielte Verbesserungen für Aktualisierung großer Repositorys, Diff-Rendering und Submodul-Verwaltung
- **Fehlerbehebungen**: Behebung mehrerer Probleme des ursprünglichen Fork (leere Änderungen, KI-Warteschlangen-Generierungsfehler usw.)

## Repository-Struktur

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # Haupt-WPF-Anwendungsquelle, XAML, Assets
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
│   ├── ForkPlus.Tests/        # xUnit-Einheitentests
│   └── ForkPlus.AutomationTests/  # FlaUI-UI-Rauchtests
├── third_party/               # Laufzeitwerkzeuge und native Binärdateien
├── gitmm/                     # git mm-Workflow-Referenzdokumentation
└── .github/workflows/         # GitHub Actions CI-Konfiguration
```

## Kompilierung

### Voraussetzungen

- Windows 10 oder höher
- Visual Studio 2019/2022, oder .NET SDK + MSBuild
- .NET Framework 4.7.2-Zielpaket
- Git 2.31 oder höher (2.40+ empfohlen; ältere Versionen lösen beim Start eine Warnung aus und einige Funktionen funktionieren möglicherweise nicht)
- git-mm 3.0 oder höher (erforderlich für git mm-Workflow; Warnung beim Start bei älterer Version oder wenn fehlend; git-mm.exe-Pfad in den Einstellungen konfigurierbar)

### Kompilierungsschritte

- `ForkPlus.sln` in Visual Studio öffnen, Release-Konfiguration auswählen und kompilieren
- Oder über die Befehlszeile: `msbuild ForkPlus.sln /p:Configuration=Release`

### Kontinuierliche Integration

Das Projekt ist mit GitHub Actions konfiguriert ([`.github/workflows/build-windows.yml`](.github/workflows/build-windows.yml)). Durch Pushen eines `v*`-Tags wird automatisch unter Windows kompiliert und ein vollständiges Laufzeit-Zip wird auf GitHub Release veröffentlicht.

```bash
git tag v1.3.0
git push origin v1.3.0
```

Das Kompilierungsartefakt enthält `ForkPlus.exe`, alle Abhängigkeits-DLLs, `biturbo.dll`, Sprachdateien und mehr — einfach entpacken und ausführen.

## Tests

- Einheitentests: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`
- UI-Rauchtests: Umgebungsvariable `FORKPLUS_AUTOMATION_EXE` auf eine kompilierte `ForkPlus.exe` setzen, dann `dotnet test src/ForkPlus.AutomationTests/ForkPlus.AutomationTests.csproj` ausführen

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

Für die neueste Version besuchen Sie die [Releases-Seite](https://github.com/hebin123456/ForkPlus/releases).

Die Änderungen der einzelnen Versionen finden Sie in den [Release Notes](RELEASENOTES.md).

## Entwicklungskonvention

Beim Ändern der Anwendung selbst bleiben Sie im Verzeichnis `src/ForkPlus`, es sei denn, Sie aktualisieren absichtlich die Laufzeitdateien unter `third_party`.
