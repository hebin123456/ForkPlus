# 原生凭据弹窗收编方案

目标：用户在 ForkPlus 内做任何需要凭据的 git 操作时，只会看到 ForkPlus 自己的 `AskPassWindow`，永远不出现 git 原生链路弹出的窗口（Windows 上典型是 Git Credential Manager 的 GUI，下称 GCM）。本文记录根因分析、已验证的 git 机制、分层实施计划与每一步的落地状态。

## 现状机制

ForkPlus 已经有一套自有的凭据管道：

- 主程序拉起 git 时注入 `SSH_ASKPASS` = `ForkPlus.AskPass`、`SSH_ASKPASS_REQUIRE=force`（`GitRequest.CreateDefaultEnv` / `CreateGitProcessStartInfo`、`ShellRequest.CreateProcessStartInfo`）。
- `ForkPlus.AskPass` 是一个桥接程序：git/ssh 以 askpass 或 credential helper 身份调用它，它通过命名管道 `Fork_Pipe{pid}_AskPass` 把请求转给主程序。
- 主程序 `App.AskPassIpcMessageHandler` 分流：askpass 模式弹 `AskPassWindow`；credential helper 模式查 `AccountManager` 里已登录的账号并回填 HTTPS 用户名/密码。
- 大多数网络类 git 命令（fetch/pull/push/clone/lfs 等，共 53 个调用点）还会前置 `-c credential.helper=...` 覆盖链。

## 根因：三个泄露点

| # | 泄露路径 | 位置 | 触发条件 |
|---|---------|------|---------|
| ① | 覆盖链末端显式保留 `credential.helper="manager"` | `App.axaml.cs` 的 `_overrideCredentialHelper` / `_overrideCredentialHelperBt` | 账号管理器里没有匹配该 host 的账号时，AskPass helper 返回空，git 继续遍历到 `manager`，GCM 弹原生窗口 |
| ② | 无账号时覆盖链退化为空数组 | `App.axaml.cs` 的 `OverrideCredentialHelper` / `OverrideCredentialHelperBt` getter（`Accounts.Length == 0` 返回 `string[0]`） | 用户一个账号都没配时，完全不注入 `-c` 覆盖，走用户系统/全局配置里的 helper 链（Windows 默认就是 GCM） |
| ③ | git mm 系列路径不挂覆盖链 | `GitMmUserControl.axaml.cs`、`InitGitMmRepositoryWindow.axaml.cs`（`new GitCommand("mm")` 不带 override，只设 `GIT_TERMINAL_PROMPT=0`） | `-c` 参数不随环境传播，git-mm 内部再拉起的 git 子进程拿不到覆盖链；同理 `git submodule update` 的子进程、`clone --recurse-submodules` 也不继承 `-c` |

①解释了"有时候"：对已配置账号的 host 操作完全静默，一旦碰到没配账号的 host（例如公司内网 GitLab），原生弹窗就冒出来。`GIT_TERMINAL_PROMPT=0` 只封终端询问，不封 GCM 的 GUI 弹窗，帮不上忙。

## 已验证的 git 机制

以下事实由 git 2.34.1 本地受控实验（`git credential fill` + 假 helper / 假 askpass）与官方文档双重确认：

| 机制 | 结论 |
|------|------|
| helper 链遍历 | git 按顺序跑 `credential.helper` 链，任一 helper 给出完整 username+password 即停止；helper 全部落空才回落 askpass 链 |
| askpass 回落顺序 | `GIT_ASKPASS` → `core.askPass` → `SSH_ASKPASS` → 终端；`SSH_ASKPASS` 对 HTTPS 用户名/密码询问同样生效（实验 T3 验证） |
| 空值重置 | `credential.helper` 配置为空字符串会清空当前已累积的 helper 列表，随后的条目重新追加（实验 T4b 验证） |
| `quit` 属性 | helper 输出 `quit=1` 时，后续 helper 与用户询问全部终止，git 以失败退出（实验 T5b 验证，git 2.34 起可用） |
| `GIT_CONFIG_COUNT` / `GIT_CONFIG_KEY_n` / `GIT_CONFIG_VALUE_n` | 环境变量形式的 `-c`，且随进程树传播（git ≥ 2.31；本仓库要求 git ≥ 2.40，内置 2.50.1）。这是让 git-mm / submodule 子进程树继承覆盖的关键武器（实验 T1b/T4b 用 env 形式验证了注入与重置均生效） |
| GCM 弹窗条件 | GCM 被调用且无已存凭据时按 `interactive` 配置决定是否弹 GUI（默认 Auto = 弹） |

