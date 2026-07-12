# git mm Befehlsreferenz

Dieses Dokument fasst die von ForkPlus verwendeten `git mm`-Befehle zusammen: `start`, `sync` und `upload`.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## `git mm start`

`start` erstellt einen neuen Entwicklungszweig aus der im Manifest definierten Revision.

```bash
git mm start <branch> [--all | <project>...]
git mm start [flags]
```

### Parameter

| Parameter | Beschreibung |
| --- | --- |
| `-a`, `--all` | Den Branch in allen Projekten starten. |
| `--allow-commit` | Erstellen eines Branchs von einem Commit erlauben. Impliziert `--allow-no-track`. |
| `--allow-no-track` | Erstellen eines Branchs ohne Tracking-Branch erlauben. |
| `--allow-tag` | Erstellen eines Branchs von einem Tag erlauben. Impliziert `--allow-no-track`. |
| `-g`, `--grep-mode <string>` | Projektsuchmodus: `name(1)`, `path(2)`, `mixed(3)`, `namereg(4)`, `pathreg(5)`, `mixedreg(6)`, `underpath(7)`. Standard: `mixed`. |
| `--head` | Den Branch von `HEAD` erstellen. |
| `-h`, `--help` | Hilfe für `start` anzeigen. |
| `-j`, `--jobs <int>` | Anzahl der Projekte zum parallelen Auschecken von Worktrees. Standard: `8`. |

## `git mm sync`

`sync` synchronisiert lokale Projektverzeichnisse mit den im Manifest beschriebenen Remote-Repositorys. Wenn ein lokales Projekt nicht existiert, wird es geklont. Wenn es bereits existiert, werden Remote-Branches aktualisiert und lokale Änderungen werden je nach ausgewählten Optionen rebased oder zusammengeführt.

```bash
git mm sync [flags]
git mm sync [options] <project>...
```

Aliase:

```text
sync, pull, update
```

### SSH-Hinweise

Wenn mindestens eine Remote-URL SSH verwendet, kann `sync` eine SSH-Verbindung über ControlMaster auf unterstützten Plattformen wiederverwenden. Dies ist unter Windows deaktiviert, da UNIX-Domain-Sockets nicht verfügbar sind.

### Parameter

| Parameter | Beschreibung |
| --- | --- |
| `-a`, `--all-branches` | Alle Branches abrufen. |
| `--auto-gc` | Garbage Collection für synchronisierte Projekte ausführen. |
| `-c`, `--change-id <string>` | Änderungen synchronisieren, die mit der Change-ID verknüpft sind. |
| `-J`, `--checkout-jobs <int>` | Anzahl der lokalen Checkout-Aufgaben. Standard: `4`. |
| `--depth <int>` | Fetch-Tiefe. |
| `-d`, `--detach` | Projekte auf die Manifest-Revision zurücksetzen. |
| `--fail-fast` | Beim ersten Fehler anhalten. |
| `--fetch-submodules` | Submodule vom Server abrufen. |
| `--force-checkout` | Checkout auf die Revisions-ID erzwingen. Warnung: kann Datenverlust verursachen. |
| `--force-fetch` | Ein bestehendes Git-Verzeichnis bei Bedarf überschreiben. Warnung: kann Datenverlust verursachen. |
| `--force-lfs` | Checkout von LFS-Objekten erzwingen. |
| `--force-remove-dirty` | Geänderte Projekte entfernen, die nicht mehr im Manifest sind. Warnung: kann Datenverlust verursachen. |
| `--force-sync` | Entspricht `--force-fetch`, `--force-checkout` und `--force-remove-dirty`. |
| `-g`, `--grep-mode <string>` | Projektsuchmodus. Standard: `mixed`. |
| `-G`, `--group <string>` | Nur Projekte in ausgewählten Gruppen synchronisieren. |
| `--hooks <string>` | Nach Sync auszuführende Hooks, durch `,` getrennt. |
| `-j`, `--jobs <int>` | Anzahl der parallel abzurufenden Projekte. Standard: `8`. |
| `-l`, `--local-only` | Nur den Worktree aktualisieren; nicht fetchen. |
| `--manifest-name <string>` | Lokales Manifest für diese Sync. |
| `--manifest-url <string>` | Manifest-Repository-URL. |
| `--merge` | Zusammenführen statt Rebase. |
| `-n`, `--network-only` | Nur fetchen; Worktrees nicht aktualisieren. |
| `--no-clean` | Worktrees vor Sync nicht bereinigen. |
| `--no-git-clean` | `git clean` nicht verwenden. |
| `--no-prune` | Entfernte Remote-Refs nicht bereinigen. |
| `--no-snapshot` | Snapshot-Plugin nicht verwenden. |
| `-N`, `--no-update-manifest` | Manifest vor Sync nicht aktualisieren. |
| `--restore` | Worktrees in den Anfangszustand zurückversetzen. Warnung: kann Datenverlust verursachen. |
| `--retry-fetches <int>` | Anzahl der Wiederholungen bei vorübergehenden Fetch-Fehlern. Standard: `2`. |
| `--skip-hooks` | Hooks nach Sync nicht ausführen. |
| `--skip-lfs` | LFS-Dateien nicht auschecken. |
| `--smart-sync` | Synchronisieren mit dem letzten als gut bekannten Manifest. |
| `--smart-tag <string>` | Synchronisieren mit einem bekannten Manifest-Tag. |
| `--stat` | Laufzeitstatistiken ausgeben. |
| `-s`, `--super <int>` | Nach Super/Root-MR-ID synchronisieren. |
| `--supergroup <string>` | Nur Projekte in ausgewählten Supergruppen synchronisieren. |
| `--tags` | Auch Tags abrufen. |
| `--unshallow` | Shallow-Repository-Beschränkungen entfernen. |

