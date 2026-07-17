# ForkPlus User Manual

> Version: 2.1.0
> For Windows 10+ / .NET 4.7.2 / Git 2.31+ (git-mm 3.0+ recommended)

ForkPlus is an enhanced Git GUI based on Fork, with the underlying engine rewritten in Rust (biturbo). It features AI-assisted development, code statistics, repository treemap, multi-theme skins, and 8 built-in languages. This manual is organized by feature module to help you get started quickly.

---

## Table of Contents

1. [Main Window Overview](#1-main-window-overview)
2. [Repository Management](#2-repository-management)
3. [Branches and References](#3-branches-and-references)
4. [Commit and Changes](#4-commit-and-changes)
5. [Diff and Merge](#5-diff-and-merge)
6. [AI-Assisted Development](#6-ai-assisted-development)
7. [AI Code Review](#7-ai-code-review)
8. [Repository Statistics and Visualization](#8-repository-statistics-and-visualization)
9. [Themes and Custom Colors](#9-themes-and-custom-colors)
10. [Multi-Language Support](#10-multi-language-support)
11. [Workspaces](#11-workspaces)
12. [Custom Commands](#12-custom-commands)
13. [Quick Launch](#13-quick-launch)
14. [External Tool Integration](#14-external-tool-integration)
15. [Preferences](#15-preferences)
16. [Keyboard Shortcuts](#16-keyboard-shortcuts)
17. [Git Flow and Lean Branching](#17-git-flow-and-lean-branching)

---

## 1. Main Window Overview

The ForkPlus main window is divided into four areas:

```
┌─────────────────────────────────────────────┐
│  Title bar menu (File / View / Repository / Window / Help) │
├─────────────────────────────────────────────┤
│  Toolbar (Fetch/Pull/Push/Stash/Branch/Appearance/Workspaces/Open in/AI) │
├──────────┬──────────────────────────────────┤
│          │                                  │
│ Sidebar  │        Main area                 │
│ (Branches/│   (Commit list + Diff detail)    │
│  Search/PR)│                                │
│          │                                  │
├──────────┴──────────────────────────────────┤
│  Bottom status bar                           │
└─────────────────────────────────────────────┘
```

- **Title bar menu**: File / View / Repository / Window / Help, lazily built (constructed on click)
- **Toolbar**: Daily Git operations + appearance switching + workspace switching + external open + AI entry
- **Sidebar**: Branch/remote/tag/stash/submodule/worktree navigation, top three-way switch (Branches / Search Commits / Pull Requests)
- **Main area**: Commit list (All Commits view) or changes list (Changes view) + Diff detail
- **Multi-tab support**: Open multiple repositories simultaneously, Ctrl+T for new tab, Ctrl+W to close

---

## 2. Repository Management

### Opening a Repository
- **File → Open Repository** (Ctrl+O): Select a local repository directory
- **Drag & drop**: Drag a repository folder onto the main window
- **Quick Launch**: Ctrl+P to open the command palette, search for a repository name to open directly
- **File → Create Repository** (Ctrl+Shift+N): Initialize a new repository
- **File → Clone**: Clone a remote repository
- **File → Init GitMm Repository** (Ctrl+G): Initialize a git mm workflow repository (requires git-mm 3.0+)

### Multi-tab
- One tab per repository, Ctrl+Tab / Ctrl+Shift+Tab to switch between tabs
- Ctrl+T for a new blank tab, Ctrl+W to close the current tab
- Window position and state are persisted automatically

### Background Auto-Fetch
- Background fetch is enabled by default, automatically fetching remote updates (without auto-merge)
- Frequency can be adjusted or disabled in Preferences

---

## 3. Branches and References

The left side of the sidebar is the reference navigation, containing 7 groups:

| Group | Content |
|---|---|
| **Pinned** | Pinned frequently-used branches |
| **Branches** | Local branches |
| **Remotes** | Remote branches |
| **Tags** | Tags |
| **Stashes** | Stash list (most recent 15) |
| **Submodules** | Submodules |
| **Worktrees** | Worktrees |

### Sort and Filter
- Each group can be sorted alphabetically (forward/reverse) or by recently used
- Top Filter box (Ctrl+F to focus, Esc to clear) filters in real time
- Collapses when over 20 items, click "Show all" to expand

### Double-click Behavior
- Branch/Tag: Checkout
- Stash: Apply
- Submodule/Worktree: Open

### Context Menu (Local Branch)
Checkout, Fast-Forward, Pull, Push, Create PR, Check Remote Sync Status (searchable submenu), Merge, Rebase, Interactive Rebase, Tracking, Rename, Delete, AI Code Review, Code statistics, Copy Name, Custom Commands

### Drag and Drop
- Drag a branch onto a local branch: pops up Merge / Rebase menu
- Drag a local branch onto a remote branch: pops up Rebase menu

### Create Branch / Tag / Worktree
- Toolbar Branch dropdown → Create Branch / Create Tag / Create Worktree
- Repository menu → Create Branch (Ctrl+Shift+B) / Create Tag (Ctrl+Shift+T)

---

## 4. Commit and Changes

Switch to the **Changes view** (top of sidebar "Changes" or Ctrl+1) to enter the commit panel.

### File Staging
- **Unstaged area**: Shows working directory changes
- **Staged area**: Shows changes to be committed
- **Stage / Unstage**: Double-click a file to toggle, or Ctrl+S (stage) / Ctrl+Shift+S (unstage) / Ctrl+Alt+Shift+S (stage all)
- **Discard**: Backspace or Ctrl+Shift+D to discard changes
- Triggers a long-operation prompt when over 5000 files

### Commit Message
- **Subject + Description**: Separated by a blank line, auto-detected
- **Gitmoji completion**: Type `:` to trigger the emoji picker
- **History completion**: Auto-completes based on past commits
- **Reference name completion**: Auto-completes branch/tag names
- **prepare-commit-msg hook**: Supported

### Commit Modes
- **Normal commit**: Ctrl+Enter
- **Commit + Push**: Ctrl+Shift+Enter (hold Shift to invert the default `PushAutomaticallyOnCommit` behavior)
- **Amend**: Check the Amend checkbox to revise the last commit
- **Squash**: Based on the in-progress squash state

### Cherry-pick / Revert / Reset
- Right-click a commit in the commit list:
  - **Cherry-pick**: Apply that commit's changes to the current branch
  - **Revert**: Create a reverse commit
  - **Reset**: Reset the current branch to that commit (soft/mixed/hard)

---

## 5. Diff and Merge

### Diff Views
ForkPlus provides multiple diff view modes:
- **Single-file text diff**: Top/bottom comparison
- **Side by side diff**: Left/right two columns
- **Split diff**: Split mode
- **Syntax-highlighted diff**: DiffCodeEditor with code coloring

### External Diff Tool
- Select a file and press **Ctrl+D** to invoke the configured external diff tool
- Configure in Preferences → Integration

### Merge Conflicts
- Automatically pops up a list of available merge tools when encountering `ChangeType.Unmerged`
- Supports common tools like VS Code, Beyond Compare, KDiff3

### Apply Patch
- **Ctrl+V** paste auto-detects `diff ` or `From ` prefix and triggers the Apply Patch flow

---

## 6. AI-Assisted Development

ForkPlus has a built-in multi-turn conversational AI programming assistant that can directly read and write files in the current repository.

### Entry
Toolbar **AI-Assisted Development** button (shows a prompt guiding to Preferences if API is not configured)

### Prerequisite
Preferences → AI Enhancement: Fill in API endpoint, API key, default model

### Core Capabilities
- **Multi-turn conversation**: Keeps the most recent 20 messages, auto-compresses summaries when too long
- **Streaming output**: Renders AI replies in real time (400ms throttle)
- **Request queue**: Continue typing while processing, queue count displayed
- **Model selection**: Top dropdown, fetches `/v1/models` list in the background
- **Direct file read/write**: AI outputs using `===FILE: path===` + code block format, auto-written to disk
- **Path safety**: Restricted to current repo + parent repo + sibling submodules + own submodules, preventing path traversal
- **Line ending normalization**: Auto-detects and preserves the original file's line ending style
- **Undo**: Click Undo to write back `_lastBeforeContents`, reverting AI's changes
- **Clear Conversation**: Clears conversation history and UI

### Usage
1. Click the AI button on the toolbar to open the conversation window
2. Type a request (e.g., "add a logging decorator to utils.py")
3. AI streams code and writes to the file
4. Review the diff, keep if satisfied, or Undo if not

---

## 7. AI Code Review

Performs AI code review on branch ranges, SHA ranges, or file lists, extracts suggestions, and supports preview/apply.

### Entry
Right-click a local or remote branch in the sidebar → **AI Code Review**

### Three Review Targets
- **Branch**: changes from src branch..dst branch
- **ShaRange**: specified start/end SHA range
- **Files**: selected file list

### Core Capabilities
- **Streaming render**: Displays review report in real time (Markdown→HTML via native engine)
- **Suggestion extraction**: Parses ```forkplus-ai-suggestions JSON blocks, extracts file/line/comment/oldText/newText
- **Preview suggestions**: Click a suggestion to preview changes
- **Apply suggestions**: Click Apply to modify the file directly
- **File review mode**: Left file tree + right diff, filter suggestions by file
- **Retry**: Retry review on a single file or all
- **Completion notification**: Sends a Windows toast notification when review completes
- **Window layout persistence**: Position and column widths auto-saved

---

## 8. Repository Statistics and Visualization

### 8.1 Repository Statistics Panel

Entry: Repository menu → Repository Statistics

Contains:
- **Author commit line chart** (Top 20 authors)
- **Author commit pie chart**
- **By day-of-week bar chart** (commit activity)
- **By hour bar chart** (commit distribution throughout the day)
- **Contribution heatmap** (GitHub-style 53×7 grid, color depth indicates commits that day)
- **Date range filter**: DateRangeButton to filter the statistics range

All charts auto-refresh colors on theme switch.

### 8.2 Code Line Statistics (tokei integration)

The "Code lines" area below the statistics panel integrates [tokei](https://github.com/XAMPPRocky/tokei) (a Rust code statistics tool supporting 200+ languages).

- **Ref dropdown**: Workspace (current working directory snapshot) / local branches / tags
- **Search box**: Top of Popup, filters branches/tags (Workspace item always retained)
- **Pie chart**: By language code lines, Top 12 + Other
- **List**: Language / Files / Code / Comments / Blanks / color block
- **Summary**: `{ref}: {files} files · {code} code · {comments} comments · {blanks} blanks`

Historical ref mode uses `git archive` to export a snapshot and then runs tokei, without polluting the working directory.

Right-click a branch → **Code statistics** to open statistics with that branch as the ref.

### 8.3 Repository Treemap

Entry: Repository menu → Repository Overview

**What is a Treemap**: A rectangle-based tree visualization. The canvas is recursively cut into small rectangles, each rectangle's area ∝ the value it represents, nested by folder/file hierarchy.

**What it shows here**:
- Grouped by file/folder path hierarchy
- **Each rectangle's area ∝ the number of commits that modified that path** (not file size)
- See at a glance where the codebase's changes are concentrated

**Usage**:
- Hover to show path + commit count
- Click a rectangle to drill down; the right side shows the commits list + author stats for that path
- DateRangeButton to filter by date range

**Example**: A repo with 100 commits, `src/` modified by 80 takes 80% of the canvas, `docs/` by 15 takes 15%, `README.md` by 5 takes 5%—the larger the rectangle, the more active the changes.

---

## 9. Themes and Custom Colors

### 9.1 Preset Themes (12)

Entry: Toolbar Appearance dropdown → Theme menu; or Window menu → Switch Theme; Ctrl+Click the toolbar appearance button for quick switch

| Theme | Base | Style |
|---|---|---|
| Light | Light | Default light |
| Dark | Dark | Default dark |
| Solarized Light | Light | Solarized classic palette |
| Solarized Dark | Dark | Solarized dark |
| GitHub Light | Light | GitHub official light |
| GitHub Dark | Dark | GitHub official dark |
| Dracula | Dark | Deep purple-black + pink-purple accent |
| Monokai | Dark | Classic Monokai deep gray + green-orange accent |
| Purple Light | Light | Purple light |
| Purple Dark | Dark | Purple dark |
| Green Light | Light | Green light |
| Green Dark | Dark | Green dark |

Theme switching takes effect immediately; all controls (including diff highlighting, line numbers, heatmap, syntax coloring) follow.

### 9.2 Custom Colors

Entry: Toolbar Appearance → **Custom Colors...** (checkable item)

**Function**: Override 30 core colors on top of any preset theme, including:
- Background, panel, border, text, accent, icon colors
- Diff added/removed colors, exact added/removed colors
- Line number color, separator color, code block selection color
- Syntax highlighting (comment/string/keyword/number)
- Editor background/foreground, window background, title bar background

**Usage**:
1. Click Custom Colors... to open the edit dialog
2. Each color supports hex input + HSV palette (saturation×value 2D square + hue bar)
3. **Changes take effect immediately**, no restart needed
4. **Random Palette**: Bottom "Random Palette" button, generates a harmonious color scheme based on a random base hue + complementary color algorithm (background/text/accent/diff/syntax all derived), ensuring unified tone and readability
5. **Reset**: Single reset / Reset All
6. OK to save, Cancel to undo all changes

**Mutual Exclusion Semantics**:
- When custom colors are enabled, the "Custom Colors" menu item is checked, all theme items are unchecked
- Switching themes automatically disables custom color override and uses the new theme's original colors (custom config is retained, can be re-checked to restore)

---

## 10. Multi-Language Support

Entry: Toolbar Appearance → Language menu

8 built-in languages:
- English
- 简体中文 (Simplified Chinese)
- 繁體中文 (Traditional Chinese)
- 日本語 (Japanese)
- 한국어 (Korean)
- Français (French)
- Deutsch (German)
- Español (Spanish)

JSON-based extensible system; you can add new language files in the `Languages/` directory to extend more languages. The entire UI updates immediately after switching languages.

---

## 11. Workspaces

Organize multiple related repositories into workspaces for quick switching.

### Entry
Toolbar **Workspaces** dropdown button; Ctrl+Click for quick switch

### Features
- Workspaces support nested folder menus
- After switching, the toolbar shows the current workspace name
- Integrated into the Quick Launch command palette, Ctrl+P to search workspace names
- Workspace list persisted to settings.json

---

## 12. Custom Commands

Users can define their own commands and bind them to different targets.

### Entry
- Preferences → Custom Commands Tab
- Repository menu → Custom Commands
- Sidebar right-click → Custom Commands

### 5 Target Types
| Target | Default Command | Description |
|---|---|---|
| Commit (Revision) | `git show ${sha}` | Execute on selected commit |
| Repository | Open URL | Execute on the whole repository |
| File (RepositoryFile) | `git diff ${file}` | Execute on selected file |
| Reference (Branch) | `git diff HEAD ${ref}` | Execute on branch/tag, supports LocalBranch/RemoteBranch sub-targets |
| Submodule | `git submodule update --remote -- ${submodule}` | Execute on submodule |

### Two Modes
- **Local**: Per-repo, visible only to the current repository, can be set to Shared
- **Global**: Global, visible to all repositories

### Two Actions
- **UI**: With buttons and controls
- **Process**: Execute a shell command

Supports OS restriction (Any / Windows / Mac).

---

## 13. Quick Launch

A command palette similar to VS Code's Command Palette.

### Entry
- Toolbar Quick Launch button
- File menu → Quick Launch
- Shortcut **Ctrl+P**

### Features
- Multi-provider command search: default commands, Git Flow, references, remotes, repository files, workspaces
- Real-time filtering as you type
- Up/Down to navigate, Enter to execute, Esc to close
- Supports parameterized commands (e.g., Checkout Branch then type the branch name)
- Background scan to refresh the repository list

### Hidden Commands
- Type `ftrace` → Enable elapsed time debugging
- Type `crash` → Send crash report

---

## 14. External Tool Integration

Toolbar **Open in** dropdown button:

- **Console**: Open Shell (default or current ShellTool, Ctrl+Alt+T)
- **File Explorer**: File Explorer (Ctrl+Alt+O)
- **External Editors**: VS / VSCode / JetBrains and other installed editors
- **Remote**: Remote repository web view
- **Custom Commands**: Custom commands

Configure the default Shell, external editors, external diff/merge tools in Preferences → Integration.

---

## 15. Preferences

Entry: File → Preferences (Ctrl+,)

### 6 Tabs

| Tab | Content |
|---|---|
| **General** | General settings (language, theme, layout scaling, auto-fetch, etc.) |
| **Commit** | Commit-related (`PushAutomaticallyOnCommit`, `HideUntrackedFiles`, `SkipCommitMessage`, etc.) |
| **AI Enhancement** | AI service configuration (API endpoint, API key, default model) |
| **Git** | Git-related configuration |
| **Integration** | External tools, Shell, external editors, diff/merge tools |
| **Custom Commands** | Custom command management (see Section 12) |

### SSH Keys
File → Configure SSH Keys: Manage SSH keys

### Accounts
File → Accounts: Configure GitHub/GitLab and other accounts (for PRs, remote operations)

---

## 16. Keyboard Shortcuts

Entry: Help → Open Keyboard Shortcuts

### General Navigation
| Shortcut | Function |
|---|---|
| Ctrl+1 / Ctrl+2 / Ctrl+0 | Switch sidebar view |
| Ctrl+P | Quick Launch |
| Ctrl+Tab / Ctrl+Shift+Tab | Switch repository tabs |
| Ctrl+T | New Tab |
| Ctrl+W | Close Tab |
| Ctrl+= / Ctrl+- | Zoom |
| Ctrl+, | Preferences |

### All Commits View
| Shortcut | Function |
|---|---|
| Ctrl+0 | Activate All Commits view |
| Ctrl+F | Focus Filter |
| Enter / F3 | Search next |
| Shift+Enter / F3 | Search previous |
| Delete | Delete branch/tag |

### Changes View
| Shortcut | Function |
|---|---|
| Ctrl+1 | Switch to Changes view |
| Ctrl+F | Focus Filter |
| Ctrl+Enter | Commit |
| Ctrl+Shift+Enter | Commit + Push |
| Enter / Ctrl+S | Stage |
| Ctrl+Shift+S | Unstage |
| Ctrl+Alt+Shift+S | Stage All |
| Backspace / Ctrl+Shift+D | Discard |
| Ctrl+O | Open file |
| Ctrl+D | External diff |

### Repository Operations
| Shortcut | Function |
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

### Repository Manager
| Shortcut | Function |
|---|---|
| F2 | Rename |
| Delete | Remove |
| Enter | Open |

### Special
- **Ctrl+V**: Pasting a patch auto-detects `diff ` / `From ` prefix and triggers Apply Patch
- **Ctrl+Click toolbar appearance button**: Quick switch theme
- **Ctrl+Click toolbar workspace button**: Quick switch workspace
- **Toolbar Fetch/Pull/Push + Ctrl**: Quick variant (executes directly without dialog)

---

## 17. Git Flow and Lean Branching

### Git Flow

Full Git Flow workflow support:

Entry: Toolbar Branch dropdown → Git Flow; or Repository menu → Git Flow

- **Init**: Initialize Git Flow configuration
- **Start Feature / Release / Hotfix**: Start a new branch
- **Finish Feature / Release / Hotfix**: Finish the branch (merge back to develop/main)
- **Deinit**: Remove Git Flow configuration

### Lean Branching (git mm workflow)

A lightweight workflow based on git-mm, requires git-mm 3.0+:

Entry: Toolbar Branch dropdown → Lean Branching

- **Start**: Create a new working branch based on main
- **Sync**: Sync the latest changes from main to the current branch
- **Finish**: Finish the branch (merge and clean up)

---

## Appendix: Technical Architecture

- **Underlying engine**: biturbo (written in Rust, exposed to .NET via P/Invoke), provides commit graph cache, treemap layout, Markdown→HTML, commit search, and more
- **Code statistics**: Integrates tokei (Rust, 200+ languages, compiled via cargo install at build time)
- **UI framework**: WPF (.NET 4.7.2)
- **Charts**: OxyPlot
- **Auto-update**: Squirrel
- **License**: MIT

---

For questions or suggestions, check version info in Help → About, or provide feedback via the project repository.
