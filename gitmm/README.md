
# git mm 命令参考

## `git mm start`

`start` 用于从清单中指定的修订开始创建新的开发分支。

```bash
git mm start <branch> [--all | <project>...]
git mm start [flags]
```

### 参数

| 参数 | 说明 |
| --- | --- |
| `-a`, `--all` | 在所有项目中创建分支。 |
| `--allow-commit` | 允许从 commit 创建分支；隐含 `--allow-no-track`。 |
| `--allow-no-track` | 即使没有 tracking branch，也允许创建分支。 |
| `--allow-tag` | 允许从 tag 创建分支；隐含 `--allow-no-track`。 |
| `-g`, `--grep-mode <string>` | 搜索项目的 grep 模式，可选 `name(1)`、`path(2)`、`mixed(3)`、`namereg(4)`、`pathreg(5)`、`mixedreg(6)`、`underpath(7)`。默认：`mixed`。 |
| `--head` | 基于 `HEAD` 创建分支。 |
| `-h`, `--help` | 显示 `start` 帮助。 |
| `-j`, `--jobs <int>` | 同时 checkout worktree 的项目数量。默认：`8`。 |

## `git mm sync`

`sync` 用于将本地项目目录与清单中指定的远程仓库同步。

如果本地项目尚不存在，`sync` 会从远程仓库 clone 一个新的本地目录，并根据清单设置 tracking branch。如果本地项目已存在，`sync` 会更新远程分支，并将新的本地变更 rebase 到新的远程变更之上。

`sync` 会同步命令行中列出的所有项目。项目可以用名称、本地项目目录的相对路径或绝对路径指定。如果没有指定项目，则同步清单中列出的所有项目。

`-d` / `--detach` 选项会将指定项目切回清单修订。当项目当前位于 topic branch，但临时需要清单修订时，这个选项很有用。

默认会同步 groups 和 supergroups 配置中包含的所有项目。`--fail-fast` 可以在第一个项目失败时立即停止同步。

```bash
git mm sync [flags]
git mm sync [options] <project>...
```

别名：

```text
sync, pull, update
```

### SSH 连接说明

如果至少一个项目的远程 URL 使用 SSH（`ssh://`、`git@host:path` 或 `user@host:path`），`sync` 在连接该 host 时会自动启用 SSH ControlMaster 选项。这样同一次同步会话中的其他项目可以复用同一个 SSH tunnel。

在 UNIX 平台上，如需禁用此行为，可以将 `GIT_SSH` 环境变量设置为 `ssh`。

示例：

```bash
export GIT_SSH=ssh
```

由于 Windows 缺少 UNIX domain socket 支持，此功能在 Windows 上禁用。

如果远端 SSH daemon 是 Gerrit Code Review `2.0.10` 或更高版本，可能需要服务端协议修复。

### 参数