## `git mm upload`

`upload` sendet lokale Topic-Branch-Änderungen an das Ziel-Code-Review-System. Projekte können nach Name oder Pfad ausgewählt werden. Wenn kein Projekt angegeben wird, werden alle Projekte im Manifest nach hochladbaren Änderungen durchsucht.

```bash
git mm upload [flags]
git mm upload [options] <project>...
```

Aliase:

```text
upload, push
```

### Parameter

| Parameter | Beschreibung |
| --- | --- |
| `--approvers <string>` | Genehmigungen von diesen Benutzern anfordern, durch `;` getrennt. |
| `-A`, `--assignees <string>` | Einreichung von diesen Benutzern anfordern, durch `;` getrennt. |
| `--br <string>` | Zu hochladender Branch. |
| `--cbr` | Nur den aktuellen Branch hochladen. |
| `--cc <string>` | Diese E-Mail-Adressen in Kopie setzen. |
| `-D`, `--description <string>` | Merge-Request-Beschreibung, wird in Markdown konvertiert. |
| `--dest <string>` | Zielbranch für die Review. |
| `-f`, `--force` | Hochladen erzwingen, auch wenn bereits zuvor hochgeladen. |
| `-g`, `--grep-mode <string>` | Projektsuchmodus. Standard: `mixed`. |
| `--hashtag <string>` | Hashtags zur Review hinzufügen. |
| `--hashtag-branch` | Lokalen Branchnamen als Hashtag verwenden. |
| `--head` | `HEAD` hochladen, auch im detached-Zustand. |
| `--honor-no-changes` | Hochladen, auch wenn neue Commits keine Änderungen enthalten. |
| `-j`, `--jobs <int>` | Anzahl der Upload-Aufgaben. Standard: `8`. |
| `-l`, `--label <string>` | Ein Label hinzufügen. |
| `--no-ssl-verify` | SSL-Verifizierung deaktivieren. Unsicher. |
| `-N`, `--no-update-manifest` | Manifest vor dem Upload nicht aktualisieren. |
| `--push-option <string>` | Zusätzliche Push-Option. |
| `--ready` | Die Änderung als bereit markieren. |
| `-R`, `--reviewers <string>` | Reviews von diesen Benutzern anfordern, durch `;` getrennt. |
| `--ssl-verify` | SSL-Zertifikate verifizieren. |
| `-T`, `--title <string>` | Merge-Request-Titel. |
| `--topic <string>` | Topic des Super-MR oder der Änderungsanfrage. Standard: lokaler Branch. |
| `--wip` | Als Work-in-Progress hochladen. |

## Globale Parameter

| Parameter | Beschreibung |
| --- | --- |
| `-C`, `--dir <string>` | `git-mm` so ausführen, als ob es in diesem Verzeichnis gestartet wurde. |
| `--git-path <string>` | Git-Binärpfad. |
| `-q`, `--quiet` | Stillmodus. |
| `--root-dir` | Aktuelles Manifest-Wurzelverzeichnis anzeigen. |
| `--timeout <string>` | Befehlszeitüberschreitung mit `s/m/h`-Suffix. |
| `--trace` | Trace-Nachrichten ausgeben. |
| `--verbose` | Gegenteil von `--quiet`. |
| `--verbosity-level <count>` | Protokollausführlichkeit: `INFO`, `DEBUG` oder `TRACE`. |
| `--version` | git-mm-Version anzeigen. |
| `-y`, `--yes` | Terminal-Eingaben automatisch mit ja beantworten. |
