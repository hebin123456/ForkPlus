# git mm コマンドリファレンス

## `git mm start`

`start` はマニフェストで指定されたリビジョンから新しい開発ブランチを作成します。

```bash
git mm start <branch> [--all | <project>...]
git mm start [flags]
```

### パラメータ

| パラメータ | 説明 |
| --- | --- |
| `-a`, `--all` | すべてのプロジェクトにブランチを作成します。 |
| `--allow-commit` | commit からブランチを作成することを許可します。`--allow-no-track` を暗黙的に含みます。 |
| `--allow-no-track` | tracking branch がなくてもブランチの作成を許可します。 |
| `--allow-tag` | tag からブランチを作成することを許可します。`--allow-no-track` を暗黙的に含みます。 |
| `-g`, `--grep-mode <string>` | プロジェクトを検索する grep モード。`name(1)`、`path(2)`、`mixed(3)`、`namereg(4)`、`pathreg(5)`、`mixedreg(6)`、`underpath(7)` から選択可能。デフォルト: `mixed`。 |
| `--head` | `HEAD` からブランチを作成します。 |
| `-h`, `--help` | `start` のヘルプを表示します。 |
| `-j`, `--jobs <int>` | 同時に worktree を checkout するプロジェクト数。デフォルト: `8`。 |

## `git mm sync`

`sync` はローカルのプロジェクトディレクトリをマニフェストで指定されたリモートリポジトリと同期します。

ローカルプロジェクトがまだ存在しない場合、`sync` はリモートリポジトリから新しいローカルディレクトリを clone し、マニフェストに基づいて tracking branch を設定します。ローカルプロジェクトが既に存在する場合、`sync` はリモートブランチを更新し、新しいローカル変更を新しいリモート変更の上に rebase します。

`sync` はコマンドラインで指定されたすべてのプロジェクトを同期します。プロジェクトは名前、ローカルプロジェクトディレクトリの相対パス、または絶対パスで指定できます。プロジェクトが指定されていない場合は、マニフェストにリストされたすべてのプロジェクトを同期します。

`-d` / `--detach` オプションは指定されたプロジェクトをマニフェストリビジョンに戻します。プロジェクトが現在 topic branch にあるが、一時的にマニフェストリビジョンが必要な場合に便利です。

デフォルトでは groups および supergroups 設定に含まれるすべてのプロジェクトを同期します。`--fail-fast` を使用すると最初のプロジェクト失敗時に即座に同期を停止できます。

```bash
git mm sync [flags]
git mm sync [options] <project>...
```

エイリアス:

```text
sync, pull, update
```

### SSH 接続に関する注意

少なくとも1つのプロジェクトのリモート URL が SSH（`ssh://`、`git@host:path`、または `user@host:path`）を使用している場合、`sync` はその host への接続時に自動的に SSH ControlMaster オプションを有効にします。これにより同じ同期セッション内の他のプロジェクトが同じ SSH tunnel を再利用できます。

UNIX プラットフォームでこの動作を無効にするには、`GIT_SSH` 環境変数を `ssh` に設定します。

例:

```bash
export GIT_SSH=ssh
```

Windows は UNIX domain socket をサポートしていないため、この機能は Windows では無効になります。

リモート SSH デーモンが Gerrit Code Review `2.0.10` 以降の場合、サーバー側のプロトコル修正が必要な場合があります。

### パラメータ

