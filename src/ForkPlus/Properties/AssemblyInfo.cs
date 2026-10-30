using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

[assembly: ComVisible(false)]
[assembly: InternalsVisibleTo("ForkPlus.Tests")]
[assembly: AssemblyMetadata("SquirrelAwareVersion", "1")]
[assembly: AssemblyCompany("ForkPlus")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCopyright("Copyright © 2018")]
// 版本号说明（2026-10-30，v5.2.3）：修复轨道图合并节点悬浮小窗的两处交互缺陷，应用版本号升至 5.2.3。
//   1) 浮窗内点击展开/收拢后浮窗立即消失——点击触发浮窗内列表 Reload（容器回收对静止鼠标
//      重算命中）产生假 PointerExited；Windows 上 Popup 是独立 HWND（未启用 OverlayPopups），
//      鼠标在浮窗内的真实移动不会到达主窗口顶层，"等真实移动判定关闭"永远无法复位待关闭
//      状态，一动鼠标即被误关。修复：关闭语义改「200ms 缓冲定时器 + 实况二次判定」——任何
//      疑似离开只重启定时器，浮窗内容自行监听 Entered/Moved 移回即取消，tick 时按
//      IsPointerOver 实况决定是否真关（GraphCellView._closePending 状态机）；
//   2) 鼠标在浮窗上滚动滚轮，滚的是外面的轨道图列表——Windows 上 Popup 是独立 HWND 且不
//      激活，Win32 把 WM_MOUSEWHEEL 发给键盘焦点窗口（主窗口），滚轮根本不进浮窗，浮窗内容
//      上的滚轮处理器收不到事件。修复：主窗口顶层 Tunnel 拦截滚轮，鼠标在浮窗上（内容
//      IsPointerOver）时驱动浮窗内部 ScrollViewer 并 Handled 吞掉；overlay 模式由浮窗内容
//      上的处理器兜底，两条路径覆盖全部呈现模式。
//   3) 按住浮窗内滚动条拖动时浮窗消失——Avalonia 拖动 thumb 会捕获指针，拖出浮窗边界后
//      _popupContent.IsPointerOver 变 false，200ms 定时器 tick 实况判定不通过即关窗。修复：
//      跟踪浮窗内按下状态（Pressed 置位，Released/CaptureLost 复位），按下未释放期间
//      ClosePopup 实况判定附加保护，操作浮窗过程中绝不关闭（hardClose 不受影响）。
// 版本号说明（2026-10-23，v5.2.2）：偏好设置 → 插件页新增搜索框，应用版本号升至 5.2.2。
//   搜索框按插件名称、插件描述与匹配扩展名（逐项子串匹配，带不带点均可）不区分大小写
//   过滤插件列表（PluginsUserControl.MatchesPluginFilter，internal static + 8 个回归
//   用例 PluginsSearchFilterTests）；搜索词跨启用/禁用、卸载、重载保留，过滤后无匹配
//   显示空态提示；「全选」只作用于过滤后可见行，已勾选项跨过滤保留（卸载计数为真实
//   卸载数）。本页文案固定中文，搜索框占位文本不随界面语言翻译（与该页既有约定一致）。
// 版本号说明（2026-10-23，v5.2.1）：新增 macOS x64（Intel Mac）发布平台，应用版本号升至 5.2.1。
//   CI 构建矩阵在 macos-latest（Apple Silicon）runner 上交叉 publish -r osx-x64（.NET
//   runtime pack 走 NuGet，交叉发布与宿主架构无关）；biturbo 原生库自 Biturbo v1.1.4 起
//   提供 libbiturbo-x64.dylib（RestoreBiturbo 按 $(RuntimeIdentifier)=osx-x64 分流拉取，
//   落地为 libbiturbo.dylib，运行时 DllImport 库名不变）；SkiaSharp / HarfBuzzSharp /
//   Avalonia.Native 的 macOS dylib 均为 universal fat（x86_64+arm64 双切片）无需处理；
//   tokei 直接使用现成 tokei-x86_64-apple-darwin 资产（osx-arm64 本就经 Rosetta 运行它，
//   osx-x64 反而是原生执行）。自动更新按 GetCurrentPlatformId()（Intel Mac 返回
//   macos-x64）匹配 ForkPlus-<版本>-macos-x64.zip，零代码改动。
//   另随包带入 v5.2.0 后的 CI 修复：GitExecutableResolver shim 测试按 OS 分支（非
//   Windows 断言 OS 守卫生效），分片守卫归一化免疫第三变体（vstest 错误行拼入用例名行尾）。
// 版本号说明（2026-10-23，v5.2.0）：新增提交签名与空提交支持，收口 AI 窗口行为，修复
//   worktree 分支显示与打开仓库慢等四项。
//   新增：① 提交签名——提交面板 ⋯ 菜单新增「Sign Commit」（读写本仓库 git config
//   commit.gpgsign --local）与「Signing Key」（选择签名密钥，无 SSH 密钥时提示），
//   squash / AI WIP 拆分 / amend 等所有 git commit 路径由 git 自身读配置天然继承；
//   提交详情面板按 git %G? / %GS 显示签名徽标（Verified / Unverified / Bad Signature /
//   Expired / Revoked 等）与签名者；创建标签新增「Sign」选项（git tag -s），预览命令同步
//   展示，勾选状态经 CreateTag_Sign 持久化。
//   ② 空提交——提交面板新增「Allow Empty Commit」（--allow-empty）与「Skip Commit Hooks」
//   （--no-verify）复选框，勾选空提交后无暂存文件也放行提交按钮。
//   ③ AI 窗口行为收口（AiWindowService）——AI 解释 commit / AI PR 描述 / AI 检视 /
//   AI 辅助开发 / AI 提交编排统一为：非模态、owner=主窗口、统一尺寸 1080×720、居中主窗口
//   所在屏幕、主窗口最大化时跟随最大化、显示在任务栏；未配置 AI 统一拦截（提示框带
//   「打开偏好设置」深链直达 AI Enhancement 标签）；右上角模型下拉框统一
//   （AiModelComboBoxHelper：占位 + 后台拉 /v1/models + 切换保存）。
//   修复：① 链接 worktree 分支显示错误——bt 枚举 refs 改用 common dir
//   （GitModule.ReferencesGitDir），HEAD symref 按「本 worktree 自己的 HEAD」修正
//   （BtReferencesExtensions.ResolveRepositoryHead），detached HEAD 时 UI 与 git 一致显示
//   detached，不再把主 worktree 的分支显示成本仓库当前分支；
//   ② 打开仓库慢（轨道图刷出来慢）——HEAD unborn 检测由 git rev-parse --verify HEAD
//   （一次 spawn 在进程创建被终端安全软件拦截的机器上实测 1~2.4s，占打开仓库 3.3s 数据
//   管线的 2.4s）改为纯文件检查 GitModule.IsHeadUnborn()，不再 spawn 子进程；
//   ③ loading 动画台阶感——CircularProgressBar 采样间隔 30ms→16ms（对齐 60fps），并补
//   峰前爬升段（0.3→1），不再「突然闪白再淡出」；
//   ④ 历史列表作者颜色框中间断开——头像半框与作者半框间 4px 空档致边框不闭合，对齐
//   WPF 原版闭合结构；详情刷新后作者邮箱选色弹窗对账（旧邮箱弹窗自动关闭、延迟开窗补开）。
// 版本号说明（2026-10-20，v5.1.6）：修复 FileDiff 视图选区浮窗范围虚高——选中 2 行
//   时暂存/丢弃浮窗（及块背景）覆盖约 4 行。根因：ChunkSelectionLayer.DrawSelectionBorder
//   对 BackgroundGeometryBuilder.GetRectsForSegment 返回的全部矩形做高度累加
//   （num2 += item.Height），而 AvaloniaEdit 12 在 extendToFullWidthAtLineEnd=true 时对
//   选区内每一可视行返回多个矩形（文本段矩形 + 行尾全宽扩展矩形，同一 Top、各自整行高），
//   选区末行之后还多一个零宽矩形——2 行选区实测 3~5 个矩形，高度累加把浮窗高放大
//   3~5 倍行高。改为取全部矩形的纵向并集跨度（min Top → max Bottom），同一行内重复
//   矩形共享 Top/Bottom，并集即选区真实范围；另加空矩形列表提前返回（选区整体滚出
//   视口时不再把浮窗钉在编辑器顶部）。
// 版本号说明（2026-10-20，v5.1.5）：修复插件卸载确认弹窗点「卸载」后偏好设置窗口被
//   主窗口压到下面（观感"点确认没用"）——ForkPlusDialogWindow 构造把模态 owner 硬登记
//   主窗口（显式通道），WPF 风格 ShowDialog() shim 解析中显式登记永远赢，嵌套模态
//   （设置窗口里弹确认框）确认框 owner 错挂主窗口，关闭时激活主窗口冲到 Z 序最前。
//   现构造期登记降级为弱兜底（SetDefaultOwnerCompat），shim owner 解析改四级：显式
//   登记 > 模态栈顶（shim 打开且仍可见的最内层父对话框）> 活动窗口 > 弱兜底；另修复
//   CustomWindow 打开期自动居中与 shim 定位的双重定位竞争。
// 版本号说明（2026-10-18，v5.1.5）：修复此仓库设置页（GeneralUserControl）「Diff View」
//   分区标题与「Tab Width」输入行叠在同一行——Grid 只定义到 Row 14，Tab Width 行放在
//   越界的 Row 15，Avalonia 将越界 Row 钳制到最后一行所致；补足第 16 行定义即可。
//   同版修复变更文件树/列表里删除文件显示黄色铅笔（Modify）图标而非删除图标——
//   ChangeTypeExtensions.GetIconKey 对 Deleted 返回 IconKeys.StatusRemove（"Status.Remove"），
//   与 StatusDelete（"Status.Delete"）是两个不同常量，BridgeExtensions 图标桥接 switch
//   只认后者 → 落入默认分支返回铅笔；补上 StatusRemove => DeletedIcon 分支。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.1.5"；
//   程序集标识与文件版本同为 5.1.5（.0）。
// 版本号说明（2026-10-18，v5.1.4）：修复 FileDiff（暂存对比）视图 Stage/Unstage 浮窗
//   飘来飘去的问题。两类漂移根因与修复：
//   A) 闪到编辑器顶部再跳回——InvalidateAdornerVisibility 在保留分支调用
//      ShowChunkAdorner(0.0)，浮窗先被定位到 Y≈15 的编辑器顶部，下一帧 Render 才回到
//      块顶部；现移除该调用，定位一律由 Render 阶段（DrawChunk/DrawSelectionBorder）给出。
//   B) remove/recreate 循环——指针落在浮窗上滚动时 MouseLeave 清空 _lastPointerPosition，
//      RefreshActiveChunk 取不到块 → RemoveChunkAdorner 把浮窗从指针下删掉，编辑器
//      PointerEntered 又立刻重建，反复闪烁；现保留条件追加 _isPointerOverAdorner，
//      子类 RefreshActiveChunk 在指针位于浮窗上时保持锚定块不变，且
//      QueueAdornerLayerUpdate 优先级提升到 Render 消除滚动时滞后一帧。
//   覆盖 ChunkSelectionLayer 基类与 DiffSelectionLayer / SideBySideCommitDiffSelectionLayer /
//   MergeChunkSelectionLayer 三个子类（split、side-by-side、冲突合并视图一致）。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.1.4"；
//   程序集标识与文件版本同为 5.1.4（.0）。
// 版本号说明（2026-10-13，v5.1.3）：修复提交信息 emoji（gitmoji）自动补全的两处小问题：
//   A) 鼠标点击浮层里的 emoji 建议项无反应（只有键盘 ↑↓ + 回车/Tab 生效）——真实点击序中
//      按下会让键盘焦点离开文本框，触发 ClosePopup() 在释放事件到达之前拆掉浮层，点击落空；
//      现改在 PointerPressed 阶段命中即应用（并修 FocusChanged 不再因焦点落到浮层而误关）；
//   B) 提交描述框输入 ":" 不弹 emoji 选择器——现描述框在 Co-authored-by 补全之后追加注册
//      gitmoji 提供器，输入 ":" 同样触发。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.1.3"；
//   程序集标识与文件版本同为 5.1.3（.0）。
// 版本号说明（2026-10-13，v5.1.2）：对比视图去重——窗口重新获得焦点会触发
//   RepositoryUserControl 刷新（重拉变更列表后重新选中同一文件），此前无条件重发
//   内容给插件视图，导致 Hex/图片/视频等重内容视图被全量重建（重新解码、丢"加载更多"
//   进度、视图模式被重置为并排），既耗性能又影响体验。本版做两层去重（保守：能证明
//   两份内容对视图渲染等价才跳过，否则照常重发，避免漏刷新）：
//   A) 宿主侧内容指纹：PluginDiffViewControl.UpdateDiff 在 SetContent 前按
//      DiffViewContentFingerprint（路径/角色/声明大小/LFS 指针/标题画色/字节 SHA-256）
//      比对，与上次相同即早退；
//   B) 插件侧幂等守卫：Hex/图片/视频视图 SetContent 开头算同一口径的内容签名，相同即早退。
//   提示：视频对比插件在独立仓 ForkPlus-Plugins，随本版发布 1.2.4。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.1.2"；
//   程序集标识与文件版本同为 5.1.2（.0）。
// 版本号说明（2026-10-13，v5.1.1）：对比视图插件新增 HTML 预览能力——共享视图组件
//   （ForkPlus.Plugins.Ui）内新增 HtmlPreview：并排双 WebView 预览控件
//   （SideBySideHtmlPreview）+ 原生 WebView 可用性探测（HtmlWebViewSupport）+
//   HTML 资源内联器（HtmlResourceInliner），走官方 Avalonia.Controls.WebView
//   （NativeWebView，Win=WebView2 / macOS=WKWebView / Linux=WPE WebKit 或 WebKitGTK）。
//   CHM / EPUB 对比视图插件借此新增「预览」模式，左右并排渲染容器内的 HTML 页面
//   （引用资源内联为 data URI）；WebView 不可用（headless 测试 / 未装运行时）时
//   只显示提示文案，不渲染页面。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.1.1"；
//   程序集标识与文件版本同为 5.1.1（.0）。
// 版本号说明（2026-10-12，v5.1.0）：对比视图插件架构升级——每插件独占一个目录
//   （plugins/<AssemblyName>/，目录根带 plugin.json 清单），宿主只加载/安装具备清单且
//   minHostVersion 兼容的插件；每个插件用独立可回收的 AssemblyLoadContext 加载，私有
//   依赖互不打架、卸载即整目录删除。偏好设置 → 插件页新增「插件版本」「最低支持主软件
//   版本」列，内置插件（图片/Hex）自本版起也可卸载并从插件仓重装。首次启动检测到旧版
//   平铺插件布局时清空 plugins/ 目录并弹窗告知需从插件仓 1.2.0 重装。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.1.0"；
//   程序集标识与文件版本同为 5.1.0（.0）。
// 版本号说明（2026-10-09，v5.0.7）：贮藏右键菜单新增「另存为补丁」，并把远端分支
//   删除状态栏的硬编码英文文案国际化。导出用 git stash show -p --binary（二进制改动
//   完整可应用），贮藏含未跟踪文件时加 --include-untracked（git 2.32+），老版本 git
//   自动降级为仅导出已跟踪改动；交互复用工作区「另存为补丁」的选路径 + 进度弹窗 +
//   后台写盘，并记录 RecentPatchDirectory。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.7"；
//   程序集标识与文件版本同为 5.0.7（.0）。
// 版本号说明（2026-10-08，v5.0.6）：修复 Windows 上自动更新"点 Download 闪一下、
//   原地踏步"——AutoUpdateRunner 命令行拼装的 --install-dir 值以 \ 结尾（Windows 的
//   AppContext.BaseDirectory 必然如此），按 Windows 规则吃掉结束引号并把后随的
//   --restart-command 段吞进该值，updater 参数解析失败、在连进度管道之前以退出码 1
//   退出且一条消息都不发；主窗口又把"无消息早退"当用户取消静默复位。修复：拼命令行
//   时把结尾连续反斜杠翻倍；同时 UpdateAvailableWindow 读退出码，非用户取消且非 0
//   时按失败报错（与手动检查窗口的"重置此版本"口径一致）。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.6"；
//   程序集标识与文件版本同为 5.0.6（.0）。
// 版本号说明（2026-10-08，v5.0.5）：让文本差异也能命中对比视图插件——
//   文本文件（如 .json / .dbc）默认仍由内置文本编辑器渲染，但若插件被用户绑定、
//   或以精确扩展名声明认领，则由 FileDiffControl / CommitFileDiffControl 加载两侧
//   字节并转交 PluginDiffViewControl；候选经 DiffViewPluginRegistry.ResolveClaimed
//   解析（用户绑定 > 精确扩展名，不含 "*" 通配兜底，避免 Hex 兜底把文本抢走）。
//   同时把 MaxHexDiffSize 由 50MB 提到 100MB，扩大非图片二进制插件的字节供给范围。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.5"；
//   程序集标识与文件版本同为 5.0.5（.0）。
[assembly: AssemblyFileVersion("5.2.3")]
[assembly: AssemblyInformationalVersion("5.2.3")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("5.2.3.0")]
