#!/usr/bin/env bash
# 准备内置（embedded）git + git-ai：从分发仓库下载对应平台产物，
# 解压/整理后放到 <stage>/embedded/ 下，随后由 build.yml 拷入 publish/<rid>/embedded/。
#
# 发布策略（2026-09-11 回退）：
#   - Windows：内置 git（完整 git-for-windows 树）+ git-ai —— 解决目标机无 git / 版本过老问题。
#   - Linux / macOS：只内置 git-ai；git 用用户自己环境中的（embedded/git 不存在时运行时
#     自动穿透回退，见 App.EmbeddedGitExecutablePath）。理由：用户机为无桌面发行版/精简环境
#     的概率低，且自带的系统包管理器安装 git 最成熟；避免把 glibc 等动态依赖打进发布物
#     带来的体积与跨发行版兼容负担。
#
# 布局（运行时 App.EmbeddedGitExecutablePath / EmbeddedGitAiExecutablePath 依赖）：
#   embedded/git/bin/git.exe  —— 仅 Windows 的完整 git 树入口
#   embedded/git-ai/git-ai(.exe)—— git-ai 单二进制（全平台）
#
# 用法：prepare-embedded-git.sh <matrix-name> <stage-dir>
#   matrix-name ∈ windows-x64|linux-x64|linux-arm64|macos-arm64
#   stage-dir  内嵌中间目录（脚本在其下创建 embedded/）
set -euo pipefail

MATRIX="$1"
STAGE="$2"
EMB="$STAGE/embedded"
gitai_OK="$EMB/git-ai"

# 分发仓库 release 下载前缀
GIT_BASE="https://github.com/hebin123456/git-release/releases/download/v2.55.0"
GIT_AI_BASE="https://github.com/hebin123456/git-ai-release/releases/download/v1.7.5"

dl() { # dl <base> <asset> <dest>
  local base="$1" asset="$2" dest="$3"
  mkdir -p "$(dirname "$dest")"
  echo ">> download $asset"
  ok=0
  for i in 1 2 3 4 5; do
    if curl -fsSL --retry 3 "$base/$asset" -o "$dest.dl"; then ok=1; break; fi
    echo "   attempt $i failed, retrying in $((i*5))s..."; sleep $((i*5))
  done
  [ "$ok" = "1" ] || { echo "FAILED to download $asset"; exit 1; }
  mv "$dest.dl" "$dest"
}

# 前置校验函数：确保 stage 干净、关键 path exists
require() { [ -e "$1" ] || { echo "MISSING: $1"; exit 1; }; }

mkdir -p "$EMB" 2>/dev/null || true
# 清理可能残留的旧解压（幂等）
rm -rf "$EMB/git-ai" "$EMB/git"

# ---------- git-ai（所有平台为单二进制，静止/静态，直接落位） ----------
# git-ai release 资产按架构命名：windows-x64/arm64、linux-x64/arm64、macos-x64/arm64
case "$MATRIX" in
  windows-x64) ai_asset="git-ai-windows-x64.exe";   ai_name="git-ai.exe" ;;
  linux-x64)   ai_asset="git-ai-linux-x64";         ai_name="git-ai"     ;;
  linux-arm64) ai_asset="git-ai-linux-arm64";       ai_name="git-ai"     ;;
  macos-arm64) ai_asset="git-ai-macos-arm64";       ai_name="git-ai"     ;;
  *) echo "unknown matrix: $MATRIX"; exit 1 ;;
esac
dl "$GIT_AI_BASE" "$ai_asset" "$EMB/$ai_asset"
mkdir -p "$gitai_OK"
mv "$EMB/$ai_asset" "$gitai_OK/$ai_name"
chmod +x "$gitai_OK/$ai_name" || true
require "$gitai_OK/$ai_name"

# ---------- git：仅 Windows 内置；Linux/macOS 用用户环境 git（不打包，运行时回退） ----------
case "$MATRIX" in
  windows-x64)
    # git-for-windows portable 完整树；入口 bin/git.exe（GetSshKeygenPath 由此推导 git 根）
    git_OK="$EMB/git"
    dl "$GIT_BASE" "git-windows-x64.tar.bz2" "$EMB/git.bz2"
    mkdir -p "$git_OK"
    echo ">> extracting windows git tree"
    tar -xjf "$EMB/git.bz2" -C "$git_OK"
    rm -rf "$EMB/git.bz2"
    require "$git_OK/bin/git.exe"
    require "$git_OK/cmd/git.exe"
    echo "=== embedded git entry (win) ==="
    ls -la "$git_OK/bin/git.exe"
    ;;
  linux-x64|linux-arm64|macos-arm64)
    # Linux/macOS：不内置 git，运行时回退用户环境 git（System PATH / 设置实例）。
    echo ">> $MATRIX: skip builtin git (use user's system git via runtime fallback)"
    ;;
  *) echo "unknown matrix: $MATRIX"; exit 1 ;;
esac

echo "=== embedded tree (top) ==="
ls -la "$EMB"
echo "=== embedded git-ai ==="
ls -la "$gitai_OK"
echo "PREPARE_EMBEDDED_OK"