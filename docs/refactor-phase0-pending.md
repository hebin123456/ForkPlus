# ForkPlus WPF → Avalonia 重构：阶段 0 待办清单

> 分支：`master-refactor`
> 创建时间：阶段 0 抽象层补全
> 原则：**先抽象、后迁移**。阶段 0 只补接口 + WPF 实现 + 注册到 ServiceLocator，**不替换任何现有调用点**，保证构建零风险。

---

## 阶段 0 已完成项

### 新增接口（`src/ForkPlus/Services/`）

| 接口 | 文件 | 替换目标 |
|------|------|----------|
| `IMessageBoxService` | `IMessageBoxService.cs` | 48 处 `System.Windows.MessageBox.Show` |
| `IProcessService` | `IProcessService.cs` | `UriExtensions` / `FileHelper` / `OpenFileInDefaultEditorCommand` 中的 `Process.Start` |
| `IFileSystemDialogService` | `IFileSystemDialogService.cs` | `OpenDialog` 静态类（`CommonOpenFileDialog` / `CommonSaveFileDialog`） |
| `ICredentialService` | `ICredentialService.cs` | `WindowsCredentialManager` 静态类（advapi32 Cred* API） |
| `IFileAssociationService` | `IFileAssociationService.cs` | `OpenFileInDefaultEditorCommand` 中的 `Shlwapi.dll!AssocQueryString` |
| `ISystemThemeService` | `ISystemThemeService.cs` | `App.xaml.cs` 中读注册表 `Themes\Personalize` / `DWM\ColorPrevalence` |
| `IAppContext`（扩展） | `IAppContext.cs` | 新增 `GitPath` / `ShellPath` / `BashPath` / `GitMmPath` / `ProcessId` / `Version` / `UserAgent`，收敛 `App.*` 静态属性 |

### 新增 WPF 实现（`src/ForkPlus/Services/Wpf/`）

| 实现类 | 委托目标 |
|--------|----------|
| `WpfMessageBoxService` | `System.Windows.MessageBox.Show` |
| `WpfProcessService` | `System.Diagnostics.Process.Start` + `explorer.exe /select` |
| `WpfFileSystemDialogService` | `ForkPlus.UI.OpenDialog`（`CommonOpenFileDialog`） |
| `WindowsCredentialService` | `WindowsCredentialManager` 静态类 |
| `WindowsFileAssociationService` | `OpenFileInDefaultEditorCommand.AssocQueryString` P/Invoke |
| `WpfSystemThemeService` | `Microsoft.Win32.Registry` + `SystemThemeHelper` |
| `WpfAppContext`（更新） | 新增 7 个属性委托到 `App.*` 静态属性 |

### ServiceLocator 更新

- `ServiceLocator.RegisterPlatformServices(...)`：一次性注册 6 个新服务（全可选参数）
- `ServiceLocator.Reset()`：清理新增属性
- `App.OnStartup`：在 `Initialize` 后调用 `RegisterPlatformServices`

---

## 阶段 0 待办（本轮未做，后续阶段处理）

### 1. 待补接口（推迟到对应阶段）

| 接口 | 推迟到 | 原因 |
|------|--------|------|
| `IIpcService` | 阶段 5 | 现有 `IpcServer` handler 签名 `Action<NamedPipeServerStream>` 与平台强耦合，需先重构 handler 为 `Action<Stream>` |
| `IThemeService` | 阶段 4 | `ReloadThemeDictionary` / `ApplyCustomColors` 操作 WPF `MergedDictionaries`，属 View 层职责，且需改 `App` 私有方法可见性 |
| `IWebViewFactory` / `IMarkdownWebView` | 阶段 3 | WebView2 事件面太大（ContextMenuRequested / WebMessageReceived / NavigationCompleted），需先设计 AI 窗口 ViewModel 再定义接口 |
| `IWindowPlacementService` | 阶段 4 | `SetWindowPlacement` / `GetWindowPlacement` 仅 `MainWindow` 使用，Win32 窗口管理专属 |
| `ISingleInstanceService` | 阶段 5 | 仅 `App.HandleCommandLineArguments` 启动时用，`Process.GetProcessesByName` + named pipe client 跨平台语义不同 |

### 2. 阶段 1：领域层验证（纯清理，零功能变更）

