# ForkPlus

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus)](https://github.com/hebin123456/ForkPlus/releases)

Un cliente Git multiplataforma: la capa de UI está construida en .NET 10 + Avalonia 12, y una única base de código funciona en Windows / Linux / macOS. El motor Rust subyacente (biturbo native), el desarrollo asistido por IA, los 8 idiomas, los 12 temas, el flujo de trabajo git mm y las visualizaciones como los mapas de calor de contribuciones y los treemaps de repositorio están disponibles desde el primer momento.

> Para el entorno de compilación y los pasos de build, consulte [BUILDING.md](BUILDING.md).

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## Características principales

- **Multiplataforma**: capa de UI multiplataforma basada en Avalonia 12; la CI produce en paralelo builds para Windows x64 / Linux x64 / macOS arm64, publicados como autónomos (self-contained, sin necesidad de instalar el runtime de .NET)
- **Soporte multilingüe**: 8 idiomas integrados (inglés, chino simplificado, chino tradicional, japonés, coreano, francés, alemán, español), ampliables con más idiomas mediante archivos JSON
- **Múltiples temas**: 12 pieles integradas (Light/Dark, Solarized, GitHub, Dracula, Monokai, Púrpura/Verde claro y oscuro) más anulaciones de color personalizadas aplicadas al instante
- **Flujo de trabajo git mm**: subcomando `git mm` integrado que proporciona el flujo de trabajo Lean Branching para gestionar y sincronizar cambios de varios subrepositorios
- **Desarrollo asistido por IA**: revisión de código con IA, generación automática de mensajes de commit y modificación de código asistida por IA
- **Mapa de calor de contribuciones**: mapa de calor de commits estilo GitHub de 53 semanas × 7 días, con leyenda de escala de color y resumen estadístico (commits totales / racha más larga / día más activo); al pasar el cursor se muestran los commits del día y el Top 3 de autores
- **Treemap del repositorio**: visualización del tamaño de los archivos del repositorio basada en el algoritmo treemap de biturbo native, con exploración por niveles mediante clics
- **Seguimiento de ramas remotas**: la acción «Seguir» del clic derecho pasa a ser un menú de dos niveles agrupado por remoto, con un cuadro de búsqueda fijo para localizar rápidamente ramas entre muchos remotos
- **Optimización del rendimiento**: optimización específica para la actualización, renderizado de diff y gestión de submódulos en repositorios grandes
- **Estadísticas de código**: integra tokei (Rust, 200+ lenguajes) para estadísticas de líneas de código por lenguaje (archivos, código, comentarios, líneas en blanco) con visualización en gráfico circular, soporta cambio de ref Workspace/rama/tag

## Estructura del proyecto

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # Código fuente de la aplicación principal (UI multiplataforma Avalonia 12), XAML, recursos
│   │   ├── Biturbo/           # Enlaces P/Invoke para la biblioteca nativa biturbo
│   │   ├── Languages/         # Archivos de traducción multilingües (JSON)
│   │   │   ├── zh-Hans.json   # Chino simplificado
│   │   │   ├── zh-Hant.json   # Chino tradicional
│   │   │   ├── ja-JP.json     # Japonés
│   │   │   ├── ko-KR.json     # Coreano
│   │   │   ├── fr-FR.json     # Francés
│   │   │   ├── de-DE.json     # Alemán
│   │   │   ├── es-ES.json     # Español
│   │   │   └── README.md      # Descripción del formato de archivo de idioma
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Programa auxiliar para contraseñas Git/SSH
│   ├── ForkPlus.RI/           # Programa auxiliar para el editor de rebase interactivo
│   ├── ForkPlus.Tests/        # Pruebas unitarias xUnit (incluye pruebas de humo de UI con Avalonia.Headless)
│   ├── ForkPlus.AskPass.Tests/# Pruebas unitarias del auxiliar AskPass
│   └── ForkPlus.RI.Tests/     # Pruebas unitarias del auxiliar RI
├── third_party/               # Binarios nativos obtenidos en tiempo de compilación (véase «Biblioteca nativa biturbo» más abajo)
├── gitmm/                     # Documentación de referencia del flujo de trabajo git mm
└── .github/workflows/         # Configuración de CI con GitHub Actions
```

## Compilación

### Requisitos del entorno

- Windows 10 o superior / Linux / macOS (soporte multiplataforma)
- .NET 10 SDK
- IDE (opcional): Visual Studio 2026 (Windows; puede abrir la solución con un clic mediante `OpenForkPlusInVS2026.cmd` en la raíz del repositorio), Rider o VS Code
- Git 2.40 o superior (recomendado; las versiones anteriores muestran una advertencia al iniciar y algunas funciones pueden no funcionar. La aplicación prefiere su instancia integrada de git 2.50.1 y recurre al git del sistema si no existe)
- git-mm 3.0 o superior (necesario para el flujo de trabajo git mm; se muestra una advertencia al iniciar si la versión es anterior; sin él, las funciones del espacio de trabajo git mm no están disponibles; ruta de git-mm configurable en Preferencias)

### Pasos de compilación

```bash
# Compilar (en la raíz del repositorio). Los artefactos de terceros (OxyPlot.Avalonia,
# biturbo, tokei) se obtienen automáticamente en tiempo de compilación desde las
# últimas releases de sus repositorios (véanse las secciones de «fuentes de terceros»
# más abajo); solo se necesita acceso de red a GitHub:
dotnet build ForkPlus.sln -c Release
```

También puede abrir `ForkPlus.sln` en la raíz del repositorio directamente con Visual Studio 2026.

### Biblioteca nativa biturbo

El componente nativo biturbo (Rust) proporciona el diseño del treemap del repositorio, la caché del grafo de commits, el análisis de cabeceras de revisión y más. **El binario no se sube a este repositorio**, sino que se descarga automáticamente en tiempo de compilación desde la última versión del [repositorio Biturbo](https://github.com/hebin123456/Biturbo), eligiendo el archivo según la plataforma:

| Plataforma | Archivo |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

Mecanismo concreto (véase [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)):

- Target `RestoreBiturbo` (`BeforeTargets=Build`): descarga automáticamente la biblioteca nativa que falte para la plataforma actual (PowerShell en Windows, bash + curl en Linux/macOS eligiendo `.so` / `.dylib` según `uname -s`, con reintentos)
- Targets `CopyHelperExecutables` / `PublishHelperExecutables` (`AfterTargets=Build` / `Publish`): copian la biblioteca nativa y los productos de los subprocesos AskPass/RI a los directorios Build / Publish
- `.gitignore` ya ignora estos archivos bajo `third_party/`

Por eso la primera compilación necesita acceso de red a GitHub; en la CI, el workflow los descarga y verifica explícitamente (no vacío y >1 MB), siendo `RestoreBiturbo` del csproj la red de seguridad.

### tokei

[tokei](https://github.com/XAMPPRocky/tokei) (licencia MIT) alimenta el panel de «líneas de código». En tiempo de compilación se obtiene el **binario precompilado** de la última versión del repositorio [hebin123456/tokei](https://github.com/hebin123456/tokei), sin necesidad de una cadena de herramientas Rust local:

- Windows x64 → exe único, guardado como `third_party/tokei.exe`
- Linux x64 / macOS → tar.gz (contiene el binario `tokei` único), extraído a `third_party/tokei`
- El recurso de macOS es x86_64 y se ejecuta en Apple Silicon mediante Rosetta 2

Mecanismo idéntico a biturbo: el target `RestoreTokei` (`BeforeTargets=Build`) lo obtiene automáticamente, la CI lo descarga y verifica explícitamente, y `.gitignore` ignora los productos.

### Fuente de OxyPlot.Avalonia

La biblioteca de gráficos [OxyPlot.Avalonia](https://github.com/oxyplot/oxyplot-avalonia) (licencia MIT) alimenta los controles de trazado del panel de estadísticas. **El repositorio oficial está estancado en Avalonia 11 y no publica binarios**; este repositorio consume el **paquete NuGet precompilado** publicado por el fork [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) (compilado de forma nativa contra Avalonia 12.1.1, doble objetivo net8.0 / net10.0):

- Las versiones del paquete llevan un sufijo de preliminar `-avalonia12.x` (p. ej. `2.1.2-avalonia12.1`) y nunca chocan con el `2.1.2` oficial de nuget.org (orientado a Avalonia 11); la restauración solo alcanza el paquete del fork
- En tiempo de compilación se descarga `OxyPlot.Avalonia.<versión>.nupkg` de la última Release del fork a `third_party/nuget/` (origen de directorio local registrado por el `nuget.config` de la raíz del repositorio) y se restaura vía `PackageReference`
- Mecanismo de obtención: target `RestoreOxyPlotAvalonia` de ForkPlus.csproj (`BeforeTargets=Restore;Build`, para desarrollo local) más un paso explícito de descarga en la CI (build.yml, para esquivar las diferencias de MSBuild Exec en los runners de macOS); `.gitignore` excluye los artefactos
- Actualizar = cambiar `AvaloniaVersion` en el fork, etiquetar con `v*` y publicar una nueva Release; este repositorio solo cambia el punto único `OxyPlotAvaloniaPackageVersion` de `ForkPlus.csproj`

### Integración continua

El proyecto está configurado con GitHub Actions ([`.github/workflows/build.yml`](.github/workflows/build.yml)): al hacer push de un tag `v*`, o al activarlo manualmente, compila en paralelo en tres plataformas, sube los productos y ejecuta la suite completa de pruebas unitarias + E2E en un runner Linux; cuando todo pasa, publica automáticamente un Release (adjuntos zip de las tres plataformas):

| Matriz | Runner | RID |
|--------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

Los productos son **publicaciones autónomas (self-contained)** (incluyen el runtime de .NET 10; no hay que instalar nada en la máquina destino) e incluyen la aplicación principal, los auxiliares AskPass/RI (también autónomos, de modo que los flujos de credenciales de git y de rebase interactivo funcionan sin runtime), la biblioteca nativa biturbo de la plataforma, tokei y los archivos de idioma (~125 MB para linux-x64). Los adjuntos zip de las versiones publicadas están en la [página de Releases](https://github.com/hebin123456/ForkPlus/releases); los productos de builds manuales (sin tag) se pueden descargar desde la ejecución correspondiente en la página de [Actions](https://github.com/hebin123456/ForkPlus/actions) (Artifacts, se conservan 14 días).

## Pruebas

- Pruebas unitarias: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj` (incluye pruebas de humo y de extremo a extremo de UI con Avalonia.Headless, multiplataforma, se ejecutan junto con las unitarias)
- Más de 4400 casos en total; cada corrección clave cuenta con una barrera de regresión (si falla, las pruebas se ponen en rojo)
- La CI ejecuta la suite completa (incluidas las pruebas de los auxiliares AskPass/RI) en un runner Ubuntu en los builds de tag y activaciones manuales; depende de gitflow-avh y git-lfs (véanse los comentarios del workflow)

## Soporte multilingüe

### Idiomas integrados

| Código de idioma | Nombre para mostrar | Estado |
|---------|---------|------|
| `en` | English | Idioma fuente |
| `zh-Hans` | 简体中文 | Completo |
| `zh-Hant` | 繁體中文 | Completo |
| `ja-JP` | 日本語 | Completo |
| `ko-KR` | 한국어 | Completo |
| `fr-FR` | Français | Completo |
| `de-DE` | Deutsch | Completo |
| `es-ES` | Español | Completo |

### Añadir un nuevo idioma

Cree un archivo `<código-de-idioma>.json` en el directorio `src/ForkPlus/Languages/`; no es necesario modificar el código. Formato del archivo:

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

Consulte [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md) para más detalles.

### API de internacionalización

La internacionalización se implementa en el código mediante las siguientes API:

- `PreferencesLocalization.Current("English text")` — traducción simple de cadenas
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — traducción de cadenas con parámetros
- `PreferencesLocalization.Translate(text, language)` — traducción a un idioma especificado

## Descarga

- Productos de build de la CI (ejecuciones manuales sin tag): página de [Actions](https://github.com/hebin123456/ForkPlus/actions) → la ejecución de build correspondiente → Artifacts (autónomos, incluyen el runtime de .NET 10, nada que instalar)
- Versiones oficiales: [página de Releases](https://github.com/hebin123456/ForkPlus/releases) (autónomos, incluyen el runtime de .NET 10, nada que instalar, zip de las tres plataformas)
- Para los cambios de cada versión, consulte las [Release Notes](RELEASE_NOTE.md) (incluye el historial de la edición WPF)

## Convenciones de desarrollo

- Al modificar la aplicación en sí, manténgase dentro del directorio `src/ForkPlus`; los binarios de tiempo de ejecución bajo `third_party/` (biblioteca nativa biturbo, tokei) se obtienen automáticamente en tiempo de compilación — no suba binarios manualmente
- Para actualizar biturbo / tokei, publique una nueva versión en el repositorio correspondiente; la próxima compilación de este repositorio la obtendrá automáticamente
- OxyPlot.Avalonia se consume como nupkg precompilado del fork [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) (compilado contra Avalonia 12.1.1; el paquete oficial está detenido en Avalonia 11 y su XAML IL lanza MissingMethodException en 12 en tiempo de ejecución), y se obtiene en tiempo de compilación desde la última release de ese fork

## Licencia

Este proyecto es de código abierto bajo la [Licencia MIT](LICENSE).

Copyright (c) 2026 hebin123456
