# 阶段 2：Commands 层去 WPF 化

> 状态：**待开始**
> 性质：中等风险，需逐个命令验证
> 前置：阶段 0 完成（抽象服务已就位）、阶段 1 完成（领域层干净）

## 目标

将 `src/ForkPlus/UI/Commands/` 下 160+ 命令类中的 WPF 直接调用替换为阶段 0 抽象的服务接口，使 Commands 层不再直接依赖 `System.Windows.*`。

## 验收标准

```bash
# Commands 目录下应为 0 命中
grep -rn "System.Windows.MessageBox" src/ForkPlus/UI/Commands/
grep -rn "Application.Current" src/ForkPlus/UI/Commands/
grep -rn "Process.Start" src/ForkPlus/UI/Commands/
```

## 待办清单（按调用频率排序）

### 高优先级文件

- [ ] `OpenFileInDefaultEditorCommand.cs`
  - `Process.Start` ×2 → `ServiceLocator.Process.OpenFileInDefaultApplication`
  - `AssocQueryString` ×1 → `ServiceLocator.FileAssociation.GetAssociatedExecutable` / `IsEditorAvailable`
  - `Application.Current.ActiveRepositoryUserControl()` ×1 → 通过参数传入 `TempFileManager`

- [ ] `CheckForkSyncCommand.cs`
  - 4 处 `System.Windows.MessageBox.Show` → `ServiceLocator.MessageBox.Show`
  - 涉及第 45/62/75/91 行

- [ ] `OpenRepositoryInFileExplorerCommand.cs` / `OpenSubmoduleCommand.cs` / `OpenWorktreeCommand.cs`
  - `Process.Start` → `ServiceLocator.Process.OpenDirectoryInFileExplorer`

- [ ] `ShowInitRepositoryWindowCommand.cs` / `OpenRepositoryCommand.cs` / `ApplyPatchCommand.cs` / `SaveFileCommand.cs` / `MoveSubmoduleCommand.cs` / `ShowSaveAsPatchDialogCommand.cs`
  - `OpenDialog.Select*` → `ServiceLocator.FileSystemDialog.Select*`

- [ ] `RunCustomCommandCommand.cs`
  - `MainWindow.Instance` → 通过参数传入或扩展 `IWindowManagerService`

- [ ] `NewTabCommand.cs` / `CloseActiveTabCommand.cs` / `SelectPreviousTabCommand.cs` / `SelectNextTabCommand.cs`
  - `Application.Current.MainWindow as MainWindow` → 扩展 `IWindowManagerService` 暴露 Tab 管理接口

### 命令基类去 WPF 化

- [ ] `IUICommand.cs`
  - `KeyGesture`（`System.Windows.Input`）→ 定义平台无关 `IKeyGesture` 接口，或改用 `string` 表示快捷键（如 `"Ctrl+Shift+O"`）
  - `Shortcut` / `SecondaryShortcut` 属性类型更新

### 全局清理

- [ ] 全局 grep `System.Windows.MessageBox.Show` 在 `UI/Commands/` 下应为 0 命中
- [ ] 全局 grep `Application.Current` 在 `UI/Commands/` 下应为 0 命中
- [ ] 全局 grep `MainWindow.Instance` 在 `UI/Commands/` 下应为 0 命中
- [ ] 全局 grep `Process.Start` 在 `UI/Commands/` 下应为 0 命中

## 迁移模式示例

**迁移前**（`CheckForkSyncCommand.cs`）：
```csharp
System.Windows.MessageBox.Show(
    PreferencesLocalization.Current("No remotes configured."),
    PreferencesLocalization.Current("Remote Sync Status"),
    System.Windows.MessageBoxButton.OK,
    System.Windows.MessageBoxImage.Warning);
```

**迁移后**：
```csharp
ServiceLocator.MessageBox.Show(
    PreferencesLocalization.Current("No remotes configured."),
    PreferencesLocalization.Current("Remote Sync Status"),
    Services.MessageBoxButton.OK,
    Services.MessageBoxImage.Warning);
```

## 风险点

- `MainWindow.Instance` / `Application.Current.MainWindow as MainWindow` 的迁移需要扩展 `IWindowManagerService`，涉及 TabManager / Toolbar 等子系统的访问，可能需要在阶段 3 ViewModel 抽取后才能彻底解决
- `KeyGesture` 类型变更会波及 160+ 命令的实现，建议先定义新接口，再批量替换
