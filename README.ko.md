# ForkPlus

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus)](https://github.com/hebin123456/ForkPlus/releases)

크로스 플랫폼 Git 클라이언트: UI 레이어는 .NET 10 + Avalonia 12 기반으로 구축되어 단일 코드베이스가 Windows / Linux / macOS에서 실행됩니다. 기반 Rust 엔진(biturbo native), AI 지원 개발, 8개 언어, 12종 테마 스킨, git mm 워크플로우, 기여 히트맵·저장소 트리맵 등 시각화 기능을 바로 사용할 수 있습니다.

> 빌드 환경과 컴파일 단계는 [BUILDING.md](BUILDING.md)를 참조하세요.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 주요 기능

- **크로스 플랫폼**: Avalonia 12 기반 크로스 플랫폼 UI 레이어. CI에서 Windows x64 / Linux x64 / macOS arm64 3개 플랫폼 빌드를 병렬 생성하고, 자체 포함(self-contained) 방식으로 게시(.NET 런타임 설치 불필요)
- **다국어 지원**: 영어, 간체 중국어, 번체 중국어, 일본어, 한국어, 프랑스어, 독일어, 스페인어 8개 언어 내장, JSON 파일로 추가 언어 확장 지원
- **다중 테마 스킨**: 12개 내장 스킨(Light/Dark, Solarized, GitHub, Dracula, Monokai, 보라/초록 라이트&다크)과 사용자 정의 색상 덮어쓰기를 즉시 적용
- **git mm 워크플로우**: `git mm` 하위 명령을 내장하여 린 브랜칭(Lean Branching) 워크플로우로 여러 서브 저장소의 변경과 동기를 통합 관리
- **AI 보조 개발**: AI 코드 리뷰, 자동 커밋 메시지 생성, AI 보조 코드 수정 통합
- **기여 히트맵**: GitHub 스타일 53주 × 7일 커밋 히트맵. 색상 범례와 통계 요약(총 커밋 수 / 최장 연속 커밋 일수 / 가장 활발한 날) 표시, 마우스 호버 시 해당 일의 커밋 수와 Top 3 작성자 표시
- **저장소 트리맵**: biturbo native treemap 알고리즘 기반 저장소 파일 크기 시각화, 클릭으로 단계별 드릴다운 지원
- **원격 브랜치 추적**: 우클릭 "추적"을 원격별로 그룹화한 2단계 메뉴로 변경. 메뉴 내 상단 고정 검색 상자로 수많은 원격 브랜치를 빠르게 검색
- **성능 최적화**: 대형 저장소의 새로고침, diff 렌더링, 서브모듈 관리에 대한 맞춤형 최적화
- **코드 통계**: tokei(Rust, 200+ 언어 지원) 통합으로 언어별 코드 라인 수·파일 수·주석 행·빈 행을 집계하고 파이 차트로 시각화, Workspace/브랜치/tag ref 전환 지원

## 프로젝트 구조

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # 메인 애플리케이션 소스(Avalonia 12 크로스 플랫폼 UI), XAML, 에셋
│   │   ├── Biturbo/           # biturbo native 라이브러리의 P/Invoke 바인딩
│   │   ├── Languages/         # 다국어 번역 파일(JSON)
│   │   │   ├── zh-Hans.json   # 간체 중국어
│   │   │   ├── zh-Hant.json   # 번체 중국어
│   │   │   ├── ja-JP.json     # 일본어
│   │   │   ├── ko-KR.json     # 한국어
│   │   │   ├── fr-FR.json     # 프랑스어
│   │   │   ├── de-DE.json     # 독일어
│   │   │   ├── es-ES.json     # 스페인어
│   │   │   └── README.md      # 언어 파일 형식 설명
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH 비밀번호 입력 도우미
│   ├── ForkPlus.RI/           # 대화형 rebase 편집기 도우미
│   ├── ForkPlus.Tests/        # xUnit 단위 테스트 (Avalonia.Headless UI 스모크 테스트 포함)
│   ├── ForkPlus.AskPass.Tests/# AskPass 도우미 단위 테스트
│   └── ForkPlus.RI.Tests/     # RI 도우미 단위 테스트
├── third_party/               # 빌드 시 가져오는 네이티브 바이너리(아래 "biturbo native 라이브러리 출처" 참조)
├── gitmm/                     # git mm 워크플로우 참조 문서
└── .github/workflows/         # GitHub Actions CI 설정
```

## 빌드

### 사전 요구 사항

- Windows 10 이상 / Linux / macOS(크로스 플랫폼 지원)
- .NET 10 SDK
- IDE(선택): Visual Studio 2026(Windows. 저장소 루트의 `OpenForkPlusInVS2026.cmd`로 한 번에 솔루션을 열 수 있습니다), Rider 또는 VS Code
- Git 2.40 이상(권장. 권장 버전 미만이면 시작 시 경고가 표시되며 일부 기능이 정상 동작하지 않을 수 있습니다. 앱은 내장 git 2.50.1을 우선 사용하며 없으면 시스템 git으로 대체합니다)
- git-mm 3.0 이상(git mm 워크플로 사용 시 필수. 미지원 버전이나 미설치 시 시작 시 경고 표시, 환경설정에서 git-mm 경로 구성 가능)

### 빌드 단계

```bash
# 빌드(저장소 루트에서). 서드파티 산출물(OxyPlot.Avalonia, biturbo, tokei)은
# 빌드 시 각 저장소의 latest Release에서 자동으로 가져옵니다(아래 "서드파티 소스"
# 참조). GitHub 네트워크 접근만 필요합니다:
dotnet build ForkPlus.sln -c Release
```

저장소 루트의 `ForkPlus.sln`을 Visual Studio 2026으로 직접 열어 빌드할 수도 있습니다.

### biturbo native 라이브러리 출처

biturbo native(Rust)는 저장소 트리맵 레이아웃, 커밋 그래프 캐시, revision header 파싱 등의 기능을 제공합니다. **이 바이너리는 이 저장소에 커밋되지 않으며**, 빌드 시 [Biturbo 저장소](https://github.com/hebin123456/Biturbo)의 최신 Release에서 자동으로 가져옵니다. 플랫폼별 파일:

| 플랫폼 | 파일 |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

구체적인 메커니즘([ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj) 참조):

- `RestoreBiturbo` 타깃(`BeforeTargets=Build`): 현재 플랫폼의 네이티브 라이브러리가 없으면 자동 다운로드(Windows는 PowerShell, Linux/macOS는 bash + curl로 `uname -s`에 따라 `.so` / `.dylib` 선택, 모두 재시도 포함)
- `CopyHelperExecutables` / `PublishHelperExecutables` 타깃(`AfterTargets=Build` / `Publish`): 네이티브 라이브러리와 AskPass/RI 하위 프로세스 산출물을 Build / Publish 출력 디렉터리로 복사
- `.gitignore`가 `third_party/`의 이 파일들을 이미 제외함

따라서 첫 빌드에는 GitHub 네트워크 접근이 필요합니다. CI에서는 workflow가 명시적으로 다운로드하고 검증하며(비어 있지 않고 1MB 초과) csproj의 `RestoreBiturbo`가 대체 수단으로 작동합니다.

### tokei 출처

[tokei](https://github.com/XAMPPRocky/tokei)(MIT 라이선스)는 통계 패널의 "코드 라인 수" 기능에 사용됩니다. 빌드 시 [hebin123456/tokei](https://github.com/hebin123456/tokei) 저장소의 최신 Release에서 **사전 컴파일된 바이너리**를 가져오므로 로컬 Rust 툴체인이 필요 없습니다:

- Windows x64 → 단일 exe, `third_party/tokei.exe`로 저장
- Linux x64 / macOS → tar.gz(단일 `tokei` 바이너리 포함), 압축 해제 후 `third_party/tokei`로 저장
- macOS 에셋은 x86_64이며 Apple Silicon에서는 Rosetta 2를 통해 실행

메커니즘은 biturbo와 동일: `RestoreTokei` 타깃(`BeforeTargets=Build`)이 자동으로 가져오고, CI가 명시적으로 다운로드·검증하며, `.gitignore`가 산출물을 제외합니다.

### OxyPlot.Avalonia 출처

차트 라이브러리 [OxyPlot.Avalonia](https://github.com/oxyplot/oxyplot-avalonia)(MIT 라이선스)는 통계 패널의 플롯 컨트롤에 사용됩니다. **공식 저장소는 Avalonia 11에 머물러 있고 바이너리를 게시하지 않으므로**, 이 저장소는 [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) 포크가 게시하는 **사전 빌드된 NuGet 패키지**를 사용합니다(Avalonia 12.1.1 기준 네이티브 컴파일, net8.0 / net10.0 듀얼 타깃):

- 패키지 버전에는 `-avalonia12.x` 시험판 접미사가 붙어(예: `2.1.2-avalonia12.1`) nuget.org 공식 `2.1.2`(Avalonia 11 대상)와 절대 혼동되지 않으며, 복원은 항상 포크 패키지만 찾습니다
- 빌드 시 포크 저장소의 최신 Release에서 `OxyPlot.Avalonia.<버전>.nupkg`를 `third_party/nuget/`로 다운로드하여(저장소 루트의 `nuget.config`에 등록된 로컬 디렉터리 소스) `PackageReference`로 복원됩니다
- 가져오기 메커니즘: ForkPlus.csproj의 `RestoreOxyPlotAvalonia` 타깃(`BeforeTargets=Restore;Build`, 로컬 개발용) + CI 명시적 다운로드 단계(build.yml, macOS 러너에서의 MSBuild Exec 차이 회피), `.gitignore`가 산출물을 제외합니다
- 업그레이드 = 포크 저장소에서 `AvaloniaVersion`을 변경한 뒤 `v*` 태그로 새 Release를 게시하고, 이 저장소는 `ForkPlus.csproj`의 `OxyPlotAvaloniaPackageVersion` 한 곳만 바꾸면 됩니다

### 지속적 통합

프로젝트는 GitHub Actions([`.github/workflows/build.yml`](.github/workflows/build.yml))로 구성되어 있습니다. `v*` 태그를 push하거나 수동 트리거하면 3개 플랫폼에서 병렬 빌드 후 산출물을 업로드하고, Linux 러너에서 단위 + E2E 테스트 스위트 전체를 실행하며, 모두 통과하면 Release(3개 플랫폼 zip 첨부)를 자동 게시합니다:

| 매트릭스 | Runner | RID |
|------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

산출물은 **자체 포함(self-contained) 게시**(.NET 10 런타임 동봉, 대상 머신에는 아무것도 설치할 필요 없음)로, 메인 앱, AskPass/RI 도우미(역시 자체 포함이라 런타임 없는 환경에서도 git 자격 증명 입력과 인터랙티브 rebase 흐름이 동작합니다), 해당 플랫폼의 biturbo native 라이브러리, tokei, 언어 파일을 포함합니다(linux-x64 기준 약 125MB). 릴리스 버전의 zip 첨부는 [Releases 페이지](https://github.com/hebin123456/ForkPlus/releases)에서, 태그 없는 수동 빌드 산출물은 저장소의 [Actions](https://github.com/hebin123456/ForkPlus/actions) 페이지에서 해당 실행의 Artifacts(14일 보관)로 다운로드할 수 있습니다.

## 테스트

- 단위 테스트: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`(크로스 플랫폼 Avalonia.Headless UI 스모크·E2E 테스트 포함, 단위 테스트와 함께 실행)
- 전체 4400+ 케이스. 주요 수정에는 모두 회귀 방어 테스트가 있습니다(실패하면 테스트가 빨간불이 됩니다)
- CI는 태그 빌드와 수동 트리거 시 ubuntu 러너에서 전체 스위트(AskPass / RI 도우미 테스트 포함)를 실행합니다. gitflow-avh와 git-lfs에 의존합니다(workflow 주석 참조)

## 다국어 지원

### 내장 언어

| 언어 코드 | 표시 이름 | 상태 |
|-----------|-----------|------|
| `en` | English | 소스 언어 |
| `zh-Hans` | 简体中文 | 완전 |
| `zh-Hant` | 繁體中文 | 완전 |
| `ja-JP` | 日本語 | 완전 |
| `ko-KR` | 한국어 | 완전 |
| `fr-FR` | Français | 완전 |
| `de-DE` | Deutsch | 완전 |
| `es-ES` | Español | 완전 |

### 새 언어 추가

`src/ForkPlus/Languages/` 디렉터리에 새 `<언어-코드>.json` 파일을 생성하면 됩니다. 코드 변경이 필요 없습니다. 파일 형식:

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

자세한 내용은 [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md)를 참조하세요.

### 국제화 API

코드베이스는 다음 API를 사용하여 국제화를 구현합니다:

- `PreferencesLocalization.Current("English text")` — 단순 문자열 번역
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — 매개변수가 있는 문자열 번역
- `PreferencesLocalization.Translate(text, language)` — 특정 언어에 대한 번역

## 다운로드

- CI 빌드 산출물(태그 없는 수동 실행): [Actions](https://github.com/hebin123456/ForkPlus/actions) 페이지 → 해당 build 실행 → Artifacts(자체 포함 방식, .NET 10 런타임 동봉, 설치 불필요)
- 정식 릴리스: [Releases 페이지](https://github.com/hebin123456/ForkPlus/releases)(자체 포함 방식, .NET 10 런타임 동봉, 설치 불필요, 3개 플랫폼 zip)
- 각 버전의 변경 사항은 [Release Notes](RELEASE_NOTE.md)를 참조하세요(원본 WPF 버전의 역사 포함)

## 개발 규칙

- 애플리케이션 자체를 수정할 때는 `src/ForkPlus` 디렉터리 내에 머무르세요. `third_party/`의 런타임 바이너리(biturbo native 라이브러리, tokei)는 빌드 시 자동으로 가져오므로 바이너리를 수동으로 커밋하지 마세요
- biturbo / tokei 버전을 업그레이드하려면 해당 저장소에 새 Release를 게시하면 이 저장소의 다음 빌드가 자동으로 가져옵니다
- 차트 라이브러리 OxyPlot.Avalonia는 [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) fork의 사전 컴파일된 nupkg를 소비합니다(Avalonia 12.1.1 기준으로 컴파일; 공식 패키지는 Avalonia 11에 머물러 있으며 그 XAML IL은 12 런타임에서 MissingMethodException을 발생시킵니다). 빌드 시 해당 fork의 latest Release에서 가져옵니다

## 라이선스

이 프로젝트는 [MIT License](LICENSE)로 오픈소스입니다.

Copyright (c) 2026 hebin123456
