# ForkPlus

Rust로 하위 엔진을 재작성한 고성능 Git GUI 클라이언트로, AI 지원 개발, 8개 언어, 12종 테마 스킨, git mm 워크플로우, 기여 히트맵 및 저장소 트리맵 등 시각화 기능을 내장하고 있습니다.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 주요 기능

- **다국어 지원**: 영어, 간체 중국어, 번체 중국어, 일본어를 내장하고, JSON 기반으로 더 많은 언어 확장 지원
- **다중 테마 스킨**: 12개 내장 스킨(Light/Dark, Solarized, GitHub, Dracula, Monokai, 보라/초록 라이트&다크)과 사용자 정의 색상 덮어쓰기를 즉시 적용
- **git mm 워크플로우**: `git mm` 하위 명령을 내장하여 린 브랜칭(Lean Branching) 워크플로우 제공
- **AI 보조 개발**: AI 코드 리뷰, 자동 커밋 메시지 생성, AI 보조 코드 수정 통합
- **성능 최적화**: 대형 저장소의 새로고침, diff 렌더링, 서브모듈 관리에 대한 맞춤형 최적화
- **결함 수정**: 원본 Fork의 여러 문제 수정(빈 수정, AI 큐 생성 실패 등)

## 프로젝트 구조

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # 메인 WPF 애플리케이션 소스, XAML, 에셋
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
│   ├── ForkPlus.Tests/        # xUnit 단위 테스트
│   └── ForkPlus.AutomationTests/  # FlaUI UI 스모크 테스트
├── third_party/               # 앱과 함께 배포되는 런타임 도구 및 네이티브 바이너리
├── gitmm/                     # git mm 워크플로우 참조 문서
└── .github/workflows/         # GitHub Actions CI 설정
```

## 빌드

### 사전 요구 사항

- Windows 10 이상
- Visual Studio 2019/2022, 또는 .NET SDK + MSBuild
- .NET Framework 4.7.2 타겟 팩
- Git 2.31 이상(2.40+ 권장, 미만 버전은 시작 시 경고가 표시되며 일부 기능이 정상적으로 작동하지 않을 수 있습니다)
- git-mm 3.0 이상 (git mm 워크플로 사용 시 필수, 미지원 버전이나 미설치 시 시작 시 경고, 환경설정에서 git-mm.exe 경로 구성 가능)

### 빌드 단계

- Visual Studio에서 `ForkPlus.sln`을 열고 Release 구성을 선택하여 빌드
- 또는 명령줄에서 실행: `msbuild ForkPlus.sln /p:Configuration=Release`

### 지속적 통합

프로젝트는 GitHub Actions([`.github/workflows/build-windows.yml`](.github/workflows/build-windows.yml))로 구성되어 있습니다. `v*`로 시작하는 tag를 푸시하면 Windows 환경에서 자동 빌드되어 완전한 런타임 zip 패키지가 GitHub Release에 게시됩니다.

```bash
git tag v1.3.0
git push origin v1.3.0
```

빌드 산출물에는 `ForkPlus.exe`, 모든 의존 DLL, `biturbo.dll`, 언어 파일 등이 포함되며, 압축을 풀면 바로 실행할 수 있습니다.

## 테스트

- 단위 테스트: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`
- UI 스모크 테스트: `FORKPLUS_AUTOMATION_EXE` 환경 변수를 빌드된 `ForkPlus.exe`로 설정한 후, `dotnet test src/ForkPlus.AutomationTests/ForkPlus.AutomationTests.csproj` 실행

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

`src/ForkPlus/Languages/` 디렉토리에 새 `<언어-코드>.json` 파일을 생성하면 됩니다. 코드 변경이 필요 없습니다. 파일 형식:

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

최신 릴리스는 [Releases 페이지](https://github.com/hebin123456/ForkPlus/releases)에서 다운로드하세요.

각 버전의 변경 사항은 [Release Notes](RELEASE_NOTE.md)를 참조하세요.

## 개발 규칙

애플리케이션 자체를 수정할 때는, `third_party` 하위의 런타임 파일을 의도적으로 업데이트하는 경우가 아니라면 `src/ForkPlus` 디렉토리 내에 머무르세요.
