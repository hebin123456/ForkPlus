# ForkPlus

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus)](https://github.com/hebin123456/ForkPlus/releases)

Un client Git multiplateforme : la couche UI est construite en .NET 10 + Avalonia 12, et une seule base de code fonctionne sur Windows / Linux / macOS. Le moteur Rust sous-jacent (biturbo native), le développement assisté par IA, les 8 langues, les 12 thèmes, le workflow git mm, ainsi que les visualisations comme les cartes de chaleur de contributions et les treemaps de dépôt sont utilisables immédiatement.

> Pour l'environnement de build et les étapes de compilation, voir [BUILDING.md](BUILDING.md).

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## Fonctionnalités principales

- **Multiplateforme** : couche UI multiplateforme basée sur Avalonia 12 ; la CI produit en parallèle des builds Windows x64 / Linux x64 / macOS arm64, publiés en mode autonome (self-contained, aucune installation du runtime .NET nécessaire)
- **Prise en charge multilingue** : 8 langues intégrées (anglais, chinois simplifié, chinois traditionnel, japonais, coréen, français, allemand, espagnol), extensibles via des fichiers JSON
- **Thèmes multiples** : 12 habillages intégrés (Light/Dark, Solarized, GitHub, Dracula, Monokai, Violet/Vert clair & sombre) plus surcharges de couleurs personnalisées appliquées instantanément
- **Flux de travail git mm** : sous-commande `git mm` intégrée fournissant des flux de travail Lean Branching pour gérer et synchroniser les changements de plusieurs sous-dépôts
- **Développement assisté par IA** : revue de code par IA, génération automatique de messages de commit et modification de code assistée par IA
- **Carte de chaleur des contributions** : carte de chaleur des commits façon GitHub sur 53 semaines × 7 jours, avec légende de l'échelle de couleurs et résumé statistique (total des commits / plus longue série / jour le plus actif) ; le survol affiche le nombre de commits du jour et le top 3 des auteurs
- **Treemap du dépôt** : visualisation de la taille des fichiers du dépôt basée sur l'algorithme treemap de biturbo native, avec exploration par clic
- **Suivi des branches distantes** : l'action « Suivre » du clic droit devient un menu à deux niveaux groupé par distant, avec une boîte de recherche épinglée pour retrouver rapidement une branche parmi de nombreux distants
- **Optimisations des performances** : améliorations ciblées pour l'actualisation des grands dépôts, le rendu des diffs et la gestion des sous-modules
- **Statistiques de code** : intègre tokei (Rust, 200+ langages) pour compter les lignes par langage (fichiers, code, commentaires, lignes vides) avec visualisation en camembert, et prend en charge le changement de ref Workspace/branche/tag

## Structure du dépôt

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # Source de l'application principale (UI multiplateforme Avalonia 12), XAML, assets
│   │   ├── Biturbo/           # Liaisons P/Invoke pour la bibliothèque native biturbo
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
│   ├── ForkPlus.Tests/        # Tests unitaires xUnit (dont tests de fumée UI Avalonia.Headless)
│   ├── ForkPlus.AskPass.Tests/# Tests unitaires de l'assistant AskPass
│   └── ForkPlus.RI.Tests/     # Tests unitaires de l'assistant RI
├── third_party/               # Binaires natifs récupérés à la compilation (voir « Bibliothèque native biturbo » ci-dessous)
├── gitmm/                     # Documentation de référence du flux de travail git mm
└── .github/workflows/         # Configuration CI GitHub Actions
```

## Compilation

### Prérequis

- Windows 10 ou version ultérieure / Linux / macOS (multiplateforme)
- .NET 10 SDK
- IDE (facultatif) : Visual Studio 2026 (Windows ; ouvrez la solution en un clic via `OpenForkPlusInVS2026.cmd` à la racine du dépôt), Rider ou VS Code
- Git 2.40 ou version ultérieure (recommandé ; les versions antérieures déclenchent un avertissement au démarrage et certaines fonctionnalités peuvent dysfonctionner. L'application préfère son instance git intégrée 2.50.1 et revient au git système si absente)
- git-mm 3.0 ou ultérieur (requis pour le workflow git mm ; un avertissement s'affiche au démarrage si la version est inférieure ; sans lui, les fonctions d'espace de travail git mm sont indisponibles ; chemin git-mm configurable dans les Préférences)

### Étapes de compilation

```bash
# Compiler (à la racine du dépôt). Les artefacts tiers (OxyPlot.Avalonia, biturbo,
# tokei) sont récupérés automatiquement à la compilation depuis les dernières
# releases de leurs dépôts (voir les sections « sources tierces » ci-dessous) ;
# seul un accès réseau à GitHub est nécessaire :
dotnet build ForkPlus.sln -c Release
```

Vous pouvez aussi ouvrir directement `ForkPlus.sln` à la racine du dépôt avec Visual Studio 2026.

### Bibliothèque native biturbo

Le composant natif biturbo (Rust) fournit la mise en page du treemap du dépôt, le cache du graphe de commits, l'analyse des en-têtes de révision, etc. **Ce binaire n'est pas commis dans ce dépôt** ; il est récupéré à la compilation depuis la dernière version du [dépôt Biturbo](https://github.com/hebin123456/Biturbo), avec sélection du fichier selon la plateforme :

| Plateforme | Fichier |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

Mécanismes (voir [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)) :

- Cible `RestoreBiturbo` (`BeforeTargets=Build`) : télécharge automatiquement la bibliothèque native manquante de la plateforme courante (PowerShell sous Windows, bash + curl sous Linux/macOS en choisissant `.so` / `.dylib` selon `uname -s`, avec reprises)
- Cibles `CopyHelperExecutables` / `PublishHelperExecutables` (`AfterTargets=Build` / `Publish`) : copient la bibliothèque native et les sorties des sous-processus AskPass/RI vers les répertoires Build / Publish
- `.gitignore` exclut déjà ces fichiers sous `third_party/`

La première compilation nécessite donc un accès réseau à GitHub ; sur la CI, le workflow les télécharge et les vérifie explicitement (non vide et > 1 Mo), la cible `RestoreBiturbo` du csproj servant de solution de repli.

### tokei

[tokei](https://github.com/XAMPPRocky/tokei) (sous licence MIT) alimente le panneau « lignes de code ». À la compilation, le **binaire précompilé** est récupéré depuis la dernière version du dépôt [hebin123456/tokei](https://github.com/hebin123456/tokei), sans chaîne d'outils Rust locale :

- Windows x64 → exe seul, enregistré sous `third_party/tokei.exe`
- Linux x64 / macOS → tar.gz (contenant le binaire `tokei` seul), extrait vers `third_party/tokei`
- L'artefact macOS est x86_64 et s'exécute sur Apple Silicon via Rosetta 2

Même mécanisme que biturbo : la cible `RestoreTokei` (`BeforeTargets=Build`) récupère automatiquement, la CI télécharge et vérifie explicitement, et `.gitignore` exclut les artefacts.

### Source d'OxyPlot.Avalonia

La bibliothèque de graphiques [OxyPlot.Avalonia](https://github.com/oxyplot/oxyplot-avalonia) (sous licence MIT) alimente les contrôles de tracé du panneau de statistiques. **Le dépôt officiel est resté sur Avalonia 11 et ne publie aucun binaire** ; ce dépôt consomme donc le **paquet NuGet précompilé** publié par le fork [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) (compilé nativement pour Avalonia 12.1.1, double ciblage net8.0 / net10.0) :

- Les versions du paquet portent un suffixe de préversion `-avalonia12.x` (ex. `2.1.2-avalonia12.1`) et ne peuvent jamais être confondues avec le `2.1.2` officiel de nuget.org (destiné à Avalonia 11) ; la restauration ne peut toucher que le paquet du fork
- Au moment du build, `OxyPlot.Avalonia.<version>.nupkg` est téléchargé depuis la dernière Release du fork vers `third_party/nuget/` (source de répertoire local enregistrée par le `nuget.config` à la racine du dépôt) et restauré via `PackageReference`
- Mécanisme de récupération : cible `RestoreOxyPlotAvalonia` de ForkPlus.csproj (`BeforeTargets=Restore;Build`, pour le développement local) plus une étape de téléchargement explicite dans la CI (build.yml, pour contourner les différences de MSBuild Exec sur les runners macOS) ; `.gitignore` exclut les artefacts
- Mise à niveau = modifier `AvaloniaVersion` dans le fork, publier un tag `v*` et une nouvelle Release ; ce dépôt ne change que le point unique `OxyPlotAvaloniaPackageVersion` dans `ForkPlus.csproj`

### Intégration continue

Le projet est configuré avec GitHub Actions ([`.github/workflows/build.yml`](.github/workflows/build.yml)) : au push d'un tag `v*` ou déclenchement manuel, il compile en parallèle sur trois plateformes, téléverse les artefacts et exécute la suite complète de tests unitaires + E2E sur un runner Linux ; une fois tout passé, il publie automatiquement une Release (zip des trois plateformes en pièces jointes) :

| Matrice | Runner | RID |
|--------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

Les artefacts sont des publications **autonomes (self-contained)** (le runtime .NET 10 est intégré ; rien à installer sur le poste cible) et contiennent l'application principale, les assistants AskPass/RI (eux aussi autonomes, afin que les flux de credentials git et de rebase interactif fonctionnent sans runtime), la bibliothèque native biturbo de la plateforme, tokei et les fichiers de langue (~125 Mo pour linux-x64). Les zip des versions publiées sont sur la [page Releases](https://github.com/hebin123456/ForkPlus/releases) ; les artefacts des builds manuels (sans tag) se téléchargent depuis l'exécution correspondante sur la page [Actions](https://github.com/hebin123456/ForkPlus/actions) (Artifacts, conservés 14 jours).

## Tests

- Tests unitaires : `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj` (dont tests de fumée et de bout en bout Avalonia.Headless, multiplateformes, exécutés avec les tests unitaires)
- Plus de 4400 cas au total ; chaque correctif clé dispose d'une garde de régression (une garde qui échoue fait passer les tests au rouge)
- La CI exécute la suite complète (y compris les tests des assistants AskPass/RI) sur un runner Ubuntu lors des builds de tag et des déclenchements manuels ; elle dépend de gitflow-avh et git-lfs (voir les commentaires du workflow)

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

- Artefacts de build CI (exécutions manuelles sans tag) : page [Actions](https://github.com/hebin123456/ForkPlus/actions) → l'exécution build correspondante → Artifacts (autonomes, runtime .NET 10 intégré, rien à installer)
- Versions officielles : [page Releases](https://github.com/hebin123456/ForkPlus/releases) (autonomes, runtime .NET 10 intégré, rien à installer, zip des trois plateformes)
- Pour les modifications de chaque version, consultez les [Release Notes](RELEASE_NOTE.md) (y compris l'historique de l'édition WPF)

## Conventions de développement

- Lors de la modification de l'application elle-même, restez dans le répertoire `src/ForkPlus` ; les binaires d'exécution sous `third_party/` (bibliothèque native biturbo, tokei) sont récupérés automatiquement à la compilation — ne commettez pas de binaires manuellement
- Pour mettre à niveau biturbo / tokei, publiez une nouvelle version dans le dépôt correspondant ; la prochaine compilation de ce dépôt la récupérera automatiquement
- OxyPlot.Avalonia est consommé comme nupkg précompilé du fork [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) (compilé pour Avalonia 12.1.1 ; le paquet officiel est bloqué sur Avalonia 11 et son XAML IL lève une MissingMethodException sur 12 à l'exécution), récupéré à la compilation depuis la dernière release de ce fork

## Licence

Ce projet est open source sous la [licence MIT](LICENSE).

Copyright (c) 2026 hebin123456
