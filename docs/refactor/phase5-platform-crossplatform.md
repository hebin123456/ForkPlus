# 阶段 5：平台特定功能跨平台化

> 状态：**待开始**
> 性质：收尾清理，需逐个 Win32 调用评估替代方案
> 前置：阶段 4 完成（UI 已切 Avalonia，但 TFM 仍 net10.0-windows）

## 目标

把残留的 Win32 P/Invoke / Windows 专属 API 替换为跨平台方案，为阶段 6 切换 TFM 到 `net10.0` 扫清障碍。

## 待办清单

### Win32 P/Invoke 跨平台化

- [ ] `App.xaml.cs` — `SetCurrentProcessExplicitAppUserModelID`（shell32 第 38-50 行）
  - Windows 专属（任务栏分组），Linux/macOS 无对应概念
  - 方案：平台条件编译 `#if WINDOWS` 或直接删除（非关键功能）

- [ ] `App.xaml.cs` — 注册表读取（第 1093-1121 行）
  - 已抽象到 `ISystemThemeService`（阶段 0 完成），本阶段迁移调用点
  - `GetSystemTheme` / `IsSystemAccentBrushEnabled` → `ServiceLocator.SystemTheme.CurrentSystemTheme` / `IsSystemAccentBrushEnabled`

- [ ] `App.xaml.cs` — `SystemEvents.UserPreferenceChanged`（第 789 行，`Microsoft.Win32.SystemEvents`）
  - Windows 专属系统事件
  - 方案：`ISystemThemeService.SubscribeToSystemEvents` 已封装，Linux/macOS 实现可监听 freedesktop 设置变更或无操作

- [ ] `UI/Helpers/WindowLocationStateExtensions.cs` — `SetWindowPlacement` / `GetWindowPlacement`（user32 第 141-159 行）
  - 已识别为阶段 0 推迟接口 `IWindowPlacementService`（阶段 4 补）
  - Avalonia 有原生窗口位置 API，可直接用

- [ ] `UI/Commands/OpenFileInDefaultEditorCommand.cs` — `AssocQueryString`（Shlwapi 第 174 行）
  - 已抽象到 `IFileAssociationService`（阶段 0 完成），阶段 2 迁移调用点
  - Linux/macOS 实现：查询 mime 类型或始终返回 true（由 xdg-open 决定）

- [ ] `UI/UserControls/ChangedFilesDisplayNormalizer.cs` — kernel32 P/Invoke（第 265/268 行）
  - 需确认用途，评估是否可移除或跨平台替代

- [ ] `UI/UserControls/IconTools.cs` — shell32 / user32（第 16/19 行）
  - Shell 图标提取，Linux/macOS 需用其他方案（如 mime-icon 库）

- [ ] `UI/Helpers/MouseHelper.cs` — user32（第 16 行）
  - 鼠标状态检测，Avalonia 有原生 `PointerDevice` API

- [ ] `UI/Controls/TouchpadAwareScrollViewer.cs` — Win32 触控板
  - Avalonia ScrollViewer 原生支持触控板，直接替换

- [ ] `FileHelper.cs` — `MoveFileEx`（Kernel32 第 124 行）
  - 原子文件写入，Linux/macOS 用 `File.Move` + `File.Replace` 或 `rename(2)` 系统调用

- [ ] `ProcessExtensions.cs` — kernel32（第 20-30 行）
  - 需确认用途，可能为进程父子关系查询

### IPC 跨平台化

- [ ] `App.xaml.cs` — `NamedPipeServerStream` / `NamedPipeClientStream`（第 339-340/980 行）
  - 已识别为阶段 0 推迟接口 `IIpcService`（本阶段补）
  - 需先重构 `IpcServer` handler 签名：`Action<NamedPipeServerStream>` → `Action<Stream>`
  - Linux/macOS named pipe（FIFO）语义不同，但 .NET `NamedPipeServerStream` 跨平台可用
  - 单实例检测 `Process.GetProcessesByName` 在 Linux/macOS 行为不同 → `ISingleInstanceService`

- [ ] `IO/Ipc/NamedPipeHelper.cs` — `CreatePipeClient`（第 23-26 行）
  - `TokenImpersonationLevel.Impersonation` 在 Linux/macOS 无意义，需平台条件

- [ ] `UI/Dialogs/InteractiveRebaseWindow.xaml.cs` — `_riIpcServer`（第 54/106 行）
  - 同上，迁移到 `IIpcService`

### 凭据管理跨平台化

- [ ] `WindowsCredentialManager.cs` — advapi32 Cred* API
  - 已抽象到 `ICredentialService`（阶段 0 完成），阶段 1 迁移 Accounts/ 调用点
  - Linux 实现方案：`libsecret` / `GNOME Keyring` / `KWallet`
  - macOS 实现方案：`Security.framework` Keychain

### Toast 通知跨平台化

- [ ] `Services/Wpf/WpfToastNotificationService.cs` — `CommunityToolkit.WinUI.Notifications`（WinRT）
  - 已抽象到 `IToastNotificationService`（阶段 0 之前就有）
  - Linux 实现：`notify-send` / `Freedesktop Notifications`
  - macOS 实现：`NSUserNotificationCenter`

### 阶段 0 推迟的接口（本阶段补）

- [ ] `IIpcService`
  - handler 签名从 `Action<NamedPipeServerStream>` 改为 `Action<Stream>`
  - WPF 实现保留 named pipe，Avalonia 实现可换用 Unix domain socket 或继续 named pipe

- [ ] `ISingleInstanceService`
  - `Process.GetProcessesByName` + named pipe client 跨平台语义不同
  - Linux/macOS 方案：文件锁 / Unix domain socket

## 风险点

- **Linux/macOS 凭据管理碎片化**：libsecret / GNOME Keyring / KWallet 三选一或动态检测，工作量大
- **IPC 跨平台语义差异**：named pipe 在 Linux 是 FIFO，消息模式行为不同，可能需切 Unix domain socket
- **Shell 图标提取无跨平台等价物**：`IconTools.cs` 的 shell32 图标提取在 Linux/macOS 需完全重写

## 后续阶段衔接

阶段 5 完成后，所有 Win32 P/Invoke 已抽象或移除，TFM 可切换到 `net10.0`（阶段 6）。
