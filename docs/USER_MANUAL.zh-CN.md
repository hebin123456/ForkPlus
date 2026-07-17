# ForkPlus 用户手册

> 版本：2.1.0
> 适用于 Windows 10+ / .NET 4.7.2 / Git 2.31+（推荐 git-mm 3.0+）

ForkPlus 是基于 Fork 的 Git 图形化工具增强版，底层用 Rust 重写（biturbo 引擎），内置 AI 辅助开发、代码统计、仓库树图、多主题皮肤、8 种语言等特色功能。本手册按功能模块组织，帮助你快速上手每个功能。

> 注：手册中的菜单/按钮名称使用软件界面英文标签（软件默认语言为英文，切换到简体中文后界面会翻译为中文）。

---

## 目录

1. [主界面概览](#1-主界面概览)
2. [仓库管理](#2-仓库管理)
3. [分支与引用](#3-分支与引用)
4. [提交与变更](#4-提交与变更)
5. [Diff 与合并](#5-diff-与合并)
6. [AI 辅助开发](#6-ai-辅助开发)
7. [AI 代码审查](#7-ai-代码审查)
8. [仓库统计与可视化](#8-仓库统计与可视化)
9. [多主题与自定义颜色](#9-多主题与自定义颜色)
10. [多语言支持](#10-多语言支持)
11. [工作区](#11-工作区)
12. [自定义命令](#12-自定义命令)
13. [快速启动](#13-快速启动)
14. [外部工具集成](#14-外部工具集成)
15. [设置](#15-设置)
16. [快捷键](#16-快捷键)
17. [Git Flow 与 Lean Branching](#17-git-flow-与-lean-branching)

---

## 1. 主界面概览

ForkPlus 主窗口分为四个区域：

```
┌─────────────────────────────────────────────┐
│  标题栏菜单（File / View / Repository / Window / Help）  │
├─────────────────────────────────────────────┤
│  工具栏（Fetch/Pull/Push/Stash/Branch/Appearance/Workspaces/Open in/AI）│
├──────────┬──────────────────────────────────┤
│          │                                  │
│  侧栏     │        主区域                    │
│ (Branches/│   (提交列表 + Diff 详情)          │
│  Search/PR)│                                 │
│          │                                  │
├──────────┴──────────────────────────────────┤
│  底部状态栏                                   │
└─────────────────────────────────────────────┘
```

- **标题栏菜单**：File / View / Repository / Window / Help 五大菜单，懒加载（点击才构建）
- **工具栏**：日常 Git 操作 + 外观切换 + 工作区切换 + 外部打开 + AI 入口
- **侧栏**：分支/远端/标签/stash/子模块/worktree 导航，顶部三选一（Branches / Search Commits / Pull Requests）
- **主区域**：提交列表（All Commits 视图）或变更列表（Changes 视图）+ Diff 详情
- **支持多标签页**：可同时打开多个仓库，Ctrl+T 新建标签页，Ctrl+W 关闭

---

## 2. 仓库管理

### 打开仓库
- **File → Open Repository**（Ctrl+O）：选择本地仓库目录
- **拖拽**：把仓库文件夹拖到主窗口
- **快速启动**：Ctrl+P 打开命令面板，搜索仓库名直接打开
- **File → Create Repository**（Ctrl+Shift+N）：初始化新仓库
- **File → Clone**：克隆远程仓库
- **File → Init GitMm Repository**（Ctrl+G）：初始化 git mm 工作流仓库（需 git-mm 3.0+）

### 多标签页
- 每个仓库一个标签页，Ctrl+Tab / Ctrl+Shift+Tab 切换前后标签
- Ctrl+T 新建空白标签，Ctrl+W 关闭当前标签
- 窗口位置和状态自动持久化

### 后台自动 Fetch
- 默认开启后台 fetch，自动获取远端更新（不自动 merge）
- 可在设置中调整频率或关闭

---

## 3. 分支与引用

侧栏左侧是引用导航，包含 7 个分组：

| 分组 | 内容 |
|---|---|
| **Pinned** | 置顶的常用分支 |
| **Branches** | 本地分支 |
| **Remotes** | 远端分支 |
| **Tags** | 标签 |
| **Stashes** | stash 列表（最近 15 个） |
| **Submodules** | 子模块 |
| **Worktrees** | worktree |

### 排序与过滤
- 每个分组可按字母正序/倒序/最近使用排序
- 顶部 Filter 框（Ctrl+F 聚焦，Esc 清空）实时过滤
- 超过 20 项时折叠，点 "Show all" 展开

### 双击行为
- 分支/Tag：Checkout
- Stash：Apply
- Submodule/Worktree：打开

### 右键菜单（本地分支）
Checkout、Fast-Forward、Pull、Push、Create PR、Check Remote Sync Status（二级可搜索菜单）、Merge、Rebase、Interactive Rebase、Tracking、Rename、Delete、AI Code Review、Code statistics、Copy Name、Custom Commands

### 拖拽操作
- 拖分支到本地分支：弹出 Merge / Rebase 菜单
- 拖本地分支到远端分支：弹出 Rebase 菜单

### 创建分支/标签/Worktree
- 工具栏 Branch 下拉按钮 → Create Branch / Create Tag / Create Worktree
- Repository 菜单 → Create Branch（Ctrl+Shift+B）/ Create Tag（Ctrl+Shift+T）

---

## 4. 提交与变更

切换到 **Changes 视图**（侧栏顶部 "Changes" 或 Ctrl+1）进入提交面板。

### 文件暂存
- **未暂存区**：显示工作区改动
- **已暂存区**：显示将要提交的改动
- **Stage / Unstage**：双击文件切换，或 Ctrl+S（stage）/ Ctrl+Shift+S（unstage）/ Ctrl+Alt+Shift+S（全部 stage）
- **Discard**：Backspace 或 Ctrl+Shift+D 丢弃改动
- 超 5000 文件触发长操作提示

### Commit Message
- **Subject + Description**：空行分隔，自动识别
- **Gitmoji 补全**：输入 `:` 触发 emoji 选择器
- **历史补全**：基于过往提交自动补全
- **Reference 名补全**：输入分支/tag 名自动补全
- **prepare-commit-msg hook**：支持

### 提交模式
- **普通提交**：Ctrl+Enter
- **Commit + Push**：Ctrl+Shift+Enter（按 Shift 反转默认 `PushAutomaticallyOnCommit` 行为）
- **Amend**：勾选 Amend 复选框，修订上一次提交
- **Squash**：基于进行中的 squash 状态

### Cherry-pick / Revert / Reset
- 在提交列表右键某个 commit：
  - **Cherry-pick**：把该 commit 的改动应用到当前分支
  - **Revert**：创建一个反向提交
  - **Reset**：把当前分支重置到该 commit（soft/mixed/hard）

---

## 5. Diff 与合并

### Diff 视图
ForkPlus 提供多种 diff 视图模式：
- **单文件文本 diff**：上下对比
- **并排 diff**（Side by Side）：左右两栏
- **分屏 diff**：Split 模式
- **带语法高亮的 diff**：DiffCodeEditor，支持代码着色

### 外部 Diff 工具
- 选中文件按 **Ctrl+D** 调用配置的外部 diff 工具
- 在 Preferences → Integration 中配置

### 合并冲突
- 遇到 `ChangeType.Unmerged` 时自动弹出可用合并工具列表
- 支持 VS Code、Beyond Compare、KDiff3 等常见工具

### Apply Patch
- **Ctrl+V** 粘贴时自动识别 `diff ` 或 `From ` 前缀，触发 Apply Patch 流程

---

## 6. AI 辅助开发

ForkPlus 内置多轮对话式 AI 编程助手，可让 AI 直接读写当前仓库文件。

### 入口
工具栏 **AI-Assisted Development** 按钮（未配置 API 时弹提示引导到设置）

### 前置配置
Preferences → AI Enhancement：填写 API endpoint、API key、默认模型

### 核心能力
- **多轮对话**：保留最近 20 条历史，超长时自动压缩摘要
- **流式输出**：实时渲染 AI 回复（400ms 节流）
- **请求队列**：处理中可继续输入排队，显示队列数
- **模型选择**：顶部下拉框，后台拉取 `/v1/models` 列表
- **直接读写文件**：AI 用 `===FILE: path===` + 代码块格式输出，自动写入磁盘
- **路径安全**：限定当前仓 + 父仓 + 兄弟子模块 + 自身子模块，防 path traversal
- **换行符归一化**：自动检测并保持原文件换行风格
- **撤销**：点 Undo 按钮用 `_lastBeforeContents` 回写，撤销 AI 的改动
- **Clear Conversation**：清空对话历史和界面

### 使用方式
1. 点工具栏 AI 按钮打开对话窗口
2. 输入需求（如"给 utils.py 加个日志装饰器"）
3. AI 流式输出代码并写入文件
4. 查看 diff，满意则保留，不满意则 Undo

---

## 7. AI 代码审查

针对分支区间、SHA 区间或文件列表进行 AI 代码审查，提取建议并支持预览/应用。

### 入口
侧栏本地分支或远端分支 **右键 → AI Code Review**

### 三种审查目标
- **Branch**：src 分支..dst 分支的改动
- **ShaRange**：指定起止 SHA 区间
- **Files**：选定文件列表

### 核心能力
- **流式渲染**：实时显示审查报告（Markdown→HTML，用 native 引擎渲染）
- **建议提取**：解析 ```forkplus-ai-suggestions JSON 块，提取 file/line/comment/oldText/newText
- **预览建议**：点击建议项预览改动
- **应用建议**：点 Apply 直接修改文件
- **文件审查模式**：左侧文件树 + 右侧 diff，按文件过滤显示建议
- **重试**：对单个文件或全部重试审查
- **完成通知**：审查完成发 Windows toast 通知
- **窗口布局持久化**：位置、列宽自动保存

---

## 8. 仓库统计与可视化

### 8.1 仓库统计面板

入口：Repository 菜单 → Repository Statistics

包含：
- **作者提交折线图**（Top 20 作者）
- **作者提交饼图**
- **按星期柱状图**（提交活跃度）
- **按小时柱状图**（一天中提交分布）
- **贡献热力图**（GitHub 风格 53×7 网格，颜色深浅表示当天提交数）
- **日期范围筛选**：DateRangeButton 过滤统计范围

所有图表随主题切换自动刷新颜色。

### 8.2 代码行数统计（tokei 集成）

统计面板下方 "Code lines" 区域，集成 [tokei](https://github.com/XAMPPRocky/tokei)（Rust 代码统计工具，支持 200+ 语言）。

- **Ref 下拉**：Workspace（当前工作区快照）/ 本地分支 / tag
- **搜索框**：Popup 顶部，过滤分支/tag（Workspace 项始终保留）
- **饼图**：按语言代码行数 Top 12 + Other
- **列表**：Language / Files / Code / Comments / Blanks / 色块
- **摘要**：`{ref}: {files} files · {code} code · {comments} comments · {blanks} blanks`

历史 ref 模式用 `git archive` 导出快照再跑 tokei，不污染工作区。

分支右键 → **Code statistics** 可直接以该分支为 ref 打开统计。

### 8.3 仓库树图（Repository Treemap）

入口：Repository 菜单 → Repository Overview

**什么是 Treemap**：一种矩形树图可视化，画布被递归切割成多个小矩形，每个矩形面积 ∝ 它代表的数值，按文件夹/文件层级嵌套。

**这里展示什么**：
- 以文件/文件夹路径为分组层级
- **每个矩形的面积 ∝ 该路径被 commit 修改的次数**（不是文件大小）
- 一眼看出代码库的改动集中在哪里

**使用方式**：
- 鼠标悬停显示路径 + commit 次数
- 点击矩形钻取，右侧显示该路径的 commits 列表 + 作者统计
- DateRangeButton 按日期范围过滤

**举例**：仓库 100 个 commit，`src/` 被 80 个改过则占画面 80%，`docs/` 15 个占 15%，`README.md` 5 个占 5%——矩形越大 = 改动越活跃。

---

## 9. 多主题与自定义颜色

### 9.1 预设主题（12 套）

入口：工具栏 Appearance 下拉 → 主题菜单；或 Window 菜单 → Switch Theme；Ctrl+Click 工具栏外观按钮快速切换

| 主题 | 基底 | 风格 |
|---|---|---|
| Light | 浅 | 默认浅色 |
| Dark | 深 | 默认深色 |
| Solarized Light | 浅 | Solarized 经典配色 |
| Solarized Dark | 深 | Solarized 深色 |
| GitHub Light | 浅 | GitHub 官方浅色 |
| GitHub Dark | 深 | GitHub 官方深色 |
| Dracula | 深 | 深紫黑底 + 粉紫 accent |
| Monokai | 深 | 经典 Monokai 深灰底 + 绿橙 accent |
| Purple Light | 浅 | 紫色浅色 |
| Purple Dark | 深 | 紫色深色 |
| Green Light | 浅 | 绿色浅色 |
| Green Dark | 深 | 绿色深色 |

主题切换即时生效，所有控件（包括 diff 高亮、行号、热力图、语法着色）跟随更新。

### 9.2 自定义颜色

入口：工具栏 Appearance → **Custom Colors...**（可勾选项）

**功能**：在任意预设主题基础上覆盖 30 个核心颜色，包括：
- 背景、面板、边框、文字、强调色、图标色
- Diff 新增/删除色、精确新增/删除色
- 行号色、分隔线色、代码块选区色
- 语法高亮（注释/字符串/关键字/数字）
- 编辑器背景/前景、窗口背景、标题栏背景

**使用方式**：
1. 点 Custom Colors... 打开编辑对话框
2. 每个颜色支持 hex 输入 + HSV 调色盘（饱和度×明度 2D 方块 + 色相条）
3. **改动即时生效**，无需重启
4. **随机配色**：底部 "Random Palette" 按钮，基于随机主色相 + 互补色算法生成一套搭配合理的配色（背景/文字/accent/diff/语法等全套派生），保证色调统一可读
5. **Reset**：单项重置 / Reset All 全部重置
6. OK 保存，Cancel 撤销所有改动

**互斥语义**：
- 启用自定义颜色后，菜单中"自定义颜色"项勾选，所有主题项不勾选
- 切换主题时自动关闭自定义颜色覆盖，使用新主题原色（自定义配置保留，可重新勾选恢复）

---

## 10. 多语言支持

入口：工具栏 Appearance → Language 菜单

内置 8 种语言：
- English
- 简体中文
- 繁體中文
- 日本語
- 한국어
- Français
- Deutsch
- Español

基于 JSON 的可扩展系统，可在 `Languages/` 目录下添加新语言文件扩展更多语言。切换语言后整个界面即时更新。

---

## 11. 工作区

把多个相关仓库组织为工作区，便于快速切换。

### 入口
工具栏 **Workspaces** 下拉按钮；Ctrl+Click 快速切换

### 功能
- 工作区支持嵌套文件夹菜单
- 切换工作区后工具栏显示当前工作区名
- 集成到 Quick Launch 命令面板，Ctrl+P 搜索工作区名切换
- 工作区列表持久化到 settings.json

---

## 12. 自定义命令

用户可定义自己的命令，绑定到不同目标。

### 入口
- Preferences → Custom Commands Tab
- Repository 菜单 → Custom Commands
- 侧栏右键 → Custom Commands

### 5 种目标类型
| 目标 | 默认命令 | 说明 |
|---|---|---|
| Commit (Revision) | `git show ${sha}` | 对选中 commit 执行 |
| Repository | 打开 URL | 对整个仓库执行 |
| File (RepositoryFile) | `git diff ${file}` | 对选中文件执行 |
| Reference (Branch) | `git diff HEAD ${ref}` | 对分支/Tag 执行，支持 LocalBranch/RemoteBranch 子目标 |
| Submodule | `git submodule update --remote -- ${submodule}` | 对子模块执行 |

### 两种模式
- **Local**：per-repo，仅当前仓库可见，可设为 Shared 共享
- **Global**：全局，所有仓库可见

### 两种 Action
- **UI**：带按钮和控件
- **Process**：执行 shell 命令

支持 OS 限定（Any / Windows / Mac）。

---

## 13. 快速启动

类似 VS Code Command Palette 的命令面板。

### 入口
- 工具栏 Quick Launch 按钮
- File 菜单 → Quick Launch
- 快捷键 **Ctrl+P**

### 功能
- 多 Provider 命令检索：默认命令、Git Flow、引用、远端、仓库文件、工作区
- 输入关键词实时过滤
- Up/Down 导航，Enter 执行，Esc 关闭
- 支持带参数命令（如 Checkout Branch 后输入分支名）
- 后台扫描刷新仓库列表

### 隐藏命令
- 输入 `ftrace` → 开启耗时调试
- 输入 `crash` → 发送崩溃报告

---

## 14. 外部工具集成

工具栏 **Open in** 下拉按钮：

- **Console**：打开 Shell（默认或当前 ShellTool，Ctrl+Alt+T）
- **File Explorer**：资源管理器（Ctrl+Alt+O）
- **External Editors**：VS / VSCode / JetBrains 等已安装编辑器
- **Remote**：远程仓库网页视图
- **Custom Commands**：自定义命令

在 Preferences → Integration 中配置默认 Shell、外部编辑器、外部 diff/merge 工具。

---

## 15. 设置

入口：File → Preferences（Ctrl+,）

### 6 个 Tab

| Tab | 内容 |
|---|---|
| **General** | 通用设置（语言、主题、布局缩放、自动 fetch 等） |
| **Commit** | 提交相关（`PushAutomaticallyOnCommit`、`HideUntrackedFiles`、`SkipCommitMessage` 等） |
| **AI Enhancement** | AI 服务配置（API endpoint、API key、默认模型） |
| **Git** | Git 相关配置 |
| **Integration** | 外部工具、Shell、外部编辑器、diff/merge 工具 |
| **Custom Commands** | 自定义命令管理（详见第 12 节） |

### SSH 密钥
File → Configure SSH Keys：管理 SSH 密钥

### 账户
File → Accounts：配置 GitHub/GitLab 等账户（用于 PR、远程操作）

---

## 16. 快捷键

入口：Help → Open Keyboard Shortcuts

### 通用导航
| 快捷键 | 功能 |
|---|---|
| Ctrl+1 / Ctrl+2 / Ctrl+0 | 切换侧栏视图 |
| Ctrl+P | Quick Launch |
| Ctrl+Tab / Ctrl+Shift+Tab | 切换仓库 Tab |
| Ctrl+T | New Tab |
| Ctrl+W | Close Tab |
| Ctrl+= / Ctrl+- | 缩放 |
| Ctrl+, | Preferences |

### All Commits 视图
| 快捷键 | 功能 |
|---|---|
| Ctrl+0 | 激活 All Commits 视图 |
| Ctrl+F | Filter 聚焦 |
| Enter / F3 | 搜索下一个 |
| Shift+Enter / F3 | 搜索上一个 |
| Delete | 删除分支/Tag |

### Changes 视图
| 快捷键 | 功能 |
|---|---|
| Ctrl+1 | 切换到 Changes 视图 |
| Ctrl+F | Filter 聚焦 |
| Ctrl+Enter | 提交 |
| Ctrl+Shift+Enter | 提交 + Push |
| Enter / Ctrl+S | Stage |
| Ctrl+Shift+S | Unstage |
| Ctrl+Alt+Shift+S | Stage All |
| Backspace / Ctrl+Shift+D | Discard |
| Ctrl+O | 打开文件 |
| Ctrl+D | 外部 diff |

### 仓库操作
| 快捷键 | 功能 |
|---|---|
| F5 | Refresh |
| Ctrl+Shift+N | Create Repository |
| Ctrl+N | New Tab |
| Ctrl+G | Init GitMm Repository |
| Ctrl+O | Open Repository |
| Ctrl+Shift+F | Fetch |
| Ctrl+Alt+Shift+F | Quick Fetch |
| Ctrl+Shift+L | Pull |
| Ctrl+Alt+Shift+L | Quick Pull |
| Ctrl+Shift+P | Push |
| Ctrl+Alt+Shift+P | Quick Push |
| Ctrl+Shift+B | Create Branch |
| Ctrl+Shift+T | Create Tag |
| Ctrl+Shift+H | Stash |
| Ctrl+Alt+O | Open in File Explorer |
| Ctrl+Alt+T | Open in Console |

### 仓库管理器
| 快捷键 | 功能 |
|---|---|
| F2 | Rename |
| Delete | Remove |
| Enter | Open |

### 特殊
- **Ctrl+V**：粘贴 patch 时自动识别 `diff ` / `From ` 前缀，触发 Apply Patch
- **Ctrl+Click 工具栏外观按钮**：快速切换主题
- **Ctrl+Click 工具栏工作区按钮**：快速切换工作区
- **工具栏 Fetch/Pull/Push + Ctrl**：Quick 变体（不弹对话框直接执行）

---

## 17. Git Flow 与 Lean Branching

### Git Flow

完整的 Git Flow 工作流支持：

入口：工具栏 Branch 下拉 → Git Flow；或 Repository 菜单 → Git Flow

- **Init**：初始化 Git Flow 配置
- **Start Feature / Release / Hotfix**：开始新分支
- **Finish Feature / Release / Hotfix**：完成分支（合并回 develop/main）
- **Deinit**：移除 Git Flow 配置

### Lean Branching（git mm 工作流）

基于 git-mm 的轻量工作流，需 git-mm 3.0+：

入口：工具栏 Branch 下拉 → Lean Branching

- **Start**：基于 main 创建新工作分支
- **Sync**：同步 main 的最新改动到当前分支
- **Finish**：完成分支（合并并清理）

---

## 附录：技术架构

- **底层引擎**：biturbo（Rust 编写，P/Invoke 暴露给 .NET），提供 commit graph cache、treemap 布局、Markdown→HTML、commit 搜索等能力
- **代码统计**：集成 tokei（Rust，200+ 语言，构建期 cargo install 编译）
- **UI 框架**：WPF（.NET 4.7.2）
- **图表**：OxyPlot
- **自动更新**：Squirrel
- **协议**：MIT

---

如有问题或建议，可在 Help → About 查看版本信息，或通过项目仓库反馈。