## 分层方案

### Layer A：链上收编

改动集中在 `App.axaml.cs`：

1. 两个覆盖数组各删掉末尾的 `credential.helper="manager"` 条目（6 元素 → 4 元素）。删掉后，helper 链只剩"重置 + ForkPlus.AskPass"，账号未命中时 git 落到 askpass 兜底链，`SSH_ASKPASS` 已指向 ForkPlus.AskPass，`AskPassWindow` 成为唯一入口。
2. `OverrideCredentialHelper` / `OverrideCredentialHelperBt` getter 去掉 `Accounts.Length == 0` 分支，无条件返回覆盖数组；`_defaultCredentialHelper` 字段随之删除。
3. IPC handler 的未命中分支保持返回空字符串（这正是让 git 回落到 AskPassWindow 的信号，无需 `quit=1`——quit 会连 askpass 兜底一起掐断，交互场景下用户反而没了输入机会）。

### Layer B：环境级收编

`-c` 参数不随进程树传播，环境变量会。落地为独立 helper 类 `GitCredentialEnv`（`src/ForkPlus/GitCredentialEnv.cs`），在 `GitRequest.CreateDefaultEnv`（Bt 原生 spawn 路径）、`GitRequest.CreateGitProcessStartInfo`（psi 路径）、`ShellRequest.CreateProcessStartInfo`（shell 路径）三处统一注入：

```
GIT_ASKPASS            = ForkPlus.AskPass 路径（封住回落链第一环，防终端继承残留）
GIT_TERMINAL_PROMPT    = 0（所有 askpass 落空时快速失败，不挂死在不可见的终端询问上）
GIT_CONFIG_COUNT       = 2
GIT_CONFIG_KEY_0/VALUE_0 = credential.helper = （空，重置）
GIT_CONFIG_KEY_1/VALUE_1 = credential.helper = <ForkPlus.AskPass 路径，EscapeSpaces 转义>
```

实现细节：

- 已有 `GIT_CONFIG_COUNT`（父环境或调用方 additionalEnv）时，注入条目从现有 index 顺延编号（`FindConfigCount` 从平铺数组尾部扫描、`ApplyToProcessStartInfo` 在 additionalEnv 应用后读字典），不覆盖既有注入。
- helper 值的引号/转义约定与 `-c` 形式（Layer A）保持一致。
- 专项测试 `GitCredentialEnvTests`：7 对结构、起点顺延、调用方条目保留、psi 字典应用。

这一层根治泄露点 ③：git-mm 子进程、`submodule update` 子进程、任何 git-spawns-git 场景都继承同一覆盖语义。范围边界：用户自定义命令（`ShCustomCommandAction` / `ProcessCustomCommandAction`）继承用户环境跑任意命令，属用户显式操作，不在收编范围内。

### Layer C：语义收编

现状：`ForkPlus.AskPass` 识别 `get/store/erase` 三个 action，但 IPC 消息只传 mode，action 信息丢失；主程序对三个 action 一律执行"查账号回填"，`store`/`erase` 形同虚设。收编后：

