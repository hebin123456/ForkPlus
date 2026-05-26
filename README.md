# ForkPlus

一个名为ForkPlus的Git图形化工具, 用rust重写了Fork工具的底层能力, 增加语言包, 增加git mm和git repo支持, 优化大数据性能, 修复部分bug

This workspace contains the ForkPlus WPF application project in a normal source layout.

The normal entry point for day-to-day development is:

- Solution: `ForkPlus.sln`
- Project: `src/ForkPlus/ForkPlus.csproj`

## Repository Layout

- `src/ForkPlus`: main WPF application source, XAML, assets, and project file
- `src/ForkPlus.AskPass`: Git/SSH askpass helper used by the main app
- `src/ForkPlus.RI`: interactive rebase editor helper used by the main app
- `src/ForkPlus.Tests`: xUnit unit tests for core non-UI logic
- `src/ForkPlus.AutomationTests`: optional FlaUI smoke tests for the built WPF app
- `third_party`: runtime tools and native binaries that are shipped with the app and are not managed by NuGet
- `gitmm`: reference documentation for the bundled `git mm` workflows

Regular third-party .NET libraries are referenced through `PackageReference` in `src/ForkPlus/ForkPlus.csproj`.

## Build

- Open `ForkPlus.sln` in Visual Studio
- Or run `dotnet build ForkPlus.sln`
- Target framework: `.NET Framework 4.7.2`

## Tests

- Unit tests: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`
- UI smoke tests: set `FORKPLUS_AUTOMATION_EXE` to a built `ForkPlus.exe`, then run `dotnet test src/ForkPlus.AutomationTests/ForkPlus.AutomationTests.csproj`
- Source coverage gate: `SourceFileCoverageManifestTests` fails when a production `.cs` file is not registered in the test manifest.
- Class coverage gate: `ClassCoverageManifestTests` fails when a production class/struct/interface/enum is not registered with an automated case id.
- Feature coverage gate: `FeatureCoverageManifestTests` fails when a feature area is missing an automated case id.

The manifest gates are intentionally strict. They make missing coverage visible immediately, while individual feature cases can be improved from smoke coverage to behavior-focused tests over time.

## Working Convention

If you are changing the application itself, stay inside `src/ForkPlus` unless you are intentionally updating runtime files under `third_party`.
