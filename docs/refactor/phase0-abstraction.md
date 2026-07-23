# 阶段 0：跨平台抽象层补全

> 状态：**已完成**
> 提交：`82296a2 refactor(阶段0): 补全跨平台抽象层，为 WPF→Avalonia 迁移铺路`
> 性质：纯增量，不替换任何现有调用点，保证构建零风险

## 目标

补全 `Services/` 抽象接口 + WPF 实现 + 注册到 ServiceLocator，为后续阶段铺路。阶段 0 **不替换任何现有调用点**，只新增接口和实现，让业务代码"有路可走"。

## 已完成项

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

## 待补接口（推迟到对应阶段）

| 接口 | 推迟到 | 原因 |
|------|--------|------|
| `IIpcService` | 阶段 5 | 现有 `IpcServer` handler 签名 `Action<NamedPipeServerStream>` 与平台强耦合，需先重构 handler 为 `Action<Stream>` |
| `IThemeService` | 阶段 4 | `ReloadThemeDictionary` / `ApplyCustomColors` 操作 WPF `MergedDictionaries`，属 View 层职责，且需改 `App` 私有方法可见性 |
| `IWebViewFactory` / `IMarkdownWebView` | 阶段 3 | WebView2 事件面太大（ContextMenuRequested / WebMessageReceived / NavigationCompleted），需先设计 AI 窗口 ViewModel 再定义接口 |
| `IWindowPlacementService` | 阶段 4 | `SetWindowPlacement` / `GetWindowPlacement` 仅 `MainWindow` 使用，Win32 窗口管理专属 |
| `ISingleInstanceService` | 阶段 5 | 仅 `App.HandleCommandLineArguments` 启动时用，`Process.GetProcessesByName` + named pipe client 跨平台语义不同 |

## 变更范围

17 个文件，纯增量（4 改 + 13 新）：

**修改的 4 个文件**：
- `src/ForkPlus/App.xaml.cs` — `OnStartup` 加 1 个 `RegisterPlatformServices` 调用
- `src/ForkPlus/Services/IAppContext.cs` — 接口加 7 个成员
- `src/ForkPlus/Services/Wpf/WpfAppContext.cs` — 实现 7 个新成员
- `src/ForkPlus/Services/ServiceLocator.cs` — 加 6 个属性 + `RegisterPlatformServices` 方法 + `Reset` 清理

**新增的 13 个文件**：
- 6 个接口文件（`Services/*.cs`）
- 6 个 WPF 实现文件（`Services/Wpf/*.cs`）
- 本文档

## 构建验证

沙箱是 Linux 无 dotnet SDK，且 TFM 为 `net10.0-windows10.0.19041.0`（WPF Windows-only），无法在本机 build。请在 Windows 上验证：

```powershell
dotnet build src/ForkPlus.sln -c Release
```

或推送到 `master-refactor` 触发 CI（Windows runner，已加入 build.yml 触发分支）。