1. 管道协议扩展：mode 增加 `"4"`（store）、`"5"`（erase）。落地时发现实现缺口：`ForkPlus.AskPass/Program.cs` 的 mode 映射此前把 get/store/erase 一律折叠成 `"2"/"3"`，action 信息在 IPC 边界就丢了——新增 `GetCredentialHelperMode` 纯函数按 action 分流（get=2/3、store=4、erase=5），否则 App 侧的 4/5 分支是死代码。
2. `get`：账号命中 → 回填；未命中 → 查 Windows Credential Manager 的 GCM 兼容键（`git:https://<host>`，读用户由 GCM 存下的存量凭据，实现无痛迁移）；仍无 → 返回空，回落 AskPassWindow。
3. `store`：把凭据写入 Windows Credential Manager（GCM 兼容键，GCM 外部读写互通），下次静默命中。
4. `erase`：删除对应键。
5. 所有 WCM 访问加 `OperatingSystem.IsWindows()` 守卫——现状 `ShowAskPassWindowCommand.QueryFromWindowsCredentialManager` 在 Linux 上会抛 `DllNotFoundException`，而 `IpcServer` 只捕获 `IOException`，这个异常足以把 IPC 线程带崩，顺带修复。Linux/macOS 上 store/erase 暂为空操作（记录在权衡一节）。

## 已知权衡与风险

- **GCM 存量凭据**：Layer A/B 后 GCM 不再参与凭据获取，但 Layer C 的 `get` 会先读 WCM 里的 GCM 兼容键，Windows 用户由 GCM 存过的凭据可以直接复用，不强制重输。
- **取消即失败**：用户在 AskPassWindow 点取消后 git 直接失败退出，不出现二次弹窗。符合"感知不到原生弹窗"的目标，代价是没有"跳过本次"的中间态。
- **平台差异**：Linux/macOS 没有 Windows Credential Manager，Layer C 的持久化在那两个平台暂为空操作（每次重输）。~~后续可接 `git-credential-store` 文件或系统 keyring，不在本计划内~~ → 已由 Layer D 补齐（见下）。
- **预存在的小坑（保持现状，仅记录）**：`_overrideCredentialHelper` 的值同时叠加了字面引号与 `EscapeSpaces()` 反斜杠转义，安装路径含空格时两种转义可能互相打架。默认安装路径不含空格，本计划不动它，避免行为漂移。
- **`GIT_CONFIG_COUNT` 侵入性**：环境注入对 git-mm / submodule 子树全局生效，包括它们内部读取 git config 的场景（如 `git config --list` 会看到注入项）。仓库内没有读取 credential 配置做展示的代码，影响面可控。

### Layer D：跨平台凭据记忆（自动记账号 / 记住密码 / 记住密码 + 不再弹出）

Layer C 落地后 Linux/macOS 仍是"每次重输"（WCM 平台守卫使 store/erase 空操作）。Layer D 以独立存储 `SavedCredentialStore`（`src/ForkPlus/Git/SavedCredentialStore.cs`，`ForkDirectoryPath/credentials.json`，AtomicWrite 落盘）补齐，并把凭据弹窗升级为三档记忆：

| 档位 | 触发 | 效果 |
|------|------|------|
| 记住账号（默认行为，无勾选框） | HTTP(S) Username 询问提交 | host → username 自动落盘，下次询问预填账号 |
| 记住密码（显式勾选"Remember password"） | HTTP(S) Password 询问提交 | password 落盘；下次弹窗**仍出现**，但密码框自动预填且"记住密码"预勾选（弹窗内自动填充） |
| 记住密码 + 不再弹出（显式勾选"Remember password and never ask again"） | HTTP(S) Password 询问提交 | password + NeverAskAgain 落盘；credential `get`（App IPC mode 2/3，非 Windows 自动填充的主路径）与 askpass（`ShowAskPassWindowCommand` 兜底）全链路静默回填，完全不弹窗；凭据缺失（被 erase）时空响应快速失败。偏好设置 > Credentials 的"不再弹出"开关可随时重新打开弹窗 |

落地细节：

