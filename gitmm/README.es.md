
# Referencia de comandos git mm

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | Español

## `git mm start`

`start` se utiliza para crear una nueva rama de desarrollo a partir de la revisión especificada en el manifest.

```bash
git mm start <branch> [--all | <project>...]
git mm start [flags]
```

### Parámetros

| Parámetro | Descripción |
| --- | --- |
| `-a`, `--all` | Crea la rama en todos los proyectos. |
| `--allow-commit` | Permite crear la rama a partir de un commit; implica `--allow-no-track`. |
| `--allow-no-track` | Permite crear la rama aunque no exista una tracking branch. |
| `--allow-tag` | Permite crear la rama a partir de un tag; implica `--allow-no-track`. |
| `-g`, `--grep-mode <string>` | Modo grep para buscar proyectos, opciones: `name(1)`, `path(2)`, `mixed(3)`, `namereg(4)`, `pathreg(5)`, `mixedreg(6)`, `underpath(7)`. Predeterminado: `mixed`. |
| `--head` | Crea la rama basándose en `HEAD`. |
| `-h`, `--help` | Muestra la ayuda de `start`. |
| `-j`, `--jobs <int>` | Número de proyectos cuyo worktree se hace checkout simultáneamente. Predeterminado: `8`. |

## `git mm sync`

`sync` se utiliza para sincronizar los directorios de proyectos locales con los repositorios remotos especificados en el manifest.

Si el proyecto local aún no existe, `sync` clonará un nuevo directorio local desde el repositorio remoto y configurará la tracking branch según el manifest. Si el proyecto local ya existe, `sync` actualizará las ramas remotas y hará rebase de los cambios locales nuevos sobre los cambios remotos nuevos.

`sync` sincroniza todos los proyectos listados en la línea de comandos. Los proyectos se pueden especificar por nombre, por la ruta relativa del directorio local del proyecto o por ruta absoluta. Si no se especifica ningún proyecto, se sincronizan todos los proyectos listados en el manifest.

La opción `-d` / `--detach` hace que los proyectos especificados vuelvan a la revisión del manifest. Esta opción es útil cuando el proyecto se encuentra actualmente en una topic branch pero se necesita temporalmente la revisión del manifest.

De forma predeterminada se sincronizan todos los proyectos incluidos en la configuración de groups y supergroups. `--fail-fast` permite detener la sincronización inmediatamente al fallar el primer proyecto.

```bash
git mm sync [flags]
git mm sync [options] <project>...
```

Alias:

```text
sync, pull, update
```

### Notas sobre la conexión SSH

Si al menos un proyecto tiene una URL remota que usa SSH (`ssh://`, `git@host:path` o `user@host:path`), `sync` habilitará automáticamente la opción SSH ControlMaster al conectarse a ese host. De este modo, otros proyectos dentro de la misma sesión de sincronización pueden reutilizar el mismo tunnel SSH.

En plataformas UNIX, si desea deshabilitar este comportamiento, establezca la variable de entorno `GIT_SSH` a `ssh`.

Ejemplo:

```bash
export GIT_SSH=ssh
```

Debido a que Windows carece de soporte para UNIX domain socket, esta funcionalidad está deshabilitada en Windows.

Si el daemon SSH remoto es Gerrit Code Review `2.0.10` o superior, puede requerir una corrección del protocolo del lado del servidor.

### Parámetros

