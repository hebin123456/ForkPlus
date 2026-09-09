# 构建指南

本文档说明 ForkPlus 的构建环境、编译步骤、测试运行与本地发布。三方件（biturbo / tokei / OxyPlot.Avalonia）的来源与机制详见 [README.md](README.md) 的「编译」一节，此处只讲怎么用。

## 环境要求

| 依赖 | 版本 | 用途 | 缺失时的影响 |
|------|------|------|-------------|
| .NET 10 SDK | 10.0.x | 编译 / 测试 / 发布 | 无法构建 |
| Git | ≥ 2.44（推荐） | 应用内 git 操作 + 测试 | 低于推荐版本启动时弹警告；应用优先使用内置 git 实例（2.50.1），缺失时回退系统 git。v4.0.5 起老版本 git 自动降级（`--update-refs` 需 2.38+，缺失时变基不带该选项并隐藏开关；变基预检 `git replay` 需 2.44+，缺失时降级为旧式三参数 merge-tree 预演）——E2E 变基模块用例在 git 2.34 上亦可全绿 |
| git-mm | ≥ 3.0 | git mm 工作流 | 该功能不可用（可在偏好设置中配置路径），启动时版本过低会警告 |
| gitflow-avh | 任意近期版本 | 仅测试需要（E2E GitFlow 模块） | 对应测试失败，不影响构建与运行 |
| git-lfs | 任意近期版本 | 仅测试需要（E2E LFS 模块） | 对应测试失败，不影响构建与运行 |

操作系统：Windows 10+ / Linux / macOS 均可（跨平台项目，CI 在三平台均有产物）。

### 安装 .NET 10 SDK

从 [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) 下载对应平台的 SDK 安装。Linux 上若采用 tar.gz 手动安装（装到 `~/.dotnet`），需自行 export：

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

### 安装测试专用依赖（新版 git / gitflow-avh / git-lfs）

git 经 PATH 查找 `git-flow` 等子命令（exec-path 方案无效），安装后确保所在目录在 PATH 里：

```bash
# 新版 git ≥ 2.44（Ubuntu 自带源版本较旧；CI 的 test job 同款步骤）
sudo add-apt-repository -y ppa:git-core/ppa
sudo apt-get update && sudo apt-get install -y git
git --version                              # 验证 ≥ 2.44

# gitflow-avh（源码安装）
git clone --depth 1 https://github.com/petervanderdoes/gitflow-avh.git /tmp/gitflow-avh
sudo make -C /tmp/gitflow-avh install      # 或 PREFIX="$HOME/.local" make install
git flow version                            # 验证

# git-lfs（Linux apt / macOS brew / Windows 官网安装包）
git lfs version                             # 验证
```

## 获取与编译

```bash
git clone https://github.com/hebin123456/ForkPlus.git
cd ForkPlus
dotnet build ForkPlus.sln -c Release
```

首次编译需要网络可达 GitHub：biturbo native 库、tokei、OxyPlot.Avalonia nupkg 三个三方件由构建期 target 自动从对应仓库的 latest Release 拉取到 `third_party/`（已 `.gitignore`，勿手动提交）。拉取失败会直接报错中断；网络不稳时可重试，target 带 Exists 守卫，已就位的文件不会重复下载。

也可用 Visual Studio 2026（Windows，仓库根目录 `OpenForkPlusInVS2026.cmd` 一键打开解决方案）、Rider 或 VS Code 打开 `ForkPlus.sln` 编译。

## 运行

```bash
dotnet run --project src/ForkPlus -c Release
```

调试运行（Debug 配置）同理。应用首次启动会在用户数据目录创建配置（Linux/macOS 为 `~/.local/share/ForkPlus/`）。

## 测试

```bash
dotnet test ForkPlus.sln --nologo
```

- 全量 4500+ 用例，含单元测试与 Avalonia.Headless UI 端到端测试（Skia 软件渲染，无需显示服务器，Linux 服务器上可直接跑）；
- E2E 套件会执行真实 git 管线（含 GitFlow / LFS 链路），需要上表的 gitflow-avh 与 git-lfs；
- 失败时的证据截图落在 `docs/evidence/`（`.gitignore` 已忽略顶层过程截图）；
- CI 在 tag 构建与手动触发时于 ubuntu runner 上以**五分片并行矩阵**跑全量测试（2026-09-09 起，`--filter` 互斥分片：E2E 按模块号十位 ×3 片 + 非 E2E 按类名首字母 ×2 片，另有分片完整性守卫防新增用例漏跑；测试墙钟 ~17min → ~8min），本地与 CI 环境差异见 [`.github/workflows/build.yml`](.github/workflows/build.yml) 注释。

## 本地发布（self-contained）

与 CI 产物一致的自包含发布（自带 .NET 10 运行时，目标机无需安装任何框架）：

```bash
# 平台 RID：win-x64 / linux-x64 / linux-arm64 / osx-arm64
RID=linux-x64

# 主程序（ProjectReference 自动带出 AskPass/RI 的构建，但发布需逐工程自包含到同一目录）
# linux-arm64 时 PlatformTarget 传 arm64（见下方说明）；x64 平台传 x64
dotnet publish src/ForkPlus/ForkPlus.csproj -c Release -r $RID --self-contained true \
  -p:PlatformTarget=x64 -p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none \
  -o publish/$RID --nologo

# AskPass / RI 两个 git 子进程 helper（必须同样自包含发布到同一目录：
# git 以独立进程拉起它们，漏掉则无运行时机器上凭证输入/交互式变基直接失败）
for proj in src/ForkPlus.AskPass/ForkPlus.AskPass.csproj src/ForkPlus.RI/ForkPlus.RI.csproj; do
  dotnet publish $proj -c Release -r $RID --self-contained true \
    -p:PlatformTarget=x64 -p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none \
    -o publish/$RID --nologo
done
```

注意：
- `-r` 不可省略：不带 RID 时 SDK 会把 NuGet 包里全部 18 个 RID 的运行时文件复制进产物，Linux 包从约 125MB 膨胀到 585MB；
- osx-arm64 / linux-arm64 时 `PlatformTarget` 传 `arm64`（csproj 固定 x64 会与 arm64 RID 冲突，NETSDK1032）；linux-arm64 需在原生 ARM64 机器（或 GitHub 原生 ARM64 runner）上发布，或自行配置交叉工具链；
- helper 的发布顺序必须在主程序之后（主工程的 Publish target 会把框架依赖版 helper 拷进输出目录，自包含发布随后覆盖之）。

## 常见问题

**Q：构建报 biturbo / tokei / OxyPlot nupkg 下载失败？**
三方件从 GitHub Release 拉取，需网络可达 GitHub。检查代理设置；重试即可（已下载的文件有 Exists 守卫不会重复拉）。也可手动下载放入 `third_party/` 对应位置（文件名见 README「编译」一节的表格）。

**Q：构建通过但启动弹"Git 版本过旧"？**
系统 git 低于 2.40。安装新版 git，或让应用使用内置 git 实例（`~/.local/share/ForkPlus/gitInstance/2.50.1/bin/git`）。

**Q：测试里 GitFlow / LFS 相关用例失败？**
对应 gitflow-avh / git-lfs 未安装或不在 PATH。见上文「安装测试专用依赖」。跑测试的 shell 必须保证安装目录在 PATH 里（git 经 PATH 查找子命令）。

**Q：Linux 上跑测试需要桌面环境吗？**
不需要。UI 测试基于 Avalonia.Headless + Skia 软件渲染，无显示服务器依赖。