目标：确认 `Git/` / `Biturbo/` / `Accounts/` / `Jobs/` / `Settings/` 不依赖 `System.Windows.*`。

- [ ] `Accounts/PrivateAccessTokenAuthentication.cs` — 4 处 `WindowsCredentialManager` 直接调用 → 迁移到 `ServiceLocator.Credential`
- [ ] `Accounts/BitbucketOAuthAuthentication.cs` — 10 处 `WindowsCredentialManager` 直接调用 → 迁移到 `ServiceLocator.Credential`
- [ ] `Accounts/AiServices/OpenAiService.cs` — 依赖 `PreferencesLocalization.FormatCurrent`（静态本地化）→ 改为通过回调或 `ILocalizationService` 注入
- [ ] 全局 grep `using System.Windows` 在 `Git/` / `Biturbo/` / `Accounts/` / `Jobs/` / `Settings/` 目录下应为 0 命中
- [ ] `Git/GitModule.cs` / `Git/Commands/*.cs` — 确认仅依赖 `System.Diagnostics` / `System.IO`，无 WPF

### 3. 阶段 2：Commands 层去 WPF 化（`src/ForkPlus/UI/Commands/`，160+ 命令）

按调用频率排序的高优先级文件：

- [ ] `OpenFileInDefaultEditorCommand.cs` — `Process.Start` ×2 → `ServiceLocator.Process`；`AssocQueryString` ×1 → `ServiceLocator.FileAssociation`；`Application.Current.ActiveRepositoryUserControl()` ×1 → 通过参数传入
- [ ] `CheckForkSyncCommand.cs` — 4 处 `System.Windows.MessageBox.Show` → `ServiceLocator.MessageBox`
- [ ] `OpenRepositoryInFileExplorerCommand.cs` / `OpenSubmoduleCommand.cs` / `OpenWorktreeCommand.cs` — `Process.Start` → `ServiceLocator.Process`
- [ ] `ShowInitRepositoryWindowCommand.cs` / `OpenRepositoryCommand.cs` / `ApplyPatchCommand.cs` / `SaveFileCommand.cs` / `MoveSubmoduleCommand.cs` / `ShowSaveAsPatchDialogCommand.cs` — `OpenDialog.Select*` → `ServiceLocator.FileSystemDialog`
- [ ] `RunCustomCommandCommand.cs` — `MainWindow.Instance` → 通过参数传入
- [ ] `NewTabCommand.cs` / `CloseActiveTabCommand.cs` / `SelectPreviousTabCommand.cs` / `SelectNextTabCommand.cs` — `Application.Current.MainWindow as MainWindow` → `IWindowManagerService` 扩展
- [ ] `IUICommand.cs` — `KeyGesture`（`System.Windows.Input`）→ 定义平台无关 `IKeyGesture` 接口或改用 string 表示
- [ ] 全局 grep `System.Windows.MessageBox.Show` 在 `UI/Commands/` 下应为 0 命中

### 4. 阶段 3：ViewModel 抽取（最大工作量）

优先级按"耦合严重程度"排序：

- [ ] `MainWindowViewModel` — 从 `MainWindow.xaml.cs` 抽出：TabManager / Toolbar / Manager 字段、快捷键映射、OnDrop 业务
- [ ] `RepositoryUserControlViewModel` — 从 `RepositoryUserControl.xaml.cs` 抽出：`GitModule` / `RepositoryData` / `JobQueue` / `UndoRedoStack` / `TempFileManager` 字段、`FindParentRepositoryName` 逻辑
- [ ] `CommitUserControlViewModel` — 从 `CommitUserControl.xaml.cs` 抽出：`AmendMode` / `FullCommitMessage` / `CommitAndPush` 属性、自动补全 provider、`Dispatcher.CheckAccess` 调度
- [ ] `AiDevelopmentWindowViewModel` — **最复杂**：从 `AiDevelopmentWindow.xaml.cs` 抽出：`_fileChanges` / `_conversationHistory` / `_lastBeforeContents` / `_pendingRequests` / `_streamingMarkdown` 全部状态字段、流式渲染节流逻辑、技能列表加载
- [ ] `AiCodeReviewWindowViewModel` / `AiCommitComposerWindowViewModel` / `AiTextResultWindowViewModel` — 同上模式
- [ ] `CloneWindowViewModel` — 从 `CloneWindow.xaml.cs` 抽出：`IsSubmitAllowed` / `GetCommandPreview`（git clone 命令拼接）
- [ ] `StatisticsUserControlViewModel` — 从 `StatisticsUserControl.xaml.cs` 抽出：`AuthorStatViewModel` / `CodeLineLanguageViewModel`（当前是 nested class）→ 独立 VM
- [ ] `CustomColorsDialogViewModel` — 12 处 `MessageBox.Show` → `ServiceLocator.MessageBox` + 颜色行集合化
- [ ] `MergeConflictUserControlViewModel` / `SideBySideMergeWindowViewModel` — 各 8 处 `MessageBox.Show` + diff 行动态生成 → `ObservableCollection<DiffLineVm>` + `DataTemplateSelector`