| Parámetro | Descripción |
| --- | --- |
| `-a`, `--all-branches` | Obtiene todas las ramas. |
| `--auto-gc` | Ejecuta la recolección de basura en todos los proyectos sincronizados. |
| `-c`, `--change-id <string>` | Sincroniza todas las solicitudes de cambio asociadas a ese change id. |
| `-J`, `--checkout-jobs <int>` | Número de tareas de checkout local que se ejecutan en paralelo. Predeterminado: `4`. |
| `--depth <int>` | Profundidad de fetch. |
| `-d`, `--detach` | Desconecta el proyecto y lo devuelve a la revisión del manifest. |
| `--fail-fast` | Detiene la sincronización tras el primer error. |
| `--fetch-submodules` | Obtiene los submodule desde el servidor. |
| `--force-checkout` | Fuerza el checkout al revision id; si el checkout falla, hace hard reset al revision id. Advertencia: puede provocar pérdida de datos. |
| `--force-fetch` | Si el directorio Git existente necesita apuntar a un object directory diferente, lo sobrescribe. Advertencia: puede provocar pérdida de datos. |
| `--force-lfs` | Fuerza el checkout de objetos LFS; si el checkout de LFS falla, falla rápidamente. |
| `--force-remove-dirty` | Si el proyecto ya no está en el manifest, lo elimina forzadamente incluso si tiene modificaciones sin confirmar. Advertencia: puede provocar pérdida de datos. |
| `--force-sync` | Sincronización forzada, equivalente a `--force-fetch`, `--force-checkout`, `--force-remove-dirty`. |
| `-g`, `--grep-mode <string>` | Modo grep para buscar proyectos, opciones: `name(1)`, `path(2)`, `mixed(3)`, `namereg(4)`, `pathreg(5)`, `mixedreg(6)`, `underpath(7)`. Predeterminado: `mixed`. |
| `-G`, `--group <string>` | Solo sincroniza los proyectos del grupo especificado. Predeterminado: `all` más `G1,G2,G3,G4,-G5,-G6`. |
| `-h`, `--help` | Muestra la ayuda de `sync`. |
| `--hooks <string>` | Especifica los hooks que se ejecutarán tras la sincronización, separados por `,`. |
| `--ignore-copylink-error` | Ignora los errores de archivos copy/link. |
| `--ignore-symlink-error` | Ignora los errores relacionados con symlink. |
| `--ignore-git-clean-error` | Ignora los errores al limpiar el worktree causados por `git-clean(1)`. |
| `-j`, `--jobs <int>` | Número de proyectos que se obtienen (fetch) simultáneamente. Predeterminado: `8`. |
| `-l`, `--local-only` | Solo actualiza el worktree, sin fetch. |
| `--manifest-name <string>` | Manifest local que se usará para esta sincronización, en sustitución del archivo manifest predeterminado. |
| `--manifest-url <string>` | Git URL del repositorio manifest que se va a consultar. |
| `--match-branch` | Sincroniza el MR super/root coincidiendo estrictamente con la rama destino. |
| `--merge` | En lugar de rebase, utiliza merge para actualizar la rama de trabajo. |
| `-n`, `--network-only` | Solo hace fetch, no actualiza el worktree. |
| `--no-clean` | No limpia el worktree antes de sincronizar el nuevo worktree. |
| `--no-git-clean` | No utiliza `git-clean(1)` para limpiar el worktree. |
| `--no-progress-bar` | Cuando está conectado a una terminal, por defecto informa del estado de progreso de la sincronización en stderr. |
| `--no-prune` | No elimina los refs que ya no existen en el remoto. |
| `--no-snapshot` | No utiliza el plugin snapshot para acelerar la sincronización. |
| `-N`, `--no-update-manifest` | Por defecto no actualiza el manifest; incluso en modo `--local-only`, actualiza el manifest antes de sincronizar. |
| `--no-update-repohooks` | No actualiza los repo hooks. |
| `--progress` | Cuando está conectado a una terminal, por defecto informa del estado de progreso en el flujo de error estándar. |
| `--progress-bar` | Opción inversa a `--no-progress-bar`. |
| `-R`, `--replace-prefix <strings>` | Si el nombre completo del proyecto comienza con el prefijo de reemplazo correspondiente, se descarga con el nombre completo reemplazado. |
| `--restore` | Restaura el worktree a su estado inicial y limpia las modificaciones locales. Advertencia: puede provocar pérdida de datos. |
| `--retry-fetches <int>` | Número de reintentos de fetch ante errores transitorios. Predeterminado: `2`. |
| `--skip-closed` | No sincroniza los commits/solicitudes de cambio ya cerrados. |
| `--skip-hooks` | No ejecuta hooks después de la sincronización. |
| `--skip-lfs` | No hace checkout de los archivos LFS. |
| `--smart-sync` | Realiza un smart sync usando el manifest de la última compilación buena conocida. |
| `--smart-tag <string>` | Realiza un smart sync usando el manifest de un tag conocido. |
| `--stat` | Muestra estadísticas de tiempo de ejecución. |
| `-s`, `--super <int>` | Sincroniza por el id del MR super/root. |
| `--supergroup <string>` | Solo sincroniza los proyectos del supergroup especificado. |
| `--tags` | También obtiene los tags. |
| `--unshallow` | Usa `--unshallow` en el fetch, eliminando la limitación de shallow repository. |

## `git mm upload`

