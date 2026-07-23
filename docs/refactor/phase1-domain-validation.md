# 阶段 1：领域层验证

> 状态：**已完成**
> 性质：纯清理，零功能变更
> 前置：阶段 0 完成（`ICredentialService` 等抽象已就位）

## 目标

确认领域层目录 `Git/` / `Biturbo/` / `Accounts/` / `Jobs/` / `Settings/` / `Utils/` 不依赖
`System.Windows.*`，使其成为可在 Avalonia 项目中直接复用的纯 C# 代码。

领域层此前对 WPF 的依赖主要通过两条传递路径：
1. `PreferencesLocalization`（位于 `UI.UserControls.Preferences`，`using System.Windows.*`）
   被领域层直接调用 → 传递依赖 WPF。
2. `WindowsCredentialManager`（Windows CredManager 封装）被 `Accounts/` 层直接调用 → 平台锁定。

阶段 1 引入 `ILocalizationService` 抽象并复用阶段 0 的 `ICredentialService`，把领域层所有
`PreferencesLocalization.*` / `WindowsCredentialManager.*` 调用迁移到 `ServiceLocator.Localization.*`
/ `ServiceLocator.Credential.*`。

## 新增抽象

- `Services/ILocalizationService.cs`（新增）
  - `string Current(string text)` — 按当前 UI 语言翻译
  - `string Translate(string text, string language)` — 按指定语言翻译
  - `string FormatCurrent(string text, params object[] args)` — 翻译并格式化
- `Services/Wpf/WpfLocalizationService.cs`（新增）— 委托到 `PreferencesLocalization` 静态类
- `Services/ServiceLocator.cs`（扩展）— 新增 `Localization` 属性 + `RegisterPlatformServices` 参数 + `Reset` 清理
- `App.xaml.cs`（扩展）— `RegisterPlatformServices` 调用注册 `WpfLocalizationService`

## 迁移清单

### Accounts 层（凭据 + 本地化）

- [x] `Accounts/PrivateAccessTokenAuthentication.cs` — 5 处 `WindowsCredentialManager` → `ServiceLocator.Credential`
- [x] `Accounts/BitbucketOAuthAuthentication.cs` — 9 处 `WindowsCredentialManager` → `ServiceLocator.Credential`
- [x] `Accounts/AiServices/OpenAiService.cs` — 48 处 `PreferencesLocalization` → `ServiceLocator.Localization`
- [x] `Accounts/GitServiceExtensions.cs` — 1 处 `Translate`
- [x] `Accounts/GitLabService.cs` — 6 处 `FormatCurrent`
- [x] `Accounts/GitHubService.cs` — 5 处 `FormatCurrent`
- [x] `Accounts/BitbucketServerService.cs` — 6 处（`Current` + `FormatCurrent`）
- [x] `Accounts/GiteaService.cs` — 6 处 `FormatCurrent`
- [x] `Accounts/BitbucketService.cs` — 4 处（`Current` + `FormatCurrent`）
- [x] `Accounts/NotificationManager.cs` — 3 处（`Current` + `FormatCurrent`）
  - 注：该文件仍 `using CommunityToolkit.WinUI.Notifications`（toast 通知），属 Windows 专用，
    留待阶段 5（平台跨平台化）用 `IToastNotificationService` 抽象处理。

### Git 层（本地化，40 文件）

- [x] `Git/UpstreamStatus.cs`
- [x] `Git/Diff/Presentation/VisualPatch.cs`
- [x] `Git/Commands/*.cs`（38 个命令文件，共 122 处 `PreferencesLocalization` 调用）
  - 含 `PullGitCommand` / `FetchGitCommand` / `PushGitCommand` / `GitCommandError` /
    `ComposeWipCommitsGitCommand` / `RestoreSnapshotGitCommand` / `BenchmarkGitCommand` 等

### Jobs 层

- [x] `Jobs/JobMonitorExtensions.cs` — 1 处 `Current`

### Utils 层

- [x] `Utils/Http/ServiceError.cs` — 2 处 `Current`（`Cancelled` / `RemoteServiceJsonError` 的 `FriendlyMessage`）

### Biturbo / Shell 层

- [x] 扫描确认无 `PreferencesLocalization` / `WindowsCredentialManager` / `using System.Windows` 残留

## 验收结果

```bash
# 以下目录均 0 命中（PreferencesLocalization / WindowsCredentialManager / using System.Windows）
# Accounts/  Git/  Jobs/  Biturbo/  Utils/  Shell/
```

- `using ForkPlus.UI.UserControls.Preferences` 在领域层 0 命中 ✅
- `using System.Windows` 在 `Accounts/` / `Git/` / `Jobs/` / `Biturbo/` / `Utils/` / `Shell/` 0 命中 ✅

## 已知遗留（留待后续阶段）

- `Settings/ForkPlusSettings.cs` 仍有 `using System.Windows` + `using ForkPlus.UI` + `using ForkPlus.UI.Controls`
  - 该文件是全局设置类，与 UI 层耦合较深（可能引用 UI 控件 / 主题类型），属直接 UI 依赖而非传递依赖。
  - grep 未发现明显的 `System.Windows` 类型实际使用，但 `using ForkPlus.UI` / `using ForkPlus.UI.Controls`
    可能涉及真实类型引用，需结合阶段 2（命令层去 WPF）一并拆解。
  - 处理方向：把 ForkPlusSettings 中纯数据/序列化部分与 UI 表现相关属性分离，或将 UI 相关设置移到 ViewModel 层。
- `Accounts/NotificationManager.cs` 的 `CommunityToolkit.WinUI.Notifications` → 阶段 5 用 `IToastNotificationService` 抽象。

## 风险点（已缓解）

- `ServiceLocator.Localization` / `ServiceLocator.Credential` 为静态属性，调用前需确保已注册。
  启动顺序：`App.OnStartup` → `ServiceLocator.Initialize` → `RegisterPlatformServices`（注册全部平台服务）
  → 后续业务代码访问。领域层的 `ServiceError` / `OpenAiService` 等均在运行时（启动后）访问，无 NPE 风险。
- 静态方法（如 `OpenAiService.MatchesCommitMessageRegex`）原本调用静态类 `PreferencesLocalization`，
  现改为 `ServiceLocator.Localization`，运行时（启动后调用）无问题；设计期/单元测试若独立调用需先注册服务。

## 后续阶段衔接

阶段 1 完成后，领域层（除 `Settings/ForkPlusSettings.cs` 的直接 UI 耦合外）可在 Avalonia 项目中
直接引用，不受 UI 框架切换影响。下一阶段（阶段 2）处理命令层 `UI/Commands/` 的去 WPF 化。
