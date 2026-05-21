
# git mm 命令參考

## `git mm start`

`start` 用於从清單中指定的修訂开始建立新的开发分支。

```bash
git mm start <branch> [--all | <project>...]
git mm start [flags]
```

### 參數

| 參數 | 說明 |
| --- | --- |
| `-a`, `--all` | 在所有專案中建立分支。 |
| `--allow-commit` | 允許从 commit 建立分支；隐含 `--allow-no-track`。 |
| `--allow-no-track` | 即使沒有 tracking branch，也允許建立分支。 |
| `--allow-tag` | 允許从 tag 建立分支；隐含 `--allow-no-track`。 |
| `-g`, `--grep-mode <string>` | 搜尋專案的 grep 模式，可选 `name(1)`、`path(2)`、`mixed(3)`、`namereg(4)`、`pathreg(5)`、`mixedreg(6)`、`underpath(7)`。預設：`mixed`。 |
| `--head` | 基于 `HEAD` 建立分支。 |
| `-h`, `--help` | 顯示 `start` 說明。 |
| `-j`, `--jobs <int>` | 同时 checkout worktree 的專案數量。預設：`8`。 |

## `git mm sync`

`sync` 用於将本機專案目录与清單中指定的遠端倉庫同步。

如果本機專案尚不存在，`sync` 会从遠端倉庫 clone 一个新的本機目录，并根據清單設定 tracking branch。如果本機專案已存在，`sync` 会更新遠端分支，并将新的本機變更 rebase 到新的遠端變更之上。

`sync` 会同步命令列中列出的所有專案。專案可以用名稱、本機專案目录的相對路徑或絕對路徑指定。如果沒有指定專案，则同步清單中列出的所有專案。

`-d` / `--detach` 选项会将指定專案切回清單修訂。当專案当前位于 topic branch，但臨時需要清單修訂时，这个选项很有用。

預設会同步 groups 和 supergroups 設定中包含的所有專案。`--fail-fast` 可以在第一个專案失敗时立即停止同步。

```bash
git mm sync [flags]
git mm sync [options] <project>...
```

別名：

```text
sync, pull, update
```

### SSH 連線說明

如果至少一个專案的遠端 URL 使用 SSH（`ssh://`、`git@host:path` 或 `user@host:path`），`sync` 在連線该 host 时会自動啟用 SSH ControlMaster 选项。这样同一次同步会话中的其他專案可以复用同一个 SSH tunnel。

在 UNIX 平台上，如需停用此行为，可以将 `GIT_SSH` 环境变量設定为 `ssh`。

範例：

```bash
export GIT_SSH=ssh
```

由於 Windows 缺少 UNIX domain socket 支援，此功能在 Windows 上停用。

如果远端 SSH daemon 是 Gerrit Code Review `2.0.10` 或更高版本，可能需要伺服器端協定修復。

### 參數

