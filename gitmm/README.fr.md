# Référence des commandes git mm

Ce document résume les commandes `git mm` utilisées par ForkPlus : `start`, `sync` et `upload`.

## `git mm start`

`start` crée une nouvelle branche de développement à partir de la révision définie dans le manifeste.

```bash
git mm start <branch> [--all | <project>...]
git mm start [flags]
```

### Options

| Option | Description |
| --- | --- |
| `-a`, `--all` | Démarrer la branche dans tous les projets. |
| `--allow-commit` | Autoriser la création d'une branche à partir d'un commit. Implique `--allow-no-track`. |
| `--allow-no-track` | Autoriser la création d'une branche sans branche de suivi. |
| `--allow-tag` | Autoriser la création d'une branche à partir d'un tag. Implique `--allow-no-track`. |
| `-g`, `--grep-mode <string>` | Mode de recherche de projet : `name(1)`, `path(2)`, `mixed(3)`, `namereg(4)`, `pathreg(5)`, `mixedreg(6)`, `underpath(7)`. Par défaut : `mixed`. |
| `--head` | Créer la branche à partir de `HEAD`. |
| `-h`, `--help` | Afficher l'aide pour `start`. |
| `-j`, `--jobs <int>` | Nombre de projets pour checkout de worktrees en parallèle. Par défaut : `8`. |

## `git mm sync`

`sync` synchronise les répertoires de projets locaux avec les dépôts distants décrits par le manifeste. Si un projet local n'existe pas, il est cloné. S'il existe déjà, les branches distantes sont mises à jour et les changements locaux sont rebasés ou fusionnés selon les options sélectionnées.

```bash
git mm sync [flags]
git mm sync [options] <project>...
```

Alias :

```text
sync, pull, update
```

### Notes SSH

Lorsqu'au moins une URL distante utilise SSH, `sync` peut réutiliser une connexion SSH via ControlMaster sur les plateformes prises en charge. Ceci est désactivé sur Windows car les sockets de domaine UNIX ne sont pas disponibles.

### Options

| Option | Description |
| --- | --- |
| `-a`, `--all-branches` | Récupérer toutes les branches. |
| `--auto-gc` | Exécuter le ramasse-miettes pour les projets synchronisés. |
| `-c`, `--change-id <string>` | Synchroniser les changements liés à l'identifiant de changement. |
| `-J`, `--checkout-jobs <int>` | Nombre de tâches de checkout locales. Par défaut : `4`. |
| `--depth <int>` | Profondeur de fetch. |
| `-d`, `--detach` | Détacher les projets vers la révision du manifeste. |
| `--fail-fast` | Arrêter à la première erreur. |
| `--fetch-submodules` | Récupérer les sous-modules depuis le serveur. |
| `--force-checkout` | Forcer le checkout vers l'identifiant de révision. Avertissement : peut causer une perte de données. |
| `--force-fetch` | Écraser un répertoire Git existant si nécessaire. Avertissement : peut causer une perte de données. |
| `--force-lfs` | Forcer le checkout des objets LFS. |
| `--force-remove-dirty` | Supprimer les projets modifiés qui ne sont plus dans le manifeste. Avertissement : peut causer une perte de données. |
| `--force-sync` | Équivalent à `--force-fetch`, `--force-checkout` et `--force-remove-dirty`. |
| `-g`, `--grep-mode <string>` | Mode de recherche de projet. Par défaut : `mixed`. |
| `-G`, `--group <string>` | Synchroniser uniquement les projets dans les groupes sélectionnés. |
| `--hooks <string>` | Hooks à exécuter après sync, séparés par `,`. |
| `-j`, `--jobs <int>` | Nombre de projets à fetch en parallèle. Par défaut : `8`. |
| `-l`, `--local-only` | Mettre à jour uniquement le worktree ; ne pas fetch. |
| `--manifest-name <string>` | Manifeste local à utiliser pour cette sync. |
| `--manifest-url <string>` | URL du dépôt de manifeste. |
| `--merge` | Fusionner au lieu de rebaser. |
| `-n`, `--network-only` | Fetch uniquement ; ne pas mettre à jour les worktrees. |
| `--no-clean` | Ne pas nettoyer les worktrees avant sync. |
| `--no-git-clean` | Ne pas utiliser `git clean`. |
| `--no-prune` | Ne pas élaguer les références distantes supprimées. |
| `--no-snapshot` | Ne pas utiliser le plugin snapshot. |
| `-N`, `--no-update-manifest` | Ne pas mettre à jour le manifeste avant sync. |
| `--restore` | Restaurer les worktrees à l'état initial. Avertissement : peut causer une perte de données. |
| `--retry-fetches <int>` | Nombre de réessais pour les échecs de fetch temporaires. Par défaut : `2`. |
| `--skip-hooks` | Ne pas exécuter les hooks après sync. |
| `--skip-lfs` | Ne pas checkout les fichiers LFS. |
| `--smart-sync` | Synchroniser en utilisant le dernier manifeste connu valide. |
| `--smart-tag <string>` | Synchroniser en utilisant un tag de manifeste connu. |
| `--stat` | Afficher les statistiques d'exécution. |
| `-s`, `--super <int>` | Synchroniser par identifiant de MR super/root. |
| `--supergroup <string>` | Synchroniser uniquement les projets dans les supergroupes sélectionnés. |
| `--tags` | Fetch également les tags. |
| `--unshallow` | Supprimer les limitations de dépôt shallow. |

