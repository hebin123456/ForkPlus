# git mm 명령 참조

이 문서는 ForkPlus에서 사용하는 `git mm` 명령인 `start`, `sync`, `upload`를 요약합니다.

## `git mm start`

`start`는 매니페스트에 정의된 리비전에서 새 개발 브랜치를 생성합니다.

```bash
git mm start <branch> [--all | <project>...]
git mm start [flags]
```

### 플래그

| 플래그 | 설명 |
| --- | --- |
| `-a`, `--all` | 모든 프로젝트에서 브랜치를 시작합니다. |
| `--allow-commit` | 커밋에서 브랜치 생성을 허용합니다. `--allow-no-track`를 암시합니다. |
| `--allow-no-track` | tracking branch 없이 브랜치 생성을 허용합니다. |
| `--allow-tag` | 태그에서 브랜치 생성을 허용합니다. `--allow-no-track`를 암시합니다. |
| `-g`, `--grep-mode <string>` | 프로젝트 검색 모드: `name(1)`, `path(2)`, `mixed(3)`, `namereg(4)`, `pathreg(5)`, `mixedreg(6)`, `underpath(7)`. 기본값: `mixed`. |
| `--head` | `HEAD`에서 브랜치를 생성합니다. |
| `-h`, `--help` | `start` 도움말을 표시합니다. |
| `-j`, `--jobs <int>` | worktree를 병렬로 checkout할 프로젝트 수. 기본값: `8`. |

## `git mm sync`

`sync`는 로컬 프로젝트 디렉토리를 매니페스트에 설명된 원격 저장소와 동기화합니다. 로컬 프로젝트가 존재하지 않으면 clone됩니다. 이미 존재하는 경우, 원격 브랜치가 업데이트되고 선택한 옵션에 따라 로컬 변경사항이 rebase되거나 merge됩니다.

```bash
git mm sync [flags]
git mm sync [options] <project>...
```

별칭:

```text
sync, pull, update
```

### SSH 참고 사항

최소 하나의 원격 URL이 SSH를 사용하는 경우, `sync`는 지원되는 플랫폼에서 ControlMaster를 통해 하나의 SSH 연결을 재사용할 수 있습니다. UNIX 도메인 소켓을 사용할 수 없기 때문에 Windows에서는 비활성화됩니다.

### 플래그

| 플래그 | 설명 |
| --- | --- |
| `-a`, `--all-branches` | 모든 브랜치를 fetch합니다. |
| `--auto-gc` | 동기화된 프로젝트에 대해 가비지 컬렉션을 실행합니다. |
| `-c`, `--change-id <string>` | 해당 change id와 관련된 변경사항을 동기화합니다. |
| `-J`, `--checkout-jobs <int>` | 로컬 checkout 작업 수. 기본값: `4`. |
| `--depth <int>` | fetch 깊이. |
| `-d`, `--detach` | 프로젝트를 매니페스트 리비전으로 되돌립니다. |
| `--fail-fast` | 첫 번째 오류에서 중지합니다. |
| `--fetch-submodules` | 서버에서 서브모듈을 fetch합니다. |
| `--force-checkout` | 리비전 id로 강제 checkout합니다. 경고: 데이터 손실이 발생할 수 있습니다. |
| `--force-fetch` | 필요한 경우 기존 Git 디렉토리를 덮어씁니다. 경고: 데이터 손실이 발생할 수 있습니다. |
| `--force-lfs` | LFS 객체를 강제 checkout합니다. |
| `--force-remove-dirty` | 매니페스트에 더 이상 없는 변경사항이 있는 프로젝트를 제거합니다. 경고: 데이터 손실이 발생할 수 있습니다. |
| `--force-sync` | `--force-fetch`, `--force-checkout`, `--force-remove-dirty`와 동일합니다. |
| `-g`, `--grep-mode <string>` | 프로젝트 검색 모드. 기본값: `mixed`. |
| `-G`, `--group <string>` | 선택한 그룹의 프로젝트만 동기화합니다. |
| `--hooks <string>` | sync 후 실행할 hooks, `,`로 구분. |
| `-j`, `--jobs <int>` | 병렬로 fetch할 프로젝트 수. 기본값: `8`. |
| `-l`, `--local-only` | worktree만 업데이트하고 fetch하지 않습니다. |
| `--manifest-name <string>` | 이 sync에 사용할 로컬 매니페스트. |
| `--manifest-url <string>` | 매니페스트 저장소 URL. |
| `--merge` | rebase 대신 merge합니다. |
| `-n`, `--network-only` | fetch만 하고 worktree를 업데이트하지 않습니다. |
| `--no-clean` | sync 전 worktree를 정리하지 않습니다. |
| `--no-git-clean` | `git clean`을 사용하지 않습니다. |
| `--no-prune` | 제거된 원격 refs를 정리하지 않습니다. |
| `--no-snapshot` | snapshot 플러그인을 사용하지 않습니다. |
| `-N`, `--no-update-manifest` | sync 전 매니페스트를 업데이트하지 않습니다. |
| `--restore` | worktree를 초기 상태로 복원합니다. 경고: 데이터 손실이 발생할 수 있습니다. |
| `--retry-fetches <int>` | 일시적 fetch 실패에 대한 재시도 횟수. 기본값: `2`. |
| `--skip-hooks` | sync 후 hooks를 실행하지 않습니다. |
| `--skip-lfs` | LFS 파일을 checkout하지 않습니다. |
| `--smart-sync` | 알려진 최신 양호 매니페스트를 사용하여 sync합니다. |
| `--smart-tag <string>` | 알려진 매니페스트 태그를 사용하여 sync합니다. |
| `--stat` | 런타임 통계를 출력합니다. |
| `-s`, `--super <int>` | super/root MR id로 sync합니다. |
| `--supergroup <string>` | 선택한 supergroup의 프로젝트만 동기화합니다. |
| `--tags` | 태그도 fetch합니다. |
| `--unshallow` | shallow 저장소 제한을 제거합니다. |