| 参数 | 说明 |
| --- | --- |
| `-a`, `--all-branches` | 拉取所有分支。 |
| `--auto-gc` | 对所有已同步项目运行垃圾回收。 |
| `-c`, `--change-id <string>` | 同步与该 change id 关联的所有变更请求。 |
| `-J`, `--checkout-jobs <int>` | 并行执行本地 checkout 的任务数量。默认：`4`。 |
| `--depth <int>` | fetch 深度。 |
| `-d`, `--detach` | 将项目 detach 回清单修订。 |
| `--fail-fast` | 遇到第一个错误后停止同步。 |
| `--fetch-submodules` | 从服务器 fetch submodule。 |
| `--force-checkout` | 强制 checkout 到 revision id；如果 checkout 失败，则 hard reset 到 revision id。警告：可能导致数据丢失。 |
| `--force-fetch` | 如果现有 Git 目录需要指向不同的 object 目录，则覆盖它。警告：可能导致数据丢失。 |
| `--force-lfs` | 强制 checkout LFS 对象；如果 LFS checkout 失败则快速失败。 |
| `--force-remove-dirty` | 如果项目已不在清单中，即使有未提交修改也强制删除。警告：可能导致数据丢失。 |
| `--force-sync` | 强制同步，等价于 `--force-fetch`、`--force-checkout`、`--force-remove-dirty`。 |
| `-g`, `--grep-mode <string>` | 搜索项目的 grep 模式，可选 `name(1)`、`path(2)`、`mixed(3)`、`namereg(4)`、`pathreg(5)`、`mixedreg(6)`、`underpath(7)`。默认：`mixed`。 |
| `-G`, `--group <string>` | 只同步指定 group 中的项目。默认：`all` 加 `G1,G2,G3,G4,-G5,-G6`。 |
| `-h`, `--help` | 显示 `sync` 帮助。 |
| `--hooks <string>` | 指定同步后要执行的 hooks，用 `,` 分隔。 |
| `--ignore-copylink-error` | 忽略 copy/link 文件错误。 |
| `--ignore-symlink-error` | 忽略 symlink 相关错误。 |
| `--ignore-git-clean-error` | 忽略由 `git-clean(1)` 引起的清理 worktree 错误。 |
| `-j`, `--jobs <int>` | 同时 fetch 的项目数量。默认：`8`。 |
| `-l`, `--local-only` | 只更新 worktree，不 fetch。 |
| `--manifest-name <string>` | 本次 sync 使用的本地 manifest，替代默认 manifest 文件。 |
| `--manifest-url <string>` | 要查询的 manifest 仓库 Git URL。 |
| `--match-branch` | 同步 super/root MR 时严格匹配目标分支。 |
| `--merge` | 不使用 rebase，改用 merge 更新工作分支。 |
| `-n`, `--network-only` | 只 fetch，不更新 worktree。 |
| `--no-clean` | 同步新 worktree 前不清理 worktree。 |
| `--no-git-clean` | 不使用 `git-clean(1)` 清理 worktree。 |
| `--no-progress-bar` | 连接到终端时，默认在 stderr 报告同步进度状态。 |
| `--no-prune` | 不删除远端已不存在的 refs。 |
| `--no-snapshot` | 不使用 snapshot 插件加速同步。 |
| `-N`, `--no-update-manifest` | 默认不更新 manifest；即使在 `--local-only` 模式，也会在 sync 前更新 manifest。 |
| `--no-update-repohooks` | 不更新 repo hooks。 |
| `--progress` | 连接到终端时，默认在标准错误流报告进度状态。 |
| `--progress-bar` | `--no-progress-bar` 的反向选项。 |
| `-R`, `--replace-prefix <strings>` | 如果项目全名以对应替换前缀开头，则用替换后的全名下载。 |
| `--restore` | 将 worktree 恢复到初始状态，并清理本地修改。警告：可能导致数据丢失。 |
| `--retry-fetches <int>` | 瞬时错误时重试 fetch 的次数。默认：`2`。 |
| `--skip-closed` | 不同步已关闭的提交/变更请求。 |
| `--skip-hooks` | 同步后不执行 hooks。 |
| `--skip-lfs` | 不 checkout LFS 文件。 |
| `--smart-sync` | 使用最新已知良好构建的 manifest 进行 smart sync。 |
| `--smart-tag <string>` | 使用已知 tag 的 manifest 进行 smart sync。 |
| `--stat` | 输出运行时统计信息。 |
| `-s`, `--super <int>` | 按 super/root MR id 同步。 |
| `--supergroup <string>` | 只同步指定 supergroup 中的项目。 |
| `--tags` | 同时 fetch tags。 |
| `--unshallow` | 使用 `--unshallow` fetch，移除 shallow repository 限制。 |

## `git mm upload`

`upload` 用于将变更发送到目标 Code Review 系统。它会在本地仓库中查找尚未发布评审的 topic branch。

如果找到多个 topic branch，`upload` 会打开编辑器，让用户选择要上传的分支。