### 5. 阶段 4：Avalonia UI 重写

- [ ] 引入 `Avalonia.AvaloniaEdit`（替换 `AvalonEdit`）
- [ ] 引入 `OxyPlot.Avalonia` 或 `ScottPlot.Avalonia`（替换 `OxyPlot.Wpf`）
- [ ] 调研 `Avalonia.WebView` / `CefGlue`（替换 `WebView2`），或改用 `AvaloniaEdit` 渲染 Markdown
- [ ] `CustomWindow` / `ForkPlusDialogWindow` 基类重写（`WindowChrome` → Avalonia chrome）
- [ ] 22 套 `Theme/Generic.*.xaml` → Avalonia Styles 机制重写
- [ ] `pack://application:,,,/ForkPlus;component/...` → `avares://ForkPlus/Assets/...`
- [ ] 176 处 `x:Name` + 事件 → Binding + ICommand（配合阶段 3 的 ViewModel）

### 6. 阶段 5：平台特定功能跨平台化

- [ ] `App.xaml.cs` Win32 P/Invoke：`SetCurrentProcessExplicitAppUserModelID`（shell32）→ 平台条件或删除
- [ ] `App.xaml.cs` 注册表读取 → `ServiceLocator.SystemTheme`（已抽象，需迁移调用点）
- [ ] `App.xaml.cs` IPC `NamedPipeServerStream` → `IIpcService`（需先补接口）
- [ ] `App.xaml.cs` `Process.GetProcessesByName` 单实例检测 → `ISingleInstanceService`（需先补接口）
- [ ] `WindowLocationStateExtensions.cs` `SetWindowPlacement` / `GetWindowPlacement` → `IWindowPlacementService`（需先补接口）
- [ ] `WindowsCredentialManager.cs` → `ICredentialService`（已抽象，需迁移 Accounts/ 调用点）
- [ ] `MouseHelper.cs` / `TouchpadAwareScrollViewer.cs` Win32 → Avalonia 等价物

### 7. 阶段 6：移除 WPF 框架依赖

- [ ] `<UseWPF>true</UseWPF>` → `<UseAvalonia>true</UseAvalonia>`
- [ ] `<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`
- [ ] 移除 NuGet：`Microsoft.Web.WebView2` / `CommunityToolkit.WinUI.Notifications` / `Microsoft-WindowsAPICodePack-Shell`
- [ ] `.github/workflows/build.yml` 开放 Linux/macOS 构建

---

## 构建验证

阶段 0 代码在 **Linux 沙箱无 dotnet SDK**，且 TFM 为 `net10.0-windows10.0.19041.0`（WPF Windows-only），无法在本机 build。

**验证方式**（二选一）：

```powershell
# Windows 本地
dotnet build src/ForkPlus.sln -c Release

# 或推送到 master-refactor 触发 CI（Windows runner）
git push origin master-refactor
```

**阶段 0 变更范围**（纯增量，不改现有调用点）：

- 新增 7 个接口文件（`Services/*.cs`）
- 新增 6 个 WPF 实现文件（`Services/Wpf/*.cs`）
- 修改 3 个现有文件：
  - `Services/IAppContext.cs` — 接口加 7 个成员
  - `Services/Wpf/WpfAppContext.cs` — 实现 7 个新成员
  - `Services/ServiceLocator.cs` — 加 6 个属性 + `RegisterPlatformServices` 方法 + `Reset` 清理
  - `App.xaml.cs` — `OnStartup` 加 1 个 `RegisterPlatformServices` 调用