## `git mm upload`

`upload`는 로컬 topic branch 변경사항을 대상 Code Review 시스템으로 전송합니다. 프로젝트는 이름이나 경로로 선택할 수 있습니다. 프로젝트를 지정하지 않으면, 매니페스트의 모든 프로젝트에서 업로드 가능한 변경사항을 검색합니다.

```bash
git mm upload [flags]
git mm upload [options] <project>...
```

별칭:

```text
upload, push
```

### 플래그

| 플래그 | 설명 |
| --- | --- |
| `--approvers <string>` | 이 사용자들에게 승인을 요청합니다. `;`로 구분. |
| `-A`, `--assignees <string>` | 이 사용자들에게 제출을 요청합니다. `;`로 구분. |
| `--br <string>` | 업로드할 브랜치. |
| `--cbr` | 현재 브랜치만 업로드합니다. |
| `--cc <string>` | 이 이메일 주소를 참조에 추가합니다. |
| `-D`, `--description <string>` | merge request 설명, Markdown으로 변환됩니다. |
| `--dest <string>` | 리뷰를 위한 대상 브랜치. |
| `-f`, `--force` | 이전에 업로드된 적이 있어도 강제로 업로드합니다. |
| `-g`, `--grep-mode <string>` | 프로젝트 검색 모드. 기본값: `mixed`. |
| `--hashtag <string>` | 리뷰에 해시태그를 추가합니다. |
| `--hashtag-branch` | 로컬 브랜치 이름을 해시태그로 사용합니다. |
| `--head` | detached 상태에서도 `HEAD`를 업로드합니다. |
| `--honor-no-changes` | 새 커밋에 변경사항이 없어도 업로드합니다. |
| `-j`, `--jobs <int>` | 업로드 작업 수. 기본값: `8`. |
| `-l`, `--label <string>` | 레이블을 추가합니다. |
| `--no-ssl-verify` | SSL 확인을 비활성화합니다. 안전하지 않음. |
| `-N`, `--no-update-manifest` | 업로드 전 매니페스트를 업데이트하지 않습니다. |
| `--push-option <string>` | 추가 push 옵션. |
| `--ready` | 변경사항을 ready로 표시합니다. |
| `-R`, `--reviewers <string>` | 이 사용자들에게 리뷰를 요청합니다. `;`로 구분. |
| `--ssl-verify` | SSL 인증서를 확인합니다. |
| `-T`, `--title <string>` | merge request 제목. |
| `--topic <string>` | super MR 또는 change request의 topic. 기본값: 로컬 브랜치. |
| `--wip` | 작업 중인 상태로 업로드합니다. |

## 전역 플래그

| 플래그 | 설명 |
| --- | --- |
| `-C`, `--dir <string>` | `git-mm`이 이 디렉토리에서 시작된 것처럼 실행합니다. |
| `--git-path <string>` | Git 바이너리 경로. |
| `-q`, `--quiet` | 조용히 모드. |
| `--root-dir` | 현재 매니페스트 루트 디렉토리를 표시합니다. |
| `--timeout <string>` | `s/m/h` 접미사가 있는 명령 타임아웃. |
| `--trace` | trace 메시지를 출력합니다. |
| `--verbose` | `--quiet`의 반대. |
| `--verbosity-level <count>` | 로그 상세도: `INFO`, `DEBUG` 또는 `TRACE`. |
| `--version` | git-mm 버전을 표시합니다. |
| `-y`, `--yes` | 터미널 프롬프트에 자동으로 yes라고 답합니다. |