1. `App.AskPassIpcMessageHandler` 的 `get` 分支在 GCM 兼容键未命中后追加 `SavedCredentialStore.TryGetSilentCredential`（仅第三档命中：password + NeverAskAgain 皆备；第二档/第一档不命中，回落 askpass 弹窗预填）；`erase` 分支（mode 5）联动 `ForgetPassword`——密码失效时只清密码，保留账号记忆与"不再询问"标记，防"get 永远命中旧密码"死循环。
2. `AskPassWindow`：Username 模式预填已记住账号，提交即自动记住（第一档，无勾选框）；HTTP(S) Password 模式预填已记住密码 + "记住密码"预勾选，新增"记住密码"/"不再弹出"两个选项（第二/三档；SSH 场景的 RememberCheckBox 语义不变）。prompt 解析（`TryParseUsernamePrompt`/`TryParsePasswordPrompt`）只认 `http(s)://` URL，SSH 询问不受影响。
3. `ShowAskPassWindowCommand`：HTTP(S) 询问在弹窗前查记忆——第三档静默回填（兜底，主路径在 credential get）；"不再弹出"但凭据缺失 → 空响应快速失败；第二档/第一档 → 弹窗（分别预填密码/账号）。
4. 偏好设置新增 Credentials 页（`CredentialsUserControl`）：提前录入（host/账号/密码/"不再弹出"开关，Add 即 `Upsert` 整条落盘）、单条行内编辑（账号/密码框 + 开关 + Save）、"不再弹出"ToggleSwitch 即时生效（重新弹出的开关）、单条 Remove（整条删除，下次从零开始）、全局 Ask Again for All Hosts。凭据操作即时落盘，无需整页 Save。
5. 明文密码是已知权衡：Linux 无跨桌面环境统一 keyring 抽象（libsecret 依赖 D-Bus session，CI/远程环境普遍缺失），与 accounts.json 的既有处理一致，依赖用户主目录权限保护。
6. 专项测试：`SavedCredentialStoreTests`（prompt 解析 / CRUD 语义 / 落盘重载 / Command 静默路径）+ `CredentialsRememberUiTests`（弹窗三档装配与提交写入 / 偏好页列表与按钮，SwapForTests 注入隔离 store）。

## 验证计划

- 每层提交后跟 CI 全量测试（test job 在 ubuntu 上跑 4255 项）。
- 补充专项测试：覆盖数组断言不含 `manager`；AskPass 程序 mode 4/5 的协议分支；IPC handler store/erase 分支的平台守卫。
- 人工场景（Windows 实机）：无账号 + 私有 host 的 fetch（应弹 AskPassWindow 而非 GCM）、输入后二次 fetch（应静默）、git mm 同场景、submodule update 同场景。

## 推进状态

| 里程碑 | 内容 | 状态 | 提交 |
|--------|------|------|------|
| Layer 0 | 本方案落档 | 已完成 | 4bfd953 |
| Layer A | 覆盖链移除 manager、getter 无条件覆盖 + 专项测试（CredentialHelperOverrideTests） | 已实施 | d583ff1 |
| Layer B | GIT_CONFIG_* / GIT_ASKPASS / GIT_TERMINAL_PROMPT 环境注入（GitCredentialEnv）+ 专项测试 | 已实施 | d1ac91e |
| Layer C | store/erase 语义 + WCM 兼容读写（GcmCompatibleStore）+ AskPass mode 分流 + 平台守卫（含 ShowAskPassWindowCommand 顺带修复）+ 专项测试 | 已实施 | fdd5251 |
| CI 修复① | GitCredentialEnvTests 缺 `using System`（CS0103，test job 编译失败） | 已实施 | 3c75a4e |
| CI 修复② | `ApplyToProcessStartInfo` 对缺失 `GIT_CONFIG_COUNT` 抛 KeyNotFoundException（干净环境 git 请求整体失败，E2E 连锁 109 红）→ ContainsKey 两段式；第一版误用 `StringDictionary.TryGetValue`（不存在，CS1061 四平台编译挂）→ 修正 | 已实施 | 4b82d98 |
| CI 全量验证 | 4b82d98：linux test job 4302/4302 绿 + AskPass 10/10 + RI 6/6，三平台 build 绿。注：Layer B 的测试与运行时 bug 此前一直被编译错误掩盖，直到 3c75a4e 首次真正执行才暴露 | 通过 | 4b82d98 |
| Layer D | 跨平台凭据记忆：SavedCredentialStore（credentials.json）+ AskPassWindow 三档记忆 + get/erase 链路接入 + 偏好设置 Credentials 页 + 专项测试 29 项 | 已实施 | （本次提交） |