## `git mm upload`

`upload` envoie les changements de la branche topic locale vers le système de Code Review cible. Les projets peuvent être sélectionnés par nom ou par chemin. Si aucun projet n'est spécifié, tous les projets du manifeste sont analysés pour trouver des changements téléversables.

```bash
git mm upload [flags]
git mm upload [options] <project>...
```

Alias :

```text
upload, push
```

### Options

| Option | Description |
| --- | --- |
| `--approvers <string>` | Demander des approbations à ces utilisateurs, séparés par `;`. |
| `-A`, `--assignees <string>` | Demander une soumission à ces utilisateurs, séparés par `;`. |
| `--br <string>` | Branche à téléverser. |
| `--cbr` | Téléverser uniquement la branche courante. |
| `--cc <string>` | Mettre en copie ces adresses e-mail. |
| `-D`, `--description <string>` | Description de la merge request, convertie en Markdown. |
| `--dest <string>` | Branche cible pour la revue. |
| `-f`, `--force` | Forcer le téléversement même si déjà téléversé auparavant. |
| `-g`, `--grep-mode <string>` | Mode de recherche de projet. Par défaut : `mixed`. |
| `--hashtag <string>` | Ajouter des hashtags à la revue. |
| `--hashtag-branch` | Utiliser le nom de branche local comme hashtag. |
| `--head` | Téléverser `HEAD`, même en état détaché. |
| `--honor-no-changes` | Téléverser même lorsque les nouveaux commits ne contiennent aucun changement. |
| `-j`, `--jobs <int>` | Nombre de tâches de téléversement. Par défaut : `8`. |
| `-l`, `--label <string>` | Ajouter un label. |
| `--no-ssl-verify` | Désactiver la vérification SSL. Non sécurisé. |
| `-N`, `--no-update-manifest` | Ne pas mettre à jour le manifeste avant le téléversement. |
| `--push-option <string>` | Option de push supplémentaire. |
| `--ready` | Marquer le changement comme prêt. |
| `-R`, `--reviewers <string>` | Demander des revues à ces utilisateurs, séparés par `;`. |
| `--ssl-verify` | Vérifier les certificats SSL. |
| `-T`, `--title <string>` | Titre de la merge request. |
| `--topic <string>` | Topic du super MR ou de la demande de changement. Par défaut : branche locale. |
| `--wip` | Téléverser comme travail en cours. |

## Options globales

| Option | Description |
| --- | --- |
| `-C`, `--dir <string>` | Exécuter comme si `git-mm` avait démarré dans ce répertoire. |
| `--git-path <string>` | Chemin du binaire Git. |
| `-q`, `--quiet` | Mode silencieux. |
| `--root-dir` | Afficher le répertoire racine du manifeste courant. |
| `--timeout <string>` | Délai d'attente de commande avec suffixe `s/m/h`. |
| `--trace` | Afficher les messages de trace. |
| `--verbose` | Opposé de `--quiet`. |
| `--verbosity-level <count>` | Verbosité du journal : `INFO`, `DEBUG` ou `TRACE`. |
| `--version` | Afficher la version de git-mm. |
| `-y`, `--yes` | Répondre automatiquement oui aux invites du terminal. |