| 參數 | 說明 |
| --- | --- |
| `-a`, `--all-branches` | 擷取所有分支。 |
| `--auto-gc` | 对所有已同步專案執行垃圾回收。 |
| `-c`, `--change-id <string>` | 同步与该 change id 关联的所有變更請求。 |
| `-J`, `--checkout-jobs <int>` | 并行執行本機 checkout 的任务數量。預設：`4`。 |
| `--depth <int>` | fetch 深度。 |
| `-d`, `--detach` | 将專案 detach 回清單修訂。 |
| `--fail-fast` | 遇到第一个錯誤后停止同步。 |
| `--fetch-submodules` | 从服务器 fetch submodule。 |
| `--force-checkout` | 強制 checkout 到 revision id；如果 checkout 失敗，则 hard reset 到 revision id。警告：可能導致資料遺失。 |
| `--force-fetch` | 如果现有 Git 目录需要指向不同的 object 目录，则覆蓋它。警告：可能導致資料遺失。 |
| `--force-lfs` | 強制 checkout LFS 物件；如果 LFS checkout 失敗则快速失敗。 |
| `--force-remove-dirty` | 如果專案已不在清單中，即使有未提交修改也強制删除。警告：可能導致資料遺失。 |
| `--force-sync` | 強制同步，等价于 `--force-fetch`、`--force-checkout`、`--force-remove-dirty`。 |
| `-g`, `--grep-mode <string>` | 搜尋專案的 grep 模式，可选 `name(1)`、`path(2)`、`mixed(3)`、`namereg(4)`、`pathreg(5)`、`mixedreg(6)`、`underpath(7)`。預設：`mixed`。 |
| `-G`, `--group <string>` | 只同步指定 group 中的專案。預設：`all` 加 `G1,G2,G3,G4,-G5,-G6`。 |
| `-h`, `--help` | 顯示 `sync` 說明。 |
| `--hooks <string>` | 指定同步后要執行的 hooks，用 `,` 分隔。 |
| `--ignore-copylink-error` | 忽略 copy/link 文件錯誤。 |
| `--ignore-symlink-error` | 忽略 symlink 相关錯誤。 |
| `--ignore-git-clean-error` | 忽略由 `git-clean(1)` 引起的清理 worktree 錯誤。 |
| `-j`, `--jobs <int>` | 同时 fetch 的專案數量。預設：`8`。 |
| `-l`, `--local-only` | 只更新 worktree，不 fetch。 |
| `--manifest-name <string>` | 本次 sync 使用的本機 manifest，替代預設 manifest 文件。 |
| `--manifest-url <string>` | 要查询的 manifest 倉庫 Git URL。 |
| `--match-branch` | 同步 super/root MR 时严格匹配目标分支。 |
| `--merge` | 不使用 rebase，改用 merge 更新工作分支。 |
| `-n`, `--network-only` | 只 fetch，不更新 worktree。 |
| `--no-clean` | 同步新 worktree 前不清理 worktree。 |
| `--no-git-clean` | 不使用 `git-clean(1)` 清理 worktree。 |
| `--no-progress-bar` | 連線到終端时，預設在 stderr 报告同步进度状态。 |
| `--no-prune` | 不删除远端已不存在的 refs。 |
| `--no-snapshot` | 不使用 snapshot 插件加速同步。 |
| `-N`, `--no-update-manifest` | 預設不更新 manifest；即使在 `--local-only` 模式，也会在 sync 前更新 manifest。 |
| `--no-update-repohooks` | 不更新 repo hooks。 |
| `--progress` | 連線到終端时，預設在标准錯誤流报告进度状态。 |
| `--progress-bar` | `--no-progress-bar` 的反向选项。 |
| `-R`, `--replace-prefix <strings>` | 如果專案全名以对应替换前缀开头，则用替换后的全名下载。 |
| `--restore` | 将 worktree 恢复到初始状态，并清理本機修改。警告：可能導致資料遺失。 |
| `--retry-fetches <int>` | 瞬时錯誤时重试 fetch 的次数。預設：`2`。 |
| `--skip-closed` | 不同步已关闭的提交/變更請求。 |
| `--skip-hooks` | 同步后不執行 hooks。 |
| `--skip-lfs` | 不 checkout LFS 文件。 |
| `--smart-sync` | 使用最新已知良好构建的 manifest 进行 smart sync。 |
| `--smart-tag <string>` | 使用已知 tag 的 manifest 进行 smart sync。 |
| `--stat` | 輸出執行时統計信息。 |
| `-s`, `--super <int>` | 按 super/root MR id 同步。 |
| `--supergroup <string>` | 只同步指定 supergroup 中的專案。 |
| `--tags` | 同时 fetch tags。 |
| `--unshallow` | 使用 `--unshallow` fetch，移除 shallow repository 限制。 |

## `git mm upload`

`upload` 用於将變更傳送到目标 Code Review 系統。它会在本機倉庫中尋找尚未發佈評審的 topic branch。

如果找到多个 topic branch，`upload` 会開啟编辑器，让使用者選擇要上傳的分支。

