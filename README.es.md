# ForkPlus

Una versión mejorada de la herramienta gráfica de Git basada en Fork, reescrita en Rust en su capa subyacente, con soporte multilingüe añadido, flujos de trabajo git mm y git repo, optimización del rendimiento para repositorios con grandes volúmenes de datos, y corrección de numerosos defectos.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | Español

## Características principales

- **Soporte multilingüe**: incluye inglés, 简体中文, 繁體中文, 日本語, y permite añadir más idiomas mediante archivos JSON
- **Flujo de trabajo git mm**: integra el subcomando `git mm`, que proporciona el flujo de trabajo Lean Branching
- **Desarrollo asistido por IA**: integra revisión de código con IA, generación automática de mensajes de commit y modificación de código asistida por IA
- **Optimización del rendimiento**: optimización específica para la actualización, renderizado de diff y gestión de submódulos en repositorios grandes
- **Corrección de defectos**: corrige múltiples problemas de la versión original de Fork (modificaciones vacías, fallos en la generación encolada de IA, etc.)

## Estructura del proyecto

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # Código fuente principal de la aplicación WPF, XAML, recursos
│   │   ├── Languages/         # Archivos de traducción multilingüe (JSON)
│   │   │   ├── zh-Hans.json   # 简体中文
│   │   │   ├── zh-Hant.json   # 繁體中文
│   │   │   ├── ja-JP.json     # 日本語
│   │   │   └── README.md      # Descripción del formato de archivos de idioma
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

### Pasos de compilación

- Abra `ForkPlus.sln` con Visual Studio, seleccione la configuración Release y compile
- O ejecute en la línea de comandos: `msbuild ForkPlus.sln /p:Configuration=Release`

### Integración continua

El proyecto está configurado con GitHub Actions ([`.github/workflows/build-windows.yml`](.github/workflows/build-windows.yml)); al crear un tag que comience con `v*` se compila automáticamente en un entorno Windows y se publica un paquete zip completo del runtime en GitHub Release.

```bash
git tag v1.2.3
git push origin v1.2.3
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

## Convenciones de desarrollo

Al modificar la aplicación en sí, manténgase dentro del directorio `src/ForkPlus`, a menos que desee actualizar deliberadamente los archivos de runtime bajo `third_party`.
