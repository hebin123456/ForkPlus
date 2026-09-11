# ForkPlus 用户手册

> 适用版本：ForkPlus v4.0+（跨平台版） · 文档日期：2026-09-07
>
> 本手册基于全量 UI 自动化测试用例（26 个模块、163 个用例、213 张截图证据）整理而成，
> 覆盖 ForkPlus 的全部用户可见功能。文中插图均取自测试套件的真实渲染截图：
> 主界面统一为 1920×1080，对话框按其真实比例呈现。

---

## 目录

1. [产品简介](#1-产品简介)
2. [安装与启动](#2-安装与启动)
3. [主界面总览](#3-主界面总览)
4. [提交历史浏览与检索](#4-提交历史浏览与检索)
5. [修订详情](#5-修订详情)
6. [变更与提交](#6-变更与提交)
7. [Diff 查看器](#7-diff-查看器)
8. [解决合并冲突](#8-解决合并冲突)
9. [分支操作](#9-分支操作)
10. [标签操作](#10-标签操作)
11. [历史改写](#11-历史改写)
12. [贮藏（Stash）与快照](#12-贮藏stash与快照)
13. [远程仓库交互](#13-远程仓库交互)
14. [子模块与 Worktree](#14-子模块与-worktree)
15. [工作流套件](#15-工作流套件)
16. [Git LFS](#16-git-lfs)
17. [补丁](#17-补丁)
18. [查看类窗口](#18-查看类窗口)
19. [偏好设置](#19-偏好设置)
20. [仓库设置](#20-仓库设置)
21. [SSH 密钥与环境](#21-ssh-密钥与环境)
22. [AI 功能](#22-ai-功能)
23. [通用工具与对话框](#23-通用工具与对话框)
24. [键盘快捷键大全](#24-键盘快捷键大全)
25. [常见问题](#25-常见问题)

---

## 1. 产品简介

ForkPlus 从 v4.0 起为跨平台版本，UI 层从 Windows 专用的 WPF 技术栈迁移至
[Avalonia 12](https://avaloniaui.net/) 框架，可在 **Windows、Linux、macOS** 三大桌面平台运行。
功能与 WPF 原版（v3.x）对齐，并针对跨平台环境做了适配修复（如 Unix 下的 SSH 密钥工具路径、
git ≥ 2.38 的本地子模块安全限制等）。

### 功能全景

| 分类 | 能力 |
|------|------|
| 仓库管理 | 欢迎页克隆/新建/浏览、仓库列表分组与搜索、多标签页、Workspace 工作区 |
| 历史浏览 | 提交图表、多选、搜索、修订详情（摘要/变更/文件树）、Reflog、标签过滤 |
| 变更与提交 | 暂存/取消暂存、行级 stage/discard、提交消息自动补全、提交模板 |
| Diff 系统 | Split/Side-by-side 双布局、滚动同步、diff 弹窗、二进制 Hex 视图、图片四模式对比 |
| 冲突解决 | 内联冲突视图、三方并排编辑器、块级 ours/theirs 选择、滚动同步 |
| Git 工作流 | 分支（9 窗口）、标签（5 窗口）、合并/变基/拣选/还原/重置/交互式变基、Stash、远程（fetch/pull/push/编辑远程）、子模块、Worktree |
| 工作流套件 | GitFlow（init/feature/release/hotfix）、git-mm、LeanBranching |
| 大文件与补丁 | Git LFS（track/status/fetch/pull/lock）、保存/应用补丁、快照 |
| 查看器 | Blame、文件/文件夹历史、仓库概览、统计、独立修订详情窗、跳转到行 |
| 设置 | 偏好设置（7 个标签页）、仓库设置（4 个标签页）、自定义命令、自定义颜色 |
| 环境 | SSH 密钥管理与生成、Git 实例选择、终端/文件管理器集成 |
| AI | AI 提交消息组合、AI 代码审查（文件/分支范围）、AI 开发助手（多轮对话） |
| 辅助 | 多语言界面（含简体中文）、快捷键参考、通知中心、性能诊断、更新检查 |

---

## 2. 安装与启动

### 2.1 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10+ / Linux（主流发行版）/ macOS |
| .NET 运行时 | .NET 10 |
| Git | 系统自带或使用 ForkPlus 内置 Git 实例（可在设置中选择） |
| 可选组件 | git-flow（GitFlow 套件）、git-lfs（LFS 功能）、git-mm（GitMm 套件） |

### 2.2 从源码构建

```bash
# 安装 .NET 10 SDK 后：
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

git clone https://github.com/hebin123456/ForkPlus.git
cd ForkPlus
dotnet build ForkPlus.sln        # 构建主程序
dotnet run --project src/ForkPlus # 启动应用
```

详细的构建环境说明（SDK 版本、三方件自动拉取机制等）见仓库根目录 `BUILDING.md`。

### 2.3 首次启动

首次启动时显示欢迎窗口，提供三种进入方式：

- **Clone**：输入远程仓库 URL 与本地目标路径，克隆后直接打开；
- **Add existing**：浏览本地磁盘，把已有仓库加入列表；
- **新建**：在指定目录初始化一个新仓库（`Ctrl+Shift+N`）。

![首次启动的欢迎窗口](evidence/e2e/01-welcome/01-welcome-initial.png)

填写表单后即可进入仓库列表：

![填好表单的欢迎窗口](evidence/e2e/01-welcome/02-welcome-filled.png)

### 2.4 仓库列表（Repository Manager）

打开多个仓库后，仓库按分组（Workspace）显示，支持搜索过滤与键盘操作：
`F2` 重命名、`Delete` 移除、`Enter` 打开。

![仓库列表：分组展示与搜索](evidence/e2e/01-welcome/03-repomanager-with-repos.png)

选中某个仓库后高亮显示，双击或按 `Enter` 打开：

![仓库列表：选中状态](evidence/e2e/01-welcome/04-repomanager-selected.png)

---

## 3. 主界面总览

每个仓库在主窗口中占用一个标签页（Tab），可同时打开多个仓库，用 `Ctrl+T` 新建、
`Ctrl+W` 关闭、`Ctrl+Tab` / `Ctrl+Shift+Tab` 在标签间切换。

![多标签页：三个仓库同时打开](evidence/e2e/01-welcome/06-tabs-three.png)

主界面由四部分组成：

```
┌──────────────────────────────────────────────────────┐
│  标签页栏（多仓库切换）                                 │
├──────────────────────────────────────────────────────┤
│  工具栏（Pull / Push / Fetch / Stash 等 + 领先落后角标）│
├───────────┬──────────────────────────────────────────┤
│           │                                          │
│  侧边栏    │   主内容区（Changes / All Commits 双视图） │
│ 分支/标签/ │   - 修订详情（摘要/变更/文件树）            │
│ 远程/贮藏  │   - 文本 Diff / 图片对比 / 冲突视图         │
│           │                                          │
└───────────┴──────────────────────────────────────────┘
```

### 3.1 工具栏

工具栏按钮随仓库状态智能启用/禁用，并显示与上游的领先/落后角标：

- 未打开仓库时，所有 Git 操作按钮禁用；
- 仓库无上游分支时，pull/push 按钮可用但无角标；
- 有上游时显示数字角标（如 `2↑` 领先、`1↓` 落后），侧边栏同步显示。

![无仓库时工具栏全部禁用](evidence/e2e/06-toolbar/01-toolbar-no-repo-disabled.png)

![领先 2 / 落后 1 的角标显示](evidence/e2e/06-toolbar/03-toolbar-ahead-behind-badges.png)

按住 `Ctrl` 点击按钮（或使用 `Ctrl+Alt+Shift+F/L/P`）可执行 **快速** fetch/pull/push，跳过确认窗口。

### 3.2 侧边栏

侧边栏按分组列出仓库的全部引用：

- **分支**：本地分支（当前分支高亮）、远程分支，支持嵌套文件夹（如 `feature/xxx`）折叠展开；
- **标签**：全部 tag；
- **贮藏（Stashes）**：stash 条目列表；
- **远程 / 子模块**。

顶部过滤框实时过滤引用（带防抖），右键点击分支/分组弹出上下文菜单
（检出、合并、变基、删除、推送等操作入口）。

![侧边栏：分支分组与嵌套文件夹](evidence/e2e/03-sidebar/02-sidebar-folder-expanded.png)

![侧边栏：过滤生效](evidence/e2e/03-sidebar/06-sidebar-filtered.png)

![分支右键菜单](evidence/e2e/03-sidebar/08-sidebar-branch-context-menu.png)

---

## 4. 提交历史浏览与检索

### 4.1 提交图表（All Commits）

`Ctrl+2` 切换到 All Commits 视图，以图表形式渲染提交历史（分支拓扑、合并线），
支持行选择与多选、滚动虚拟化（大仓库流畅）、悬停 tooltip。
`Ctrl+0` 跳转回 HEAD；再次按 `Ctrl+2` 也会跳到 HEAD。

![提交历史视图](evidence/e2e/02-revisionlist/01-revision-list-loaded.png)

### 4.2 搜索

`Ctrl+F` 打开搜索面板，按提交信息/作者等条件匹配，命中的提交行高亮标记；
`Enter` / `F3` 跳到下一个结果，`Shift+Enter` / `Shift+F3` 跳到上一个；清空恢复。

![搜索命中标记](evidence/e2e/02-revisionlist/03-revision-search-matches.png)

列表支持 **水平/垂直方向切换**（适合窄屏）：

![切换方向后的提交列表](evidence/e2e/02-revisionlist/05-revision-orientation-switched.png)

`Ctrl+Shift+A` 可按当前分支过滤提交列表（只看当前分支可达的提交）。

---

## 5. 修订详情

选中单个提交后，下方显示修订详情，含三个标签页：

- **Commit（摘要）**：作者、日期、消息、SHA、父提交等信息；
- **Changes（变更）**：该提交改动的文件列表 + 内联 diff；选中文件按 `Space` 可弹窗查看；
- **File Tree（文件树）**：该提交的完整文件树，选中文件即时预览内容。

![修订详情：提交摘要](evidence/e2e/04-revisiondetails/01-revisiondetails-commit-summary.png)

![修订详情：Changes 标签页](evidence/e2e/04-revisiondetails/02-revisiondetails-changes-tab.png)

![修订详情：File Tree 标签页](evidence/e2e/04-revisiondetails/03-revisiondetails-filetree-tab.png)

**按住 Ctrl 选中两个提交**进入范围对比模式：File Tree 禁用、自动切换到 Changes，
显示两次提交之间的全部差异。

![双提交范围对比](evidence/e2e/04-revisiondetails/05-revisiondetails-range-compare.png)

状态栏还提供两个开关：

- **Reflog**：开启后提交列表包含引用日志条目，可找回"丢失"的提交；
- **Hide tags**：隐藏/显示标签引用；选中某个 tag 时状态栏显示 `Filtered by ...`。

![开启 Reflog](evidence/e2e/04-revisiondetails/06-revisiondetails-reflog-enabled.png)

---

## 6. 变更与提交

`Ctrl+1` 切换到 Changes 视图（再按一次聚焦提交消息输入框）。工作区改动分为
**未暂存（Unstaged）** 与 **已暂存（Staged）** 两个列表，按文件状态（修改/新增/删除）标色。

![Changes 视图](evidence/e2e/05-changescommit/01-commit-view-loaded.png)

### 6.1 暂存与取消暂存

- 选中文件按 `Enter`（或 `Ctrl+Shift+S`）：暂存/取消暂存**单个文件**；
- 底部按钮 **Stage All / Unstage All**（或 `Ctrl+Alt+Shift+S`）：全量切换，
  按钮文案随当前状态智能切换；
- 右键菜单同样提供上述操作。

![暂存选中文件](evidence/e2e/05-changescommit/02-stage-selected-file.png)

![Stage All 按钮](evidence/e2e/05-changescommit/04-stage-all.png)

### 6.2 行级（Chunk）操作

在 diff 中选中若干行后，浮出 **Stage** / **Discard...** 悬浮按钮：

- **Stage**：只把选中行生成补丁写入暂存区（`git apply --cached`），其余改动留在工作区；
- **Discard...**：弹出确认框后用反向补丁丢弃选中行，未选中的改动保持原样。

![选中行后浮出的 Stage / Discard 按钮](evidence/e2e/05-changescommit/06-floating-buttons-on-selection.png)

![行级 Stage 后：仅所选行入暂存](evidence/e2e/05-changescommit/07-line-level-stage-applied.png)

![Discard 确认弹窗](evidence/e2e/05-changescommit/09-discard-confirm-dialog.png)

### 6.3 提交消息自动补全

提交消息输入框内置自动补全：

- 输入 `Co-auth` 等前缀，30ms 防抖后弹出建议浮层（如 `Co-authored-by: <name>`）；
- `Tab` 采纳建议并定位光标；建议浮层支持 Gitmoji 与用户身份（带图标）两类条目。

![提交消息自动补全建议浮层](evidence/e2e/05-changescommit/11-commit-message-autocomplete.png)

### 6.4 提交

写好消息后：

- `Ctrl+Enter`：**提交**；
- `Ctrl+Shift+Enter`：**提交并推送**。

提交消息可配置：长度指示线、分页线（50/72 字符）、提交消息正则校验、
拼写检查（详见[偏好设置](#19-偏好设置)）；仓库级还可配置提交模板（详见[仓库设置](#20-仓库设置)）。

---

## 7. Diff 查看器

### 7.1 文本 Diff 的两种布局

diff 头部按钮在 **Split（上下对照）** 与 **Side-by-side（左右对照）** 之间切换，
设置全局持久化：

![Split 模式](evidence/e2e/07-textdiff/01-split-mode.png)

![Side-by-side 模式](evidence/e2e/07-textdiff/02-side-by-side-mode.png)

Side-by-side 模式下 **左右两侧滚动严格同步**（垂直与水平均同步），
交替滚动也不会出现弹动错位。

### 7.2 Diff 弹窗（DiffPopupWindow）

在 Changes 视图或修订详情的文件列表里选中文件，按 **`Space`** 弹出独立 diff 窗口
（约占主窗口 90% 大小）：

- 标题栏显示文件路径；
- `Space` 或 `Escape` 关闭；关闭后可再次 `Space` 重开；
- 弹窗内按 **`↑` / `↓`** 直接切换到上一个/下一个文件，弹窗内容与主视图选择同步更新；
- 长行可水平滚动。

![Commit 视图 Space 弹出的 diff 弹窗](evidence/e2e/08-diffpopup/01-popup-from-commit-view.png)

![弹窗内 ↓ 键切换到下一个文件](evidence/e2e/08-diffpopup/05-popup-arrow-next-file.png)

### 7.3 二进制 Diff

非文本文件自动切换到 **Hex 视图**（十六进制字节对比，差异高亮）：

![二进制文件的 Hex 视图](evidence/e2e/09-binarydiff/08-binary-hex-view.png)

### 7.4 图片对比（四种模式）

图片文件的 diff 提供四种查看模式（头部切换）：

1. **Side-by-Side**：左右并列，可叠加品红色差异高亮（HighlightPixels 开关）；
2. **Swipe**：拖动分割线对比新旧图片；
3. **Onion Skin**：透明度滑块混合新旧图片（0 = 旧图，1 = 新图）；
4. **Hex**：切换到字节级对比。

![图片对比：Side-by-Side](evidence/e2e/09-binarydiff/01-image-side-by-side.png)

![图片对比：Swipe 拖动分割线](evidence/e2e/09-binarydiff/03-image-swipe-dragged.png)

![图片对比：Onion Skin 50% 混合](evidence/e2e/09-binarydiff/05-image-onionskin-half.png)

![图片差异高亮（HighlightPixels）](evidence/e2e/09-binarydiff/06-image-highlightpixels.png)

---

## 8. 解决合并冲突

合并/变基产生冲突时，Changes 视图中的冲突文件显示内联冲突视图：

- 冲突块以 `<<<<<<< ours` / `=======` / `>>>>>>> theirs` 划分；
- 每个冲突块可单独选择 **ours** 或 **theirs**；
- 全部解决后 **Resolve** 按钮变为可提交状态（`Merge` 按钮），文件自动移入已暂存列表。

![内联冲突视图](evidence/e2e/10-mergeconflict/01-conflict-view.png)

![部分解决：1/2 → 2/2 状态](evidence/e2e/10-mergeconflict/07-partial-1-of-2.png)

### 三方并排编辑器（Side-by-Side Merge）

双击冲突文件（或从右键菜单打开）进入 **SideBySideMergeWindow** 独立窗口：
左中右三栏分别为 ours / 结果 / theirs，支持：

- **全选 ours / theirs** 一键解决整个文件；
- 块间 **Prev / Next** 导航按钮快速跳转冲突块；
- 双侧滚动同步；
- 完成后按 Resolve 提交结果。

![三方并排冲突编辑器](evidence/e2e/10-mergeconflict/04-sidebyside-window.png)

![全选 ours](evidence/e2e/10-mergeconflict/05-select-all-ours.png)

---

## 9. 分支操作

分支相关操作从侧边栏右键菜单或工具栏进入，共 9 个窗口：

| 操作 | 说明 |
|------|------|
| 新建分支 | 名称即时校验（空/非法/重复三态警告）、可选创建后立即检出、底部实时显示等价 git 命令预览 |
| 检出分支 | 单击检出；或选择 **Checkout as worktree** 检出为独立工作树 |
| 重命名分支 | 输入新名，预览 `git branch -m` |
| 删除本地分支 | 单分支视图，安全检查后删除 |
| 删除远程分支 | 单分支或**多选列表**两种模式，预览后删除 |
| 跟踪远程分支 | 为本地分支设置上游（`git branch -u`） |
| 推送多分支 | 列表多选，一次推送多个分支 |
| Lean 分支 | LeanBranching 流程的 Start / Finish（见[工作流套件](#15-工作流套件)） |

![新建分支：重复名警告](evidence/e2e/11-branchops/02-create-branch-duplicate-warning.png)

![新建分支：命令预览](evidence/e2e/11-branchops/03-create-branch-command-preview.png)

![检出分支窗口](evidence/e2e/11-branchops/04-checkout-branch.png)

![删除远程分支（多选模式）](evidence/e2e/11-branchops/07b-remove-remote-branches-multi.png)

> 提示：所有 Git 操作窗口底部都有**命令预览区**，提交前可看到将执行的完整 git 命令，
> 学习 git 的同时也能防止误操作。

---

## 10. 标签操作

标签操作共 5 个窗口，覆盖标签的完整生命周期：

| 操作 | 说明 |
|------|------|
| 新建标签 | 支持附注标签（带消息）与轻量标签，底部预览 `git tag -a` / `git tag` |
| 新建并推送 | 创建标签后立即推送到远程 |
| 删除标签 | 可同时删除本地与远程（勾选后预览含 `git push --delete`） |
| 推送标签 | 单标签或多标签（列表多选）推送 |
| 标签详情 | 查看附注标签的 tagger/消息，或轻量标签指向的提交 |

![新建标签：命令预览](evidence/e2e/12-tagops/03-create-tag-command-preview.png)

![删除标签：含远程删除两态预览](evidence/e2e/12-tagops/05-remove-tag-remote-delete.png)

![多标签推送](evidence/e2e/12-tagops/08-push-tags-multi.png)

![标签详情（附注标签）](evidence/e2e/12-tagops/09-tag-details-annotated.png)

---

## 11. 历史改写

历史改写操作集中在提交右键菜单，共 7 类窗口。所有窗口均有命令预览与安全确认。

### 合并（Merge）

选择要合并进当前分支的分支；可选 `--no-ff`；若检测到会产生冲突，
提交前弹出警告，可选择继续或取消。

![合并：默认预览](evidence/e2e/13-historyrewrite/01-merge-default-preview.png)

![合并：冲突预检警告](evidence/e2e/13-historyrewrite/03-merge-conflict-warning.png)

### 变基（Rebase）

把当前分支变基到目标分支，可开启 **autostash**（自动贮藏未提交改动，变基后恢复）。

![变基：autostash 预览](evidence/e2e/13-historyrewrite/05-rebase-autostash-preview.png)

### 交互式变基（Interactive Rebase）

完整图形化 todo 编辑：以真实 `git rebase -i` 启动辅助进程，
在窗口中把提交行的操作下拉改为 pick / reword / edit / squash / drop，
提交后由 git 执行改写。

![交互式变基 todo 列表](evidence/e2e/13-historyrewrite/15-ir-todolist-loaded.png)

![将某提交改为 Drop](evidence/e2e/13-historyrewrite/16-ir-drop-selected.png)

### 拣选（Cherry-pick）

把选中提交（支持多选列表模式）复制到当前分支；可选 `--no-commit`。

![拣选：多提交列表模式](evidence/e2e/13-historyrewrite/08-cherry-pick-multi.png)

### 还原（Revert）

生成反向提交撤销指定提交；可选 `--no-commit`。

![还原预览](evidence/e2e/13-historyrewrite/09-revert-preview.png)

### 重置（Reset）

soft / mixed / hard 三种模式，预览区实时显示对应命令与影响说明；
hard 模式有醒目警示。

![重置：hard 模式预览](evidence/e2e/13-historyrewrite/11-reset-hard-preview.png)

### Reflog（引用日志）

列出本仓库全部引用日志条目；选中某条按 **Jump** 弹出确认框，
确认后执行 `git reset --hard` 回到该位置——误操作后悔药。

![Reflog 窗口](evidence/e2e/13-historyrewrite/12-reflog-window.png)

![Reflog 跳转确认](evidence/e2e/13-historyrewrite/13-reflog-jump-confirm.png)

---

## 12. 贮藏（Stash）与快照

### 贮藏（Stash）

工具栏 Stash 按钮或 `Ctrl+Shift+H` 打开保存窗口；侧边栏 Stashes 分组右键管理条目。

| 操作 | 说明 |
|------|------|
| 保存 | 可选消息与 **Stage new files**（把未跟踪文件一并入栈） |
| 部分保存 | 文件列表勾选，只贮藏所选文件（pathspec），其余改动留在工作区 |
| 应用 | **Apply**（保留条目）或 **Pop**（应用并删除条目）两态；`--index` 连暂存状态一并恢复 |
| 删除 | 单条（视图模式）或多条（列表模式）批量 drop |
| 重命名 | 预填当前消息，改后 `git stash rename` 生效 |

![保存贮藏：带消息](evidence/e2e/14-stash/02-save-message.png)

![部分保存：勾选文件](evidence/e2e/14-stash/04-partial-selection.png)

![应用：Pop 模式预览](evidence/e2e/14-stash/07-apply-pop.png)

### 快照（Snapshot）

快照（菜单 File → Save Snapshot）与贮藏的关键区别：

> **快照只记录，不打扰**——执行 `stash create + store` 后 **工作区保持原样**（改动不消失），
> 而普通 Stash 会清空工作区。适合"先存个保险点再继续改"的场景。

快照可通过 Undo（`Ctrl+Z`）历史恢复：切分支、reset、文件破坏等操作后，
从撤销栈一键恢复到快照时的 head/branch/工作区状态。

![保存快照窗口](evidence/e2e/19-patchsnapshot/07-save-snapshot.png)

---

## 13. 远程仓库交互

工具栏三个主按钮对应三大操作，均有确认窗口与命令预览：

### Fetch（`Ctrl+Shift+F`）

默认拉取 origin；**--all** 开关拉取全部远程。快速 Fetch（`Ctrl+Alt+Shift+F` 或 Ctrl+点击）跳过窗口。

![Fetch：--all 模式](evidence/e2e/15-remote/02-fetch-all.png)

### Pull（`Ctrl+Shift+L`）

显示 upstream（远程 + 远程分支 + 目标分支）三元组；**--rebase** 开关变 pull 为 pull --rebase。

![Pull：rebase 模式](evidence/e2e/15-remote/03-pull-rebase.png)

### Push（`Ctrl+Shift+P`）

- 默认推送当前分支到上游；新分支时切换到 **new** 项，可勾选 track（`--set-upstream`）；
- **force-with-lease** 强推开关，开启时显示醒目警告图标；
- 快速 Push（`Ctrl+Alt+Shift+P` 或 Ctrl+点击）跳过窗口。

![Push：force-with-lease 警告](evidence/e2e/15-remote/05-push-force.png)

![Push 新分支并跟踪](evidence/e2e/15-remote/07-push-newbranch.png)

### 编辑远程与自定义 refspec

仓库设置中的远程管理支持 **添加**（校验空值/重名）与 **编辑**（set-url / rename 双预览）；
自定义 refspec 窗口按 remote + branch 预填并输出 refspec 字符串。

![添加远程](evidence/e2e/15-remote/09-editremote-add.png)

![自定义 refspec](evidence/e2e/15-remote/11-custom-refspec.png)

---

## 14. 子模块与 Worktree

### 子模块

| 操作 | 说明 |
|------|------|
| 添加子模块 | 输入 URL 与路径；支持本地路径（自动处理 git≥2.38 的 file transport 安全限制） |
| 删除子模块 | 预览 deinit + rm 命令；含未提交改动的子模块会被强制移除 |
| 子模块 diff | Commit 视图选中子模块指针变动时显示：ahead 角标、未提交文件数、内部修订列表 |

![添加子模块](evidence/e2e/16-submodule-worktree/01-addsubmodule-ready.png)

![子模块 diff 视图](evidence/e2e/16-submodule-worktree/03-submodulediff.png)

### Worktree（工作树）

| 操作 | 说明 |
|------|------|
| 创建 worktree | 输入名字自动派生路径；新建分支或检出既有分支；占用中的分支会被拒绝 |
| 检出为 worktree | 从分支操作进入，检出既有分支到独立工作树 |
| 删除 worktree | 预览后 `git worktree remove` |

创建成功后主窗口自动打开新标签页，每个 worktree 像独立仓库一样操作。

![创建 worktree](evidence/e2e/16-submodule-worktree/04-createworktree.png)

---

## 15. 工作流套件

菜单 Workflow 提供三种团队工作流。

### GitFlow

经典 Vincent Driessen 工作流，五步向导：

1. **Init**：检测 main 分支、生成 develop 与 feature/release/hotfix/support 前缀；
2. **Start Feature**：从 develop 拉出 `feature/<name>`；
3. **Finish Feature**：合并回 develop（可选 `-r` rebase 合并、`--no-ff`、`-k` 保留分支）；
4. **Start / Finish Release**：从 develop 拉出发布分支，完成时打 tag 并合并回 main 与 develop；
5. **Start / Finish Hotfix**：从 main 拉出修复分支，完成时打 tag 并合回 main。

![GitFlow Init](evidence/e2e/17-workflows/02-gitflow-init-ready.png)

![Start Feature](evidence/e2e/17-workflows/04-gitflow-start-feature-ready.png)

![Finish Feature](evidence/e2e/17-workflows/05-gitflow-finish-feature-ready.png)

![Finish Release](evidence/e2e/17-workflows/07-gitflow-finish-release-ready.png)

### git-mm

多仓管理工具 git-mm 的集成：Init 向导（三必填校验 + 目标目录非空拦截），
`.mm` 目录可直接作为 GitMm 工作区打开（独立标签页类型）。

![git-mm Init](evidence/e2e/17-workflows/10-gitmm-init-ready.png)

![GitMm 工作区标签页](evidence/e2e/17-workflows/12-gitmm-workspace-tab.png)

### LeanBranching

轻量分支流：**Start** 建支切支，**Finish** 校验后合回主干（main 分支可在仓库设置中配置）；
另有 **Sync** 一键同步。Start/Finish 窗口见[分支操作](#9-分支操作)。

---

## 16. Git LFS

菜单 LFS 提供大文件管理（需系统安装 git-lfs）：

| 窗口 | 说明 |
|------|------|
| Track | 添加跟踪模式（如 `*.psd`），实时预览将匹配到的文件，提交写入 `.gitattributes` |
| Status | 列出全部 LFS 文件；支持 **Lock / Unlock**（对接 LFS locks API），显示锁定者与过滤 |
| Fetch | 拉取 LFS 对象到本地存储（工作区仍是指针） |
| Pull | 拉取并把指针 smudge 成真实文件内容 |
| Init / Deinit | 安装/移除 LFS 钩子 |
| Prune | 清理本地无引用的 LFS 对象 |

![LFS Track：匹配文件预览](evidence/e2e/18-lfs/01-track-preview.png)

![LFS Status：文件锁定状态](evidence/e2e/18-lfs/03-status-locked.png)

---

## 17. 补丁

### 保存为补丁（Save as Patch）

选中提交（范围选择支持双提交）导出 `.patch` 文件：
单修订 `Revision:` 标签 / 范围 `Revisions:` 标签，路径含空格自动加引号。

![保存为补丁（范围模式）](evidence/e2e/19-patchsnapshot/03-save-as-patch-range.png)

### 应用补丁（Apply Patch）

两种来源：

- **文件**：浏览选择 `.patch`；无 `From` 头时折叠"创建提交"选项，走 `git apply`；
- **剪贴板**：粘贴补丁内容；有 `From` 头时可勾选用 `git am` 直接生成提交。

应用前自动 `apply --check` 冲突检测，冲突时状态条告警。

![应用补丁：冲突检测警告](evidence/e2e/19-patchsnapshot/05-apply-patch-conflict.png)

![应用剪贴板补丁（git am 模式）](evidence/e2e/19-patchsnapshot/06-apply-patch-clipboard.png)

---

## 18. 查看类窗口

| 窗口 | 入口 | 能力 |
|------|------|------|
| Blame | 文件右键 → Blame | 逐行归属：修订下拉切换历史版本、时间线、行级作者徽章、SHA 跳转 |
| 文件历史 | 文件右键 → History | 该文件的提交历史 + diff 预览；文件夹模式支持展开全部子文件 |
| 仓库概览 | 菜单 View → Repository Overview | 提交-文件树状图：根节点联动提交列表、作者列表、文件名 |
| 仓库统计 | 菜单 View → Repository Statistics | 作者维度的提交聚合统计 |
| 修订详情独立窗 | 提交右键 → Open in separate window | 把修订详情拆到独立窗口（标题 = sha7 + subject） |
| 跳转到行 | 编辑器内 `Ctrl+G` | 输入行号跳转（空/非数字拒绝，边界钳制） |

![Blame 视图](evidence/e2e/20-viewerwindows/01-blame-multicommit.png)

![文件历史（文件夹模式）](evidence/e2e/20-viewerwindows/04-filehistory-directory.png)

![仓库概览](evidence/e2e/20-viewerwindows/05-repository-overview.png)

![仓库统计](evidence/e2e/20-viewerwindows/06-repository-statistics.png)

---

## 19. 偏好设置

`Ctrl+,` 打开，共 7 个标签页，设置持久化到用户目录。

### General

- 提交排序方式（RevisionSortOrder 单选）；
- 界面**语言**（ComboBox 切换后立即重新本地化，无需重启）；
- 各类行为开关与代码编辑器字号（3-99，越界自动钳制）。

![General 标签页](evidence/e2e/21-preferences/01-general-tab.png)

![语言切换](evidence/e2e/21-preferences/02-language-combo.png)

### Commit

拼写检查三态（Disable / System / English）、提交消息长度指示线、分页线位置、
提交消息正则校验（非法数字自动回退默认值 72）。

![Commit 标签页](evidence/e2e/21-preferences/03-commit-tab.png)

### Git

- **Git 实例**：内置实例 / System PATH / 环境变量（forkgitinstance）；
- **全局身份**：user.name / user.email，失焦即写入 `git config --global`；
- git-mm / git-ai 可执行文件路径；AI 归属与 verbose 开关。

![Git 标签页](evidence/e2e/21-preferences/04-git-tab.png)

### Integration

终端 Shell 选择（Default / Windows Terminal / Command Prompt / PowerShell / Custom），
切换联动路径与参数输入框；是否显示 bugtracker 链接。

![Integration 标签页](evidence/e2e/21-preferences/05-integration-tab.png)

### Custom Commands（自定义命令）

添加作用于 **修订（Revision）** 或 **仓库（Repository）** 的自定义命令；
自动命名计数、目标类型联动、按名排序持久化到 `custom-commands.json`，支持删除（带确认）。
自定义命令会出现在对应右键菜单里。

![自定义命令](evidence/e2e/21-preferences/06-custom-commands.png)

### AI Review

AI 服务地址（自动归一化 `/v1` 等后缀）、重试次数与超时（带下限钳制）、
自定义 AI 技能列表（添加/更新/删除，JSON 持久化）。

![AI Review 标签页](evidence/e2e/21-preferences/07-ai-review-tab.png)

### Import / Export

把全部设置导出为 zip / 从 zip 导入（导入后可选择重启生效）。

![导入导出标签页](evidence/e2e/21-preferences/08-import-export-tab.png)

---

## 20. 仓库设置

每个仓库独立的设置窗口，共 4 个标签页。

### General

- **仓库身份**：默认使用全局身份；勾选"本地身份"后填写仅本仓库生效的 user.name/email；
- **主分支**（LeanBranching 用）：develop/main/master 自动探测或手动选择；
- No fast forward 开关；Tab 宽度。

### Commit

- **提交模板**：取消"使用全局模板"后编辑，写入 `commit_msg_template.txt` 并配置
  `commit.template`；重勾全局则解除本地模板；
- Sign-off、跳过消息正则（联动启停）。

### Issue Tracker

应用级未开启时整页回退提示；仓库级勾选启用后配置规则（如
`https://github.com/user/repo/issues/{0}`），提交消息中的 `#123` 自动链接。

![Issue Tracker 规则配置](evidence/e2e/22-repository-settings/04-issue-tracker-rule.png)

### Custom Commands（本地模式）

与偏好设置的自定义命令同构，但只作用于当前仓库（可与全局同名共存）。

![仓库设置 General](evidence/e2e/22-repository-settings/01-general-tab.png)

---

## 21. SSH 密钥与环境

菜单 File → Configure SSH Keys 打开 SSH 管理窗口：

- **密钥列表**：自动发现本地密钥；选中显示路径、公钥、SHA256 指纹三字段；
- **启用密钥**：勾选后 ForkPlus 使用该密钥（多密钥时配置文本逗号连接并提示）；
  勾选即触发真实 `ssh-keygen -y` 验证，失败自动回退；
- 未勾选任何密钥时回退系统 ssh-agent；
- **生成新密钥**：输入名称/邮箱（重名告警），预览 ssh-keygen 命令，
  真实生成 ed25519 密钥对并可直接启用。

![SSH 密钥列表与详情](evidence/e2e/23-ssh-environment/02-ssh-keys-list.png)

![生成新密钥](evidence/e2e/23-ssh-environment/03-generate-new-ssh-key.png)

### Git 实例

选择 ForkPlus 使用的 git：内置实例或 System PATH（候选列表真实执行 `git --version` 验证）。

![Git 实例选择](evidence/e2e/23-ssh-environment/04-git-instance.png)

### Workspaces（工作区）

管理仓库分组：默认 Home / Work 两个分组，可添加、改名（`Enter` 提交）、删除
（至少保留 2 个，删除带确认），每个分组可控制是否显示在标题中。
欢迎页的仓库列表即按工作区分组。

![Workspaces 管理](evidence/e2e/23-ssh-environment/05-workspaces.png)

---

## 22. AI 功能

> AI 功能兼容 OpenAI Chat API（`/v1/chat/completions`，Bearer 鉴权，SSE 流式），
> 在偏好设置 → AI Review 中配置服务地址后可用。

### AI 提交组合（AI Commit Composer）

把已暂存改动交给 AI 分析，流式返回**按文件分组的多条提交建议**（WIP 拆分）：
审查分组结果后可 **Apply All** 一键生成多条提交，也可逐组采纳。

![AI 提交组合：分组建议](evidence/e2e/24-ai/01-composer-groups.png)

![AI 提交组合：文件分组详情](evidence/e2e/24-ai/02-composer-doc-group.png)

### AI 代码审查（AI Code Review）

两种审查范围：

- **Files**：审查当前已暂存的改动；
- **Branch**：审查 `merge-base..HEAD` 的整分支改动（标题与状态栏显示范围）。

结果以 Markdown 渲染呈现。

![AI 代码审查：Files 范围](evidence/e2e/24-ai/03-codereview-files.png)

![AI 代码审查：Branch 范围](evidence/e2e/24-ai/04-codereview-branch.png)

### AI 开发助手（AI Development）

多轮对话式助手：

- 模型列表启动时后台拉取；
- 用户消息与 AI 流式响应以气泡呈现；对话历史落盘；
- 队列管理：发送后可 **Stop** 中断生成、**Clear** 清空会话。

![AI 开发助手：多轮对话](evidence/e2e/24-ai/06-dev-multiturn.png)

---

## 23. 通用工具与对话框

### 通知中心

主窗口右上角铃铛图标（无账号体系时隐藏）；各类后台事件（fetch 完成、命令失败等）
汇入通知面板集中查看。

![通知面板](evidence/e2e/25-common-dialogs/11-notification-panel.png)

### 消息与错误框

三形态消息框（确认/警告/错误），`Esc` 快捷关闭、Enter 走 Submit；
错误窗口除展示 git 原始输出外，对 `index.lock` 场景提供一键修复（删除锁文件）。

![标准消息框](evidence/e2e/25-common-dialogs/01-messagebox-standard.png)

![仓库锁定错误的修复按钮](evidence/e2e/25-common-dialogs/04-errorwindow-locked.png)

### 凭据询问（AskPass）

远程操作遇认证时按提示类型分流：用户名密码 / SSH 口令短语 / SSH 用户+口令，
支持"记住"选项。

![SSH 口令短语询问](evidence/e2e/25-common-dialogs/05-askpass-passphrase.png)

### 自定义颜色

30 项界面颜色可编辑（实时预览），**Reset All** 恢复默认。

![自定义颜色窗口](evidence/e2e/25-common-dialogs/06-customcolors.png)

### 性能诊断

实时采样列表（UI/后台耗时），支持 Refresh 与一键复制到剪贴板。

![性能诊断窗口](evidence/e2e/25-common-dialogs/10-diagnostics.png)

### 更新检查

菜单 Help → Check for Updates：检查中 → 结果面板（新版本/已是最新/网络失败三种终态）。

![更新检查结果](evidence/e2e/25-common-dialogs/09-updatecheck-result.png)

### 关于

版本号、版权与 Logo。

![关于窗口](evidence/e2e/25-common-dialogs/08-about.png)

---

## 24. 键盘快捷键大全

菜单 Help → Keyboard Shortcuts（或 `?`）打开快捷键参考窗口。完整清单：

### 通用导航

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+1` | 切换到 Changes 视图（再按聚焦提交消息框） |
| `Ctrl+2` | 切换到 All Commits 视图（再按跳到 HEAD） |
| `Ctrl+0` | 定位到 HEAD |
| `Ctrl+P` | 快速启动（Quick Launch） |
| `Ctrl+Tab` | 下一个标签页 |
| `Ctrl+Shift+Tab` | 上一个标签页 |
| `Ctrl+T` | 新建标签页 |
| `Ctrl+W` | 关闭当前标签页 |
| `Ctrl+=` / `Ctrl+-` | 放大 / 缩小界面 |
| `Ctrl+,` | 打开偏好设置 |

### All Commits 视图

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+0` | 跳到 HEAD |
| `Ctrl+F` | 提交搜索 |
| `Enter` / `F3` | 下一个搜索结果 |
| `Shift+Enter` / `Shift+F3` | 上一个搜索结果 |
| `Ctrl+C` | 复制提交信息 |
| `Delete` | 删除分支/贮藏 |
| `Ctrl+Shift+A` | 按当前分支过滤 |

### Changes 视图

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+Enter` | 提交 |
| `Ctrl+Shift+Enter` | 提交并推送 |
| `Ctrl+1` | 聚焦提交消息框 |
| `Ctrl+F` | 过滤文件 |
| `Enter` / `Ctrl+Shift+S` | 暂存/取消暂存选中文件（或选中行） |
| `Ctrl+Alt+Shift+S` | 暂存/取消暂存全部文件 |
| `Backspace` / `Ctrl+Shift+D` | 丢弃选中文件（或选中行） |
| `Ctrl+O` | 打开选中文件 |
| `Ctrl+D` | 用外部 diff 工具打开 |
| `Ctrl+C` | 复制选中文件完整路径 |

### 仓库操作

| 快捷键 | 功能 |
|--------|------|
| `F5` | 刷新 |
| `Ctrl+Shift+N` | 初始化新仓库 |
| `Ctrl+N` | 克隆仓库 |
| `Ctrl+G` | 初始化 git-mm 仓库 |
| `Ctrl+O` | 打开仓库 |
| `Ctrl+Shift+F` | Fetch |
| `Ctrl+Alt+Shift+F` / `Ctrl+点击` | 快速 Fetch |
| `Ctrl+Shift+L` | Pull |
| `Ctrl+Alt+Shift+L` / `Ctrl+点击` | 快速 Pull |
| `Ctrl+Shift+P` | Push |
| `Ctrl+Alt+Shift+P` / `Ctrl+点击` | 快速 Push |
| `Ctrl+Shift+B` | 新建分支 |
| `Ctrl+Shift+T` | 新建标签 |
| `Ctrl+Shift+H` | 创建贮藏 |
| `Ctrl+Alt+O` | 在文件管理器中打开 |
| `Ctrl+Alt+T` | 在终端中打开 |

### 仓库管理器

| 快捷键 | 功能 |
|--------|------|
| `F2` | 重命名仓库 |
| `Delete` | 移除仓库 |
| `Enter` | 打开仓库 |

### Diff 与编辑器

| 快捷键 | 功能 |
|--------|------|
| `Space` | 打开/关闭 diff 弹窗 |
| `Escape` | 关闭弹窗 |
| `↑` / `↓` | diff 弹窗内切换上/下一个文件 |
| `Ctrl+G` | 跳转到行 |

---

## 25. 常见问题

**Q：界面语言如何切换？**
偏好设置 → General → Language 下拉选择，切换后立即生效（无需重启）。

**Q：操作失误了怎么撤销？**
`Ctrl+Z` 撤销（支持跨操作：提交、reset、切分支等均入撤销栈，快照恢复走同一条栈）；
历史级误操作用 Reflog 窗口（提交列表状态栏开启 Reflog 后选中条目 Jump）。

**Q：合并冲突了怎么办？**
Changes 视图选中冲突文件进入内联视图逐块选择，或双击进入三方并排编辑器
（见[第 8 章](#8-解决合并冲突)）。逐块解决时 Resolve 按钮显示剩余块数，全部解决后自动暂存。

**Q：只想暂存一个文件里的几行怎么办？**
在 diff 中选中目标行，点浮出的 **Stage** 按钮（或 `Ctrl+Shift+S`），
只有选中行进入暂存区，其余改动留在工作区。

**Q：为什么我的子模块操作报 "transport 'file' not allowed"？**
git ≥ 2.38 对本地路径子模块有安全限制。ForkPlus 已内置规避（自动加
`-c protocol.file.allow=always`），使用旧版本程序或外部 git 时才会遇到。

**Q：SSH 密钥放在哪里能被识别？**
默认 `~/.ssh/` 目录；在 SSH 管理窗口勾选启用后，ForkPlus 对远程操作自动注入该密钥。
带口令的密钥会在连接时弹出 AskPass 询问。

**Q：AI 功能怎么配置？**
偏好设置 → AI Review：填入兼容 OpenAI Chat API 的服务地址与密钥（Token 走 Bearer）。
提交组合、代码审查、开发助手三个入口共用此配置。

**Q：报告性能问题需要提供什么？**
菜单打开性能诊断窗口 → Refresh → Copy，把采样数据粘贴给开发者。

**Q：在哪里能看到每个操作实际执行的 git 命令？**
几乎所有 Git 操作窗口底部都有**命令预览区**，提交前实时显示完整命令。
也可以用外部 diff 工具对照学习（`Ctrl+D`）。

---

> 本手册随版本演进持续更新。功能对应自动化测试证据见 `docs/evidence/e2e/`，
> 测试计划与模块覆盖明细见 `docs/e2e-ui-test-plan.md`。
