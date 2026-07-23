# 阶段 2：Commands 层去 WPF 化

> 状态：**已完成（核心目标）**
> 性质：中等风险，逐文件验证
> 前置：阶段 0 完成（抽象服务已就位）、阶段 1 完成（领域层干净）

## 目标

将 `src/ForkPlus/UI/Commands/` 下命令类中的 WPF 直接调用替换为阶段 0/2 抽象的服务接口，
使 Commands 层不再直接依赖 `System.Windows.MessageBox` / `Process.Start` / `OpenDialog` /
`MainWindow.Instance.TabManager` / `Application.Current`（应用级操作部分）。

## 抽象扩展（阶段 2 新增）

### IWindowManagerService 扩展（+8 方法）

承接 Commands 层对 `MainWindow.Instance.TabManager` 与 `Application.Current` 的直接访问：

| 方法 | 替换的原始调用 |
|------|---------------|
| `NewTab()` | `(Application.Current.MainWindow as MainWindow).TabManager.NewTab()` |
| `CloseActiveTab()` | `(...).TabManager.CloseActiveTab()` |
| `SelectPreviousTab()` | `(...).TabManager.SelectPreviousTab()` |
| `SelectNextTab()` | `(...).TabManager.SelectNextTab()` |
| `OpenRepository(string path, GitModule nextTo = null)` | `MainWindow.Instance.TabManager.OpenRepository(...)` |
| `OpenRepositories(string[] paths)` | `Application.Current.TabManager()?.OpenRepositories(...)` |
| `RefreshActiveRepositoryManager()` | `Application.Current.TabManager()?.ActiveRepositoryManager?.Refresh()` |
| `RefreshLayoutScaling()` | `Application.Current.RefreshLayoutScaling()` |
| `CheckForUpdates()` | `MainWindow.Instance?.CheckForUpdates()` |

- `IWindowManagerService.cs` — 接口扩展（新增 using ForkPlus.Git）
- `WpfWindowManagerService.cs` — WPF 实现，全部转发到 `MainWindow.Instance.TabManager` / `Application.Current`

### WindowsFileAssociationService 内化 P/Invoke

原 `OpenFileInDefaultEditorCommand` 中的 `AssocQueryString` P/Invoke + `AssocF`/`AssocStr` 枚举
移入 `WindowsFileAssociationService`（私有），消除 service 对 command 的反向依赖。
`OpenFileInDefaultEditorCommand` 改用 `ServiceLocator.FileAssociation.IsEditorAvailable(extension)`。

## 迁移清单

### A. 直接替换（阶段 0 抽象已就位）

- [x] `CheckForkSyncCommand.cs` — 4 处 `System.Windows.MessageBox.Show` → `ServiceLocator.MessageBox.Show`
  + `PreferencesLocalization` → `ServiceLocator.Localization`
- [x] `OpenFileInDefaultEditorCommand.cs` — 2 处 `Process.Start` → `ServiceLocator.Process.OpenFileInDefaultApplication`
  + `AssocQueryString` P/Invoke → `ServiceLocator.FileAssociation.IsEditorAvailable`
  （注：`SaveToTempDestination` 中 `Application.Current.ActiveRepositoryUserControl()?.TempFileManager` 属 View 类型耦合，留待阶段 3）

### B. OpenDialog → IFileSystemDialogService（去掉 owner 参数）

- [x] `ShowInitRepositoryWindowCommand.cs` — `SelectDirectory` + `OpenRepository`
- [x] `OpenRepositoryCommand.cs`（Commands 根）— `SelectDirectory` + `OpenRepository`
- [x] `ApplyPatchCommand.cs` — `SelectFile`
- [x] `SaveFileCommand.cs` — `SelectFileSaveLocation` + `PreferencesLocalization`
- [x] `ShowSaveAsPatchDialogCommand.cs` — `SelectPatchSaveLocation`
- [x] `MoveSubmoduleCommand.cs` — `SelectDirectory` + `PreferencesLocalization`

### C. Tab 管理 → IWindowManagerService

- [x] `NewTabCommand.cs` / `CloseActiveTabCommand.cs` / `SelectPreviousTabCommand.cs` / `SelectNextTabCommand.cs`
- [x] `OpenWorktreeCommand.cs` / `OpenSubmoduleCommand.cs` — `OpenRepository(path, gitModule)`
- [x] `OpenRepositoriesCommand.cs`（RepositoryManager）— `OpenRepositories`
- [x] `OpenRepositoryCommand.cs`（RepositoryManager）— `OpenRepository` + `RefreshActiveRepositoryManager` + `PreferencesLocalization`

### D. 应用级操作 → IWindowManagerService / IAppContext / IDispatcher

- [x] `ExitApplicationCommand.cs` — `Application.Current.Shutdown(0)` → `ServiceLocator.AppContext.Shutdown()`
- [x] `RescanUserRepositoriesCommand.cs` — `Application.Current.Dispatcher.Async` ×2 → `ServiceLocator.Dispatcher.Async`
- [x] `IncreaseLayoutScaleCommand.cs` / `DecreaseLayoutScaleCommand.cs` — `RefreshLayoutScaling`
- [x] `UpdateApplicationCommand.cs` — `MainWindow.Instance?.CheckForUpdates()` → `ServiceLocator.WindowManager.CheckForUpdates()`