專案可以用名稱、本機專案目录的相對路徑或絕對路徑指定。如果沒有指定專案，`upload` 会在清單中列出的所有專案中尋找可上傳的變更。

如果传入 `--reviewers` 或 `--cc`，这些信箱会被添加到評審中。作为 reviewer 传入的使用者必须已经在 Code Review 系統中注册，否则上傳会失敗。

使用 `--title` 和 `--description` 可以設定 merge request 的標題和描述。描述內容允許使用 Markdown。

```bash
git mm upload [flags]
git mm upload [options] <project>...
```

別名：

```text
upload, push
```

### 參數

| 參數 | 說明 |
| --- | --- |
| `--approvers <string>` | 請求这些人 approve，用 `;` 分隔。仅对 CodeHub CR 有效。 |
| `-A`, `--assignees <string>` | 請求这些人提交，用 `;` 分隔。仅对 CodeHub 有效。 |
| `--br <string>` | 要上傳的分支。 |
| `--cbr` | 只上傳当前分支。 |
| `--cc <string>` | 同时傳送邮件给这些信箱地址。 |
| `-D`, `--description <string>` | merge request 描述，会转换为 Markdown。仅对 CodeHub MR 有效。 |
| `--dest <string>` | 提交到该目标分支进行評審。 |
| `-f`, `--force` | 即使專案之前已经上傳过，也強制上傳每个專案。 |
| `-g`, `--grep-mode <string>` | 搜尋專案的 grep 模式，可选 `name(1)`、`path(2)`、`mixed(3)`、`namereg(4)`、`pathreg(5)`、`mixedreg(6)`、`underpath(7)`。預設：`mixed`。 |
| `--hashtag <string>` | 为評審添加 hashtag，用逗号分隔。 |
| `--hashtag-branch` | 将本機分支名作为 hashtag。 |
| `--head` | 上傳 `HEAD`，即使当前处于 detached 状态。 |
| `-h`, `--help` | 顯示 `upload` 說明。 |
| `--honor-no-changes` | 即使新提交中沒有變更，也上傳專案。 |
| `-j`, `--jobs <int>` | 同时上傳專案的任务數量。預設：`8`。 |
| `-l`, `--label <string>` | 上傳时添加 label。 |
| `--no-ssl-verify` | 停用 SSL 憑證驗證。不安全。預設：`true`。 |
| `-N`, `--no-update-manifest` | 預設不更新 manifest；上傳前会更新 manifest。 |
| `--push-option <string>` | 传递额外的 push options。 |
| `--ready` | 将變更標記为 ready，并清除 work-in-progress 設定。 |
| `-R`, `--reviewers <string>` | 請求这些人 review，用 `;` 分隔。 |
| `--ssl-verify` | 驗證 SSL 憑證。 |
| `-T`, `--title <string>` | merge request 標題。仅对 CodeHub MR 有效。 |
| `--topic <string>` | super MR 或 change request 的 topic。預設是本機分支。 |
| `--wip` | 以 work-in-progress 状态上傳變更。 |

## 全域參數

| 參數 | 說明 |
| --- | --- |
| `-C`, `--dir <string>` | 按指定路径作为当前工作目录執行 `git-mm`。 |
| `--git-path <string>` | Git 可執行文件路径。 |
| `-q`, `--quiet` | 静默模式，控制是否顯示詳細 Git 命令輸出。 |
| `--root-dir` | 顯示当前 manifest 專案的根目录。 |
| `--timeout <string>` | 命令執行超时时间，可使用 `s/m/h` 后缀。預設无超时。 |
| `--trace` | 輸出 trace 消息。 |
| `--verbose` | `--quiet` 的反向选项。 |
| `--verbosity-level <count>` | 終端日志詳細層級。預設：`INFO`；`-v` 为 `DEBUG`；`-vv` 为 `TRACE`。 |
| `--version` | 顯示 git-mm 版本。 |
| `-y`, `--yes` | 对終端問題自動回答 yes。 |