| パラメータ | 説明 |
| --- | --- |
| `-a`, `--all-branches` | すべてのブランチを取得します。 |
| `--auto-gc` | 同期したすべてのプロジェクトでガベージコレクションを実行します。 |
| `-c`, `--change-id <string>` | その change id に関連するすべての変更リクエストを同期します。 |
| `-J`, `--checkout-jobs <int>` | ローカル checkout を並列実行するタスク数。デフォルト: `4`。 |
| `--depth <int>` | fetch の深さ。 |
| `-d`, `--detach` | プロジェクトをマニフェストリビジョンに detach します。 |
| `--fail-fast` | 最初のエラー発生後に同期を停止します。 |
| `--fetch-submodules` | サーバーから submodule を fetch します。 |
| `--force-checkout` | revision id に強制 checkout します。checkout が失敗した場合は revision id に hard reset します。警告: データ損失の可能性があります。 |
| `--force-fetch` | 既存の Git ディレクトリが別の object ディレクトリを指す必要がある場合、それを上書きします。警告: データ損失の可能性があります。 |
| `--force-lfs` | LFS オブジェクトを強制 checkout します。LFS checkout が失敗した場合は即座に失敗します。 |
| `--force-remove-dirty` | プロジェクトがもはやマニフェストにない場合、未コミットの変更があっても強制的に削除します。警告: データ損失の可能性があります。 |
| `--force-sync` | 強制同期。`--force-fetch`、`--force-checkout`、`--force-remove-dirty` と同等です。 |
| `-g`, `--grep-mode <string>` | プロジェクトを検索する grep モード。`name(1)`、`path(2)`、`mixed(3)`、`namereg(4)`、`pathreg(5)`、`mixedreg(6)`、`underpath(7)` から選択可能。デフォルト: `mixed`。 |
| `-G`, `--group <string>` | 指定した group のプロジェクトのみを同期します。デフォルト: `all` および `G1,G2,G3,G4,-G5,-G6`。 |
| `-h`, `--help` | `sync` のヘルプを表示します。 |
| `--hooks <string>` | 同期後に実行する hooks を `,` 区切りで指定します。 |
| `--ignore-copylink-error` | copy/link ファイルのエラーを無視します。 |
| `--ignore-symlink-error` | symlink 関連のエラーを無視します。 |
| `--ignore-git-clean-error` | `git-clean(1)` による worktree クリーンアップのエラーを無視します。 |
| `-j`, `--jobs <int>` | 同時に fetch するプロジェクト数。デフォルト: `8`。 |
| `-l`, `--local-only` | worktree のみを更新し、fetch は行いません。 |
| `--manifest-name <string>` | 今回の sync で使用するローカル manifest。デフォルトの manifest ファイルを置き換えます。 |
| `--manifest-url <string>` | 問い合わせる manifest リポジトリの Git URL。 |
| `--match-branch` | super/root MR 同期時に厳密に対象ブランチにマッチさせます。 |
| `--merge` | rebase ではなく merge を使用して作業ブランチを更新します。 |
| `-n`, `--network-only` | fetch のみを行い、worktree を更新しません。 |
| `--no-clean` | 新しい worktree を同期する前に worktree をクリーンアップしません。 |
| `--no-git-clean` | `git-clean(1)` を使用して worktree をクリーンアップしません。 |
| `--no-progress-bar` | ターミナル接続時、デフォルトで stderr に同期進行状況を報告します。 |
| `--no-prune` | リモートに存在しなくなった refs を削除しません。 |
| `--no-snapshot` | snapshot プラグインによる同期の高速化を使用しません。 |
| `-N`, `--no-update-manifest` | デフォルトで manifest を更新しません。`--local-only` モードでも sync 前に manifest を更新します。 |
| `--no-update-repohooks` | repo hooks を更新しません。 |
| `--progress` | ターミナル接続時、デフォルトで標準エラーに進行状況を報告します。 |
| `--progress-bar` | `--no-progress-bar` の逆オプション。 |
| `-R`, `--replace-prefix <strings>` | プロジェクトのフルネームが対応する置換プレフィックスで始まる場合、置換後のフルネームでダウンロードします。 |
| `--restore` | worktree を初期状態に戻し、ローカル変更をクリーンアップします。警告: データ損失の可能性があります。 |
| `--retry-fetches <int>` | 一時エラー時の fetch 再試行回数。デフォルト: `2`。 |
| `--skip-closed` | クローズされた commit/変更リクエストを同期しません。 |
| `--skip-hooks` | 同期後に hooks を実行しません。 |
| `--skip-lfs` | LFS ファイルを checkout しません。 |
| `--smart-sync` | 最新の既知の良好なビルドの manifest を使用して smart sync を行います。 |
| `--smart-tag <string>` | 既知の tag の manifest を使用して smart sync を行います。 |
| `--stat` | 実行時統計情報を出力します。 |
| `-s`, `--super <int>` | super/root MR id で同期します。 |
| `--supergroup <string>` | 指定した supergroup のプロジェクトのみを同期します。 |
| `--tags` | tags も同時に fetch します。 |
| `--unshallow` | `--unshallow` fetch を使用し、shallow repository の制限を解除します。 |

## `git mm upload`

`upload` は変更を対象の Code Review システムに送信します。ローカルリポジトリでまだレビューに提出されていない topic branch を検索します。

複数の topic branch が見つかった場合、`upload` はエディタを開き、アップロードするブランチをユーザーに選択させます。

