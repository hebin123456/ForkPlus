# ForkPlus

Un outil graphique Git amélioré basé sur Fork, avec des capacités sous-jacentes réécrites en Rust, une prise en charge multilingue, des flux de travail git mm et git repo, des optimisations de performances pour les grands dépôts, et de nombreuses corrections de bugs.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## Fonctionnalités principales

- **Prise en charge multilingue** : Anglais, chinois simplifié, chinois traditionnel et japonais intégrés, avec extensibilité basée sur JSON pour d'autres langues
- **Flux de travail git mm** : Sous-commande `git mm` intégrée fournissant des flux de travail Lean Branching
- **Développement assisté par IA** : Revue de code par IA, génération automatique de messages de commit et modification de code assistée par IA
- **Optimisations des performances** : Améliorations ciblées pour l'actualisation des grands dépôts, le rendu des diffs et la gestion des sous-modules
- **Corrections de bugs** : Résolution de plusieurs problèmes de l'original Fork (modifications vides, échecs de génération de file d'attente IA, etc.)

## Structure du dépôt

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # Source de l'application WPF principale, XAML, assets
│   │   ├── Languages/         # Fichiers de traduction multilingues (JSON)
│   │   │   ├── zh-Hans.json   # Chinois simplifié
│   │   │   ├── zh-Hant.json   # Chinois traditionnel
│   │   │   ├── ja-JP.json     # Japonais
│   │   │   ├── ko-KR.json     # Coréen
│   │   │   ├── fr-FR.json     # Français
│   │   │   ├── de-DE.json     # Allemand
│   │   │   ├── es-ES.json     # Espagnol
│   │   │   └── README.md      # Description du format de fichier de langue
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Assistant askpass Git/SSH
│   ├── ForkPlus.RI/           # Assistant éditeur de rebase interactif
│   ├── ForkPlus.Tests/        # Tests unitaires xUnit
│   └── ForkPlus.AutomationTests/  # Tests de fumée UI FlaUI
├── third_party/               # Outils d'exécution et binaires natifs
├── gitmm/                     # Documentation de référence du flux de travail git mm
└── .github/workflows/         # Configuration CI GitHub Actions
```

## Compilation

### Prérequis

- Windows 10 ou version ultérieure
- Visual Studio 2019/2022, ou .NET SDK + MSBuild
- Pack de ciblage .NET Framework 4.7.2
- Git 2.31 ou version ultérieure (2.40+ recommandé ; les versions antérieures déclenchent un avertissement au démarrage et certaines fonctionnalités peuvent dysfonctionner)

### Étapes de compilation

- Ouvrir `ForkPlus.sln` dans Visual Studio, sélectionner la configuration Release et compiler
- Ou exécuter en ligne de commande : `msbuild ForkPlus.sln /p:Configuration=Release`

### Intégration continue

Le projet est configuré avec GitHub Actions ([`.github/workflows/build-windows.yml`](.github/workflows/build-windows.yml)). Pousser un tag `v*` déclenche automatiquement une compilation sur Windows et publie un zip d'exécution complet vers GitHub Release.

```bash
git tag v1.2.5
git push origin v1.2.5
```

L'artefact de compilation inclut `ForkPlus.exe`, toutes les DLL de dépendance, `biturbo.dll`, les fichiers de langue, etc. — il suffit de décompresser et d'exécuter.

## Tests

- Tests unitaires : `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`
- Tests de fumée UI : définir la variable d'environnement `FORKPLUS_AUTOMATION_EXE` sur un `ForkPlus.exe` compilé, puis exécuter `dotnet test src/ForkPlus.AutomationTests/ForkPlus.AutomationTests.csproj`

## Prise en charge multilingue

### Langues intégrées

| Code de langue | Nom affiché | Statut |
|-----------------|-------------|--------|
| `en` | English | Langue source |
| `zh-Hans` | 简体中文 | Complet |
| `zh-Hant` | 繁體中文 | Complet |
| `ja-JP` | 日本語 | Complet |
| `ko-KR` | 한국어 | Complet |
| `fr-FR` | Français | Complet |
| `de-DE` | Deutsch | Complet |
| `es-ES` | Español | Complet |

### Ajout d'une nouvelle langue

Créez un nouveau fichier `<code-langue>.json` dans le répertoire `src/ForkPlus/Languages/` — aucune modification de code n'est requise. Format de fichier :

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

Voir [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md) pour plus de détails.

### API d'internationalisation

La base de code utilise les API suivantes pour l'internationalisation :

- `PreferencesLocalization.Current("English text")` — Traduction simple de chaîne
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — Traduction de chaîne paramétrée
- `PreferencesLocalization.Translate(text, language)` — Traduction pour une langue spécifique

## Téléchargement

Pour la dernière version, visitez la [page Releases](https://github.com/hebin123456/ForkPlus/releases).

## Convention de développement

Lors de la modification de l'application elle-même, restez dans le répertoire `src/ForkPlus`, sauf si vous mettez intentionnellement à jour les fichiers d'exécution sous `third_party`.
