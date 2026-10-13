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
[assembly: AssemblyFileVersion("5.1.3")]
[assembly: AssemblyInformationalVersion("5.1.3")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("5.1.3.0")]