`upload` se utiliza para enviar cambios al sistema de Code Review objetivo. Busca en el repositorio local las topic branch que aún no han sido enviadas a revisión.

Si encuentra varias topic branch, `upload` abrirá el editor para que el usuario seleccione la rama que desea subir.

Los proyectos se pueden especificar por nombre, por la ruta relativa del directorio local del proyecto o por ruta absoluta. Si no se especifica ningún proyecto, `upload` buscará los cambios que se pueden subir en todos los proyectos listados en el manifest.

Si se pasa `--reviewers` o `--cc`, esos correos se añadirán a la revisión. Los usuarios pasados como reviewer deben estar ya registrados en el sistema de Code Review, de lo contrario la subida fallará.

Con `--title` y `--description` se puede establecer el título y la descripción del merge request. El contenido de la descripción permite usar Markdown.

```bash
git mm upload [flags]
git mm upload [options] <project>...
```

Alias:

```text
upload, push
```

### Parámetros

| Parámetro | Descripción |
| --- | --- |
| `--approvers <string>` | Solicita la aprobación de estas personas, separadas por `;`. Solo válido para CodeHub CR. |
| `-A`, `--assignees <string>` | Solicita que estas personas envíen, separadas por `;`. Solo válido para CodeHub. |
| `--br <string>` | La rama que se va a subir. |
| `--cbr` | Solo sube la rama actual. |
| `--cc <string>` | Envía también un correo a estas direcciones. |
| `-D`, `--description <string>` | Descripción del merge request, se convertirá a Markdown. Solo válido para CodeHub MR. |
| `--dest <string>` | Envía a esa rama destino para revisión. |
| `-f`, `--force` | Fuerza la subida de cada proyecto incluso si ya se había subido anteriormente. |
| `-g`, `--grep-mode <string>` | Modo grep para buscar proyectos, opciones: `name(1)`, `path(2)`, `mixed(3)`, `namereg(4)`, `pathreg(5)`, `mixedreg(6)`, `underpath(7)`. Predeterminado: `mixed`. |
| `--hashtag <string>` | Añade un hashtag a la revisión, separados por comas. |
| `--hashtag-branch` | Usa el nombre de la rama local como hashtag. |
| `--head` | Sube `HEAD`, incluso si está en estado detached. |
| `-h`, `--help` | Muestra la ayuda de `upload`. |
| `--honor-no-changes` | Sube el proyecto incluso si no hay cambios en los nuevos commits. |
| `-j`, `--jobs <int>` | Número de tareas que suben proyectos simultáneamente. Predeterminado: `8`. |
| `-l`, `--label <string>` | Añade un label al subir. |
| `--no-ssl-verify` | Deshabilita la verificación del certificado SSL. Inseguro. Predeterminado: `true`. |
| `-N`, `--no-update-manifest` | Por defecto no actualiza el manifest; antes de subir se actualiza el manifest. |
| `--push-option <string>` | Pasa push options adicionales. |
| `--ready` | Marca los cambios como ready y limpia la configuración work-in-progress. |
| `-R`, `--reviewers <string>` | Solicita que estas personas hagan review, separadas por `;`. |
| `--ssl-verify` | Verifica el certificado SSL. |
| `-T`, `--title <string>` | Título del merge request. Solo válido para CodeHub MR. |
| `--topic <string>` | Topic del super MR o change request. Por defecto es la rama local. |
| `--wip` | Sube los cambios en estado work-in-progress. |

## Parámetros globales

| Parámetro | Descripción |
| --- | --- |
| `-C`, `--dir <string>` | Ejecuta `git-mm` usando la ruta especificada como directorio de trabajo actual. |
| `--git-path <string>` | Ruta del ejecutable de Git. |
| `-q`, `--quiet` | Modo silencioso, controla si se muestra la salida detallada de los comandos Git. |
| `--root-dir` | Muestra el directorio raíz del proyecto manifest actual. |
| `--timeout <string>` | Tiempo de espera máximo para la ejecución del comando, admite sufijos `s/m/h`. Por defecto sin tiempo de espera. |
| `--trace` | Muestra mensajes de trace. |
| `--verbose` | Opción inversa a `--quiet`. |
| `--verbosity-level <count>` | Nivel de detalle del registro en terminal. Predeterminado: `INFO`; `-v` es `DEBUG`; `-vv` es `TRACE`. |
| `--version` | Muestra la versión de git-mm. |
| `-y`, `--yes` | Responde automáticamente yes a las preguntas de la terminal. |
