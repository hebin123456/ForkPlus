# ForkPlus

Un cliente GUI Git de alto rendimiento con motor subyacente reescrito en Rust, que integra desarrollo asistido por IA, 8 idiomas, 12 temas, flujo de trabajo git mm y visualizaciones como mapas de calor de contribuciones y treemaps de repositorio.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | Español

## Características principales

- **Soporte multilingüe**: incluye inglés, 简体中文, 繁體中文, 日本語, y permite añadir más idiomas mediante archivos JSON
- **Múltiples temas**: 12 pieles integradas (Light/Dark, Solarized, GitHub, Dracula, Monokai, Púrpura/Verde claro y oscuro) más anulaciones de color personalizadas aplicadas al instante
- **Flujo de trabajo git mm**: integra el subcomando `git mm`, que proporciona el flujo de trabajo Lean Branching
- **Desarrollo asistido por IA**: integra revisión de código con IA, generación automática de mensajes de commit y modificación de código asistida por IA
- **Optimización del rendimiento**: optimización específica para la actualización, renderizado de diff y gestión de submódulos en repositorios grandes
- **Estadísticas de código**: Integra tokei (Rust, 200+ lenguajes) para estadísticas de líneas de código por lenguaje (archivos, comentarios, líneas en blanco) con visualización en gráfico circular, soporta cambio de ref Workspace/rama/tag

## Estructura del proyecto

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # Código fuente principal de la aplicación WPF, XAML, recursos
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
│   ├── ForkPlus.AskPass/      # Programa auxiliar para entrada de contraseñas Git/SSH
│   ├── ForkPlus.RI/           # Programa auxiliar para el editor de rebase interactivo
│   ├── ForkPlus.Tests/        # Pruebas unitarias xUnit
│   └── ForkPlus.AutomationTests/  # Pruebas de humo de UI con FlaUI
├── third_party/               # Herramientas de tiempo de ejecución y binarios nativos distribuidos con la aplicación
├── gitmm/                     # Documentación de referencia del flujo de trabajo git mm
└── .github/workflows/         # Configuración de CI con GitHub Actions
```

## Compilación

### Requisitos del entorno

- Windows 10 o superior
- Visual Studio 2019/2022, o .NET SDK + MSBuild
- .NET Framework 4.7.2 targeting pack
- Git 2.31 o superior (se recomienda 2.40+; las versiones anteriores muestran una advertencia al iniciar y algunas funciones pueden no funcionar)
- git-mm 3.0 o superior (necesario para el flujo de trabajo git mm; se muestra una advertencia al iniciar si la versión es anterior o está ausente; ruta de git-mm.exe configurable en Preferencias)

### Pasos de compilación

- Abra `ForkPlus.sln` con Visual Studio, seleccione la configuración Release y compile
- O ejecute en la línea de comandos: `msbuild ForkPlus.sln /p:Configuration=Release`

### Integración continua

El proyecto está configurado con GitHub Actions ([`.github/workflows/build-windows.yml`](.github/workflows/build-windows.yml)); al crear un tag que comience con `v*` se compila automáticamente en un entorno Windows y se publica un paquete zip completo del runtime en GitHub Release.

```bash
git tag v1.3.0
git push origin v1.3.0
```

El producto de compilación incluye `ForkPlus.exe`, todas las dll dependientes, `biturbo.dll`, los archivos de idioma, etc.; se puede ejecutar simplemente descomprimiéndolo.

## Pruebas

- Pruebas unitarias: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`
- Pruebas de humo de UI: establezca la variable de entorno `FORKPLUS_AUTOMATION_EXE` para que apunte al `ForkPlus.exe` compilado, luego ejecute `dotnet test src/ForkPlus.AutomationTests/ForkPlus.AutomationTests.csproj`

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

Para la última versión, vaya a la [página de Releases](https://github.com/hebin123456/ForkPlus/releases) para descargarla.

Para los cambios de cada versión, consulte las [Release Notes](RELEASE_NOTE.md).

## Convenciones de desarrollo

Al modificar la aplicación en sí, manténgase dentro del directorio `src/ForkPlus`, a menos que desee actualizar deliberadamente los archivos de runtime bajo `third_party`.