项目可以用名称、本地项目目录的相对路径或绝对路径指定。如果没有指定项目，`upload` 会在清单中列出的所有项目中查找可上传的变更。

如果传入 `--reviewers` 或 `--cc`，这些邮箱会被添加到评审中。作为 reviewer 传入的用户必须已经在 Code Review 系统中注册，否则上传会失败。

使用 `--title` 和 `--description` 可以设置 merge request 的标题和描述。描述内容允许使用 Markdown。

```bash
git mm upload [flags]
git mm upload [options] <project>...
```

别名：

```text
upload, push
```

### 参数

| 参数 | 说明 |
| --- | --- |
| `--approvers <string>` | 请求这些人 approve，用 `;` 分隔。仅对 CodeHub CR 有效。 |
| `-A`, `--assignees <string>` | 请求这些人提交，用 `;` 分隔。仅对 CodeHub 有效。 |
| `--br <string>` | 要上传的分支。 |
| `--cbr` | 只上传当前分支。 |
| `--cc <string>` | 同时发送邮件给这些邮箱地址。 |
| `-D`, `--description <string>` | merge request 描述，会转换为 Markdown。仅对 CodeHub MR 有效。 |
| `--dest <string>` | 提交到该目标分支进行评审。 |
| `-f`, `--force` | 即使项目之前已经上传过，也强制上传每个项目。 |
| `-g`, `--grep-mode <string>` | 搜索项目的 grep 模式，可选 `name(1)`、`path(2)`、`mixed(3)`、`namereg(4)`、`pathreg(5)`、`mixedreg(6)`、`underpath(7)`。默认：`mixed`。 |
| `--hashtag <string>` | 为评审添加 hashtag，用逗号分隔。 |
| `--hashtag-branch` | 将本地分支名作为 hashtag。 |
| `--head` | 上传 `HEAD`，即使当前处于 detached 状态。 |
| `-h`, `--help` | 显示 `upload` 帮助。 |
| `--honor-no-changes` | 即使新提交中没有变更，也上传项目。 |
| `-j`, `--jobs <int>` | 同时上传项目的任务数量。默认：`8`。 |
| `-l`, `--label <string>` | 上传时添加 label。 |
| `--no-ssl-verify` | 禁用 SSL 证书校验。不安全。默认：`true`。 |
| `-N`, `--no-update-manifest` | 默认不更新 manifest；上传前会更新 manifest。 |
| `--push-option <string>` | 传递额外的 push options。 |
| `--ready` | 将变更标记为 ready，并清除 work-in-progress 设置。 |
| `-R`, `--reviewers <string>` | 请求这些人 review，用 `;` 分隔。 |
| `--ssl-verify` | 校验 SSL 证书。 |
| `-T`, `--title <string>` | merge request 标题。仅对 CodeHub MR 有效。 |
| `--topic <string>` | super MR 或 change request 的 topic。默认是本地分支。 |
| `--wip` | 以 work-in-progress 状态上传变更。 |

## 全局参数

| 参数 | 说明 |
| --- | --- |
| `-C`, `--dir <string>` | 按指定路径作为当前工作目录运行 `git-mm`。 |
| `--git-path <string>` | Git 可执行文件路径。 |
| `-q`, `--quiet` | 静默模式，控制是否显示详细 Git 命令输出。 |
| `--root-dir` | 显示当前 manifest 项目的根目录。 |
| `--timeout <string>` | 命令执行超时时间，可使用 `s/m/h` 后缀。默认无超时。 |
| `--trace` | 输出 trace 消息。 |
| `--verbose` | `--quiet` 的反向选项。 |
| `--verbosity-level <count>` | 终端日志详细级别。默认：`INFO`；`-v` 为 `DEBUG`；`-vv` 为 `TRACE`。 |
| `--version` | 显示 git-mm 版本。 |
| `-y`, `--yes` | 对终端问题自动回答 yes。 |