### E. PreferencesLocalization → ServiceLocator.Localization（35 文件，61 处调用）

阶段 1 未覆盖 UI/Commands 的 `PreferencesLocalization` 调用，阶段 2 一并清理。
35 个命令文件的 `Current` / `FormatCurrent` / `Translate` 调用全部迁移到 `ServiceLocator.Localization`。
完整文件清单见提交记录。

## 验收结果

```bash
# Commands 目录下以下模式应为 0 命中（或仅剩明确遗留项）
grep -rn "System.Windows.MessageBox" src/ForkPlus/UI/Commands/   # 0 ✓
grep -rn "Process.Start" src/ForkPlus/UI/Commands/               # 0 ✓
grep -rn "OpenDialog\." src/ForkPlus/UI/Commands/                # 0 ✓
grep -rn "MainWindow.Instance.TabManager" src/ForkPlus/UI/Commands/  # 0 ✓
```

- `System.Windows.MessageBox.Show` — 0 命中 ✅（4 处 → 0）
- `Process.Start` — 0 命中 ✅（2 处 → 0）
- `OpenDialog.` — 0 命中 ✅（9 处 → 0）
- `(Application.Current.MainWindow as MainWindow).TabManager.*` — 0 命中 ✅（4 处 → 0）
- `MainWindow.Instance.TabManager.*` — 0 命中 ✅（6 处 → 0）
- `Application.Current.Dispatcher` / `.Shutdown` / `.RefreshLayoutScaling` / `.TabManager()` — 0 命中 ✅
- `PreferencesLocalization.Current/FormatCurrent/Translate` — 0 命中 ✅（61 处 → 0）

## 已知遗留（留待后续阶段）

### 阶段 3（ViewModel 抽取）处理 — `Application.Current.ActiveRepositoryUserControl()` 返回 View 类型

以下 12 文件 14 处调用获取 `RepositoryUserControl`（View 类型）后调用其方法
（`InvalidateAndRefresh` / `ActivateRevisionView` / `ShowRevisionDetails` / `TempFileManager`），
需 ViewModel 抽取后通过 VM 命令/消息实现：

- `SwitchRevisionListOrientationCommand.cs`
- `ToggleReferenceFilterCommand.cs`
- `CompareRevisionToWorkingDirectoryCommand.cs`
- `UnpinReferenceCommand.cs`
- `ShowPreferencesWindowCommand.cs`（含 `Application.Current.MainWindow is MainWindow`）
- `ToggleShowReflogInRevisionListCommand.cs`
- `OpenFileInDefaultEditorCommand.cs`（TempFileManager 部分）
- `ShowRevisionInSeparateWindowCommand.cs`（`new RevisionDetailsWindow`）
- `ToggleHideTagsCommand.cs`
- `ShowCreateTagWindowCommand.cs`
- `RefreshRepositoryDataCommand.cs`
- `ShowRepositorySettingsWindowCommand.cs`
- `PinReferenceCommand.cs`

### 阶段 3 处理 — MainWindow.Instance 细碎耦合

- `SwitchWorkspaceCommand.cs` — `MainWindow instance = MainWindow.Instance` +
  `instance.TabManager.SaveSession/RestoreSession` + `instance.Toolbar.RefreshWorkspacesButton` +
  `instance.RefreshTitle` + `instance.RefreshRepositoriesStatus`（需 ViewModel）

### 阶段 4（Avalonia UI 重写）处理 — WPF 深度耦合

- `SwitchApplicationThemeCommand.cs` — `Application.Current.Resources.MergedDictionaries`（WPF ResourceDictionary，3 处）
- `RunCustomCommandCommand.cs` — `runSharedCustomCommandConfirmationWindow.Owner = MainWindow.Instance`（WPF Window.Owner）
- `IUICommandExtension.cs` — `PreferencesLocalization.MenuHeader/FormatMenuHeader`（WPF MenuItem 辅助类，整体重写）

## 风险点（已缓解）

- **IWindowManagerService 扩展方法在 WPF 实现中转发到 `MainWindow.Instance`**：
  启动顺序保证 `MainWindow.Instance` 在命令执行时已就绪（命令由用户交互触发，此时主窗口必然存在）。
- **OpenRepository 签名**：`bool OpenRepository(string path, GitModule nextTo = null)` 与 `TabManager.OpenRepository` 一致，
  GitModule 是领域类型（非 WPF），可安全暴露在 Services 接口。
- **WindowsFileAssociationService 内化 P/Invoke**：消除 service→command 反向依赖，command 不再持有任何 P/Invoke。

## 后续阶段衔接

阶段 2 完成后，Commands 层的 WPF 直接调用已大幅减少（MessageBox/Process/OpenDialog/Tab管理/应用操作 全部清除）。
剩余的 `Application.Current.ActiveRepositoryUserControl()` 类别 3 遗留是阶段 3（ViewModel 抽取）的核心动机——
这些命令需要通过 ViewModel 而非直接操作 View 来触发仓库视图刷新/切换。
