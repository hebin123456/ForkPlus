# 阶段 1：领域层验证

> 状态：**待开始**
> 性质：纯清理，零功能变更
> 前置：阶段 0 完成（`ICredentialService` 等抽象已就位）

## 目标

确认领域层目录 `Git/` / `Biturbo/` / `Accounts/` / `Jobs/` / `Settings/` **不依赖 `System.Windows.*`**，使其成为可在 Avalonia 项目中直接复用的纯 C# 代码。

## 验收标准

```bash
# 在以下目录执行，应为 0 命中
grep -rn "using System.Windows" src/ForkPlus/Git/ src/ForkPlus/Biturbo/ src/ForkPlus/Accounts/ src/ForkPlus/Jobs/ src/ForkPlus/Settings/
```

## 待办清单

### Accounts 层（最需要处理）

- [ ] `Accounts/PrivateAccessTokenAuthentication.cs` — 4 处 `WindowsCredentialManager` 直接调用 → 迁移到 `ServiceLocator.Credential`
  - 第 44 行：`RemoveCredential(Key(...))`
  - 第 52 行：`RemoveCredential(OldKey(...))`
  - 第 69 行：`WriteCredential(Key(...), Username, Token)`
  - 第 109/113 行：`ReadCredential(Key/OldKey(...))`
- [ ] `Accounts/BitbucketOAuthAuthentication.cs` — 10 处 `WindowsCredentialManager` 直接调用 → 迁移到 `ServiceLocator.Credential`
  - 第 44/52/60 行：`RemoveCredential` ×3
  - 第 77/81/87 行：`WriteCredential` ×3
  - 第 167/174/175 行：`ReadCredential` ×3
- [ ] `Accounts/AiServices/OpenAiService.cs` — 依赖 `PreferencesLocalization.FormatCurrent`（静态本地化）→ 改为通过回调或 `ILocalizationService` 注入

### Git 层（预期已干净）

- [ ] `Git/GitModule.cs` — 确认仅依赖 `System.Diagnostics` / `System.IO`，无 WPF
- [ ] `Git/Commands/*.cs` — 全部 git 命令封装应无 WPF 依赖
- [ ] `Git/Interaction/GitRequest.cs` — `Process.Start` 属于 git 调用，保留但确认无 WPF 控件访问

### Biturbo 层（预期已干净）

- [ ] `Biturbo/Bt.cs` — 确认仅 P/Invoke 到 native libgit2，无 WPF
- [ ] `Biturbo/BtRepositoryManager.cs` — 同上

### Jobs / Settings 层（预期已干净）

- [ ] `Jobs/JobQueue.cs` / `Job.cs` / `JobMonitor.cs` — 确认无 WPF
- [ ] `Settings/ForkPlusSettings.cs` — 确认无 WPF

## 风险点

- `OpenAiService` 的 `PreferencesLocalization.FormatCurrent` 依赖是 UI 层本地化静态方法，需引入 `ILocalizationService` 抽象（阶段 0 未补，本阶段补上）
- `Accounts/` 层的 `WindowsCredentialManager` 调用迁移后，需确保 `ServiceLocator.Credential` 在调用前已初始化（启动顺序）

## 后续阶段衔接

阶段 1 完成后，领域层即可在 Avalonia 项目中直接引用，不受 UI 框架切换影响。
