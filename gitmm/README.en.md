# git mm Command Reference

This document summarizes the `git mm` commands used by ForkPlus: `start`, `sync`, and `upload`.

## `git mm start`

`start` creates a new development branch from the revision defined in the manifest.

```bash
git mm start <branch> [--all | <project>...]
git mm start [flags]
```

### Flags

| Flag | Description |
| --- | --- |
| `-a`, `--all` | Start the branch in all projects. |
| `--allow-commit` | Allow creating a branch from a commit. Implies `--allow-no-track`. |
| `--allow-no-track` | Allow creating a branch without a tracking branch. |
| `--allow-tag` | Allow creating a branch from a tag. Implies `--allow-no-track`. |
| `-g`, `--grep-mode <string>` | Project search mode: `name(1)`, `path(2)`, `mixed(3)`, `namereg(4)`, `pathreg(5)`, `mixedreg(6)`, `underpath(7)`. Default: `mixed`. |
| `--head` | Create the branch from `HEAD`. |
| `-h`, `--help` | Show help for `start`. |
| `-j`, `--jobs <int>` | Number of projects to checkout worktrees in parallel. Default: `8`. |

## `git mm sync`

`sync` synchronizes local project directories with the remote repositories described by the manifest. If a local project does not exist, it is cloned. If it already exists, remote branches are updated and local changes are rebased or merged according to the selected options.

```bash
git mm sync [flags]
git mm sync [options] <project>...
```

Aliases:

```text
sync, pull, update
```

### SSH Notes

When at least one remote URL uses SSH, `sync` can reuse one SSH connection through ControlMaster on supported platforms. This is disabled on Windows because UNIX domain sockets are unavailable.

### Flags

| Flag | Description |
| --- | --- |
| `-a`, `--all-branches` | Fetch all branches. |
| `--auto-gc` | Run garbage collection for synced projects. |
| `-c`, `--change-id <string>` | Sync changes related to the change id. |
| `-J`, `--checkout-jobs <int>` | Number of local checkout jobs. Default: `4`. |
| `--depth <int>` | Fetch depth. |
| `-d`, `--detach` | Detach projects back to the manifest revision. |
| `--fail-fast` | Stop at the first error. |
| `--fetch-submodules` | Fetch submodules from the server. |
| `--force-checkout` | Force checkout to the revision id. Warning: may cause data loss. |
| `--force-fetch` | Overwrite an existing Git directory when needed. Warning: may cause data loss. |
| `--force-lfs` | Force checkout of LFS objects. |
| `--force-remove-dirty` | Remove dirty projects no longer in the manifest. Warning: may cause data loss. |
| `--force-sync` | Equivalent to `--force-fetch`, `--force-checkout`, and `--force-remove-dirty`. |
| `-g`, `--grep-mode <string>` | Project search mode. Default: `mixed`. |
| `-G`, `--group <string>` | Sync only projects in selected groups. |
| `--hooks <string>` | Hooks to run after sync, separated by `,`. |
| `-j`, `--jobs <int>` | Number of projects to fetch in parallel. Default: `8`. |
| `-l`, `--local-only` | Update the worktree only; do not fetch. |
| `--manifest-name <string>` | Local manifest to use for this sync. |
| `--manifest-url <string>` | Manifest repository URL. |
| `--merge` | Merge instead of rebase. |
| `-n`, `--network-only` | Fetch only; do not update worktrees. |
| `--no-clean` | Do not clean worktrees before sync. |
| `--no-git-clean` | Do not use `git clean`. |
| `--no-prune` | Do not prune removed remote refs. |
| `--no-snapshot` | Do not use the snapshot plugin. |
| `-N`, `--no-update-manifest` | Do not update the manifest before sync. |
| `--restore` | Restore worktrees to the initial state. Warning: may cause data loss. |
| `--retry-fetches <int>` | Retry count for transient fetch failures. Default: `2`. |
| `--skip-hooks` | Do not run hooks after sync. |
| `--skip-lfs` | Do not checkout LFS files. |
| `--smart-sync` | Sync using the latest known good manifest. |
| `--smart-tag <string>` | Sync using a known manifest tag. |
| `--stat` | Print runtime statistics. |
| `-s`, `--super <int>` | Sync by super/root MR id. |
| `--supergroup <string>` | Sync only projects in selected supergroups. |
| `--tags` | Fetch tags too. |
| `--unshallow` | Remove shallow repository limitations. |

## `git mm upload`

`upload` sends local topic branch changes to the target Code Review system. Projects can be selected by name or path. If no project is specified, all projects in the manifest are scanned for uploadable changes.

```bash
git mm upload [flags]
git mm upload [options] <project>...
```

Aliases:

```text
upload, push
```

### Flags

| Flag | Description |
| --- | --- |
| `--approvers <string>` | Request approvals from these users, separated by `;`. |
| `-A`, `--assignees <string>` | Request submission from these users, separated by `;`. |
| `--br <string>` | Branch to upload. |
| `--cbr` | Upload only the current branch. |
| `--cc <string>` | CC these email addresses. |
| `-D`, `--description <string>` | Merge request description, converted to Markdown. |
| `--dest <string>` | Target branch for review. |
| `-f`, `--force` | Force upload even if already uploaded before. |
| `-g`, `--grep-mode <string>` | Project search mode. Default: `mixed`. |
| `--hashtag <string>` | Add hashtags to the review. |
| `--hashtag-branch` | Use the local branch name as a hashtag. |
| `--head` | Upload `HEAD`, even when detached. |
| `--honor-no-changes` | Upload even when new commits contain no changes. |
| `-j`, `--jobs <int>` | Number of upload jobs. Default: `8`. |
| `-l`, `--label <string>` | Add a label. |
| `--no-ssl-verify` | Disable SSL verification. Unsafe. |
| `-N`, `--no-update-manifest` | Do not update the manifest before upload. |
| `--push-option <string>` | Additional push option. |
| `--ready` | Mark the change as ready. |
| `-R`, `--reviewers <string>` | Request reviews from these users, separated by `;`. |
| `--ssl-verify` | Verify SSL certificates. |
| `-T`, `--title <string>` | Merge request title. |
| `--topic <string>` | Topic of the super MR or change request. Default: local branch. |
| `--wip` | Upload as work in progress. |

## Global Flags

| Flag | Description |
| --- | --- |
| `-C`, `--dir <string>` | Run as if `git-mm` started in this directory. |
| `--git-path <string>` | Git binary path. |
| `-q`, `--quiet` | Quiet mode. |
| `--root-dir` | Show the current manifest root directory. |
| `--timeout <string>` | Command timeout with `s/m/h` suffix. |
| `--trace` | Print trace messages. |
| `--verbose` | Opposite of `--quiet`. |
| `--verbosity-level <count>` | Log verbosity: `INFO`, `DEBUG`, or `TRACE`. |
| `--version` | Show git-mm version. |
| `-y`, `--yes` | Automatically answer yes to terminal prompts. |