プロジェクトは名前、ローカルプロジェクトディレクトリの相対パス、または絶対パスで指定できます。プロジェクトが指定されていない場合は、マニフェストにリストされたすべてのプロジェクトでアップロード可能な変更を検索します。

`--reviewers` または `--cc` を渡すと、それらのメールアドレスがレビューに追加されます。reviewer として指定されたユーザーは Code Review システムに既に登録されている必要があり、そうでない場合はアップロードが失敗します。

`--title` と `--description` を使用して merge request のタイトルと説明を設定できます。説明には Markdown が使用できます。

```bash
git mm upload [flags]
git mm upload [options] <project>...
```

エイリアス:

```text
upload, push
```

### パラメータ

| パラメータ | 説明 |
| --- | --- |
| `--approvers <string>` | これらのユーザーに approve をリクエストします。`;` 区切り。CodeHub CR のみ有効。 |
| `-A`, `--assignees <string>` | これらのユーザーに提出をリクエストします。`;` 区切り。CodeHub のみ有効。 |
| `--br <string>` | アップロードするブランチ。 |
| `--cbr` | 現在のブランチのみをアップロードします。 |
| `--cc <string>` | これらのメールアドレスにもメールを送信します。 |
| `-D`, `--description <string>` | merge request の説明。Markdown に変換されます。CodeHub MR のみ有効。 |
| `--dest <string>` | レビューのために提出する対象ブランチ。 |
| `-f`, `--force` | プロジェクトが以前にアップロード済みでも、各プロジェクトを強制的にアップロードします。 |
| `-g`, `--grep-mode <string>` | プロジェクトを検索する grep モード。`name(1)`、`path(2)`、`mixed(3)`、`namereg(4)`、`pathreg(5)`、`mixedreg(6)`、`underpath(7)` から選択可能。デフォルト: `mixed`。 |
| `--hashtag <string>` | レビューに hashtag を追加します。カンマ区切り。 |
| `--hashtag-branch` | ローカルブランチ名を hashtag として使用します。 |
| `--head` | detached 状態でも `HEAD` をアップロードします。 |
| `-h`, `--help` | `upload` のヘルプを表示します。 |
| `--honor-no-changes` | 新しい commit に変更がなくてもプロジェクトをアップロードします。 |
| `-j`, `--jobs <int>` | 同時にアップロードするプロジェクトのタスク数。デフォルト: `8`。 |
| `-l`, `--label <string>` | アップロード時に label を追加します。 |
| `--no-ssl-verify` | SSL 証明書の検証を無効にします。安全ではありません。デフォルト: `true`。 |
| `-N`, `--no-update-manifest` | デフォルトで manifest を更新しません。アップロード前に manifest を更新します。 |
| `--push-option <string>` | 追加の push options を渡します。 |
| `--ready` | 変更を ready としてマークし、work-in-progress 設定をクリアします。 |
| `-R`, `--reviewers <string>` | これらのユーザーに review をリクエストします。`;` 区切り。 |
| `--ssl-verify` | SSL 証明書を検証します。 |
| `-T`, `--title <string>` | merge request のタイトル。CodeHub MR のみ有効。 |
| `--topic <string>` | super MR または change request の topic。デフォルトはローカルブランチ。 |
| `--wip` | 変更を work-in-progress としてアップロードします。 |

## グローバルパラメータ

| パラメータ | 説明 |
| --- | --- |
| `-C`, `--dir <string>` | 指定したパスをカレントワーキングディレクトリとして `git-mm` を実行します。 |
| `--git-path <string>` | Git 実行ファイルのパス。 |
| `-q`, `--quiet` | サイレントモード。詳細な Git コマンド出力の表示を制御します。 |
| `--root-dir` | 現在の manifest プロジェクトのルートディレクトリを表示します。 |
| `--timeout <string>` | コマンド実行のタイムアウト時間。`s/m/h` サフィックス使用可能。デフォルトはタイムアウトなし。 |
| `--trace` | trace メッセージを出力します。 |
| `--verbose` | `--quiet` の逆オプション。 |
| `--verbosity-level <count>` | ターミナルログの詳細レベル。デフォルト: `INFO`。`-v` は `DEBUG`、`-vv` は `TRACE`。 |
| `--version` | git-mm のバージョンを表示します。 |
| `-y`, `--yes` | ターミナルの質問に自動的に yes と答えます。 |
