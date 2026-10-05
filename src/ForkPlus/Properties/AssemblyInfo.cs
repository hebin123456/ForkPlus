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
// 版本号说明（2026-10-05，v5.0.1）：在 v5.0.0 插件化架构之上新增「插件管理器」——
//   1) 偏好设置新增「插件」页：展示每个对比视图插件的名称/版本号/描述/匹配扩展名/优先级，
//      支持启用/禁用（即时生效并持久化，禁用仅退出路由、条目保留）与「重新加载插件」
//      （重建注册表 + 重扫描 plugins/ 目录，无需重启 ForkPlus）。
//   2) 插件元数据契约 IPluginMetadata（名称/版本/描述）；内置图片对比与 Hex 对比插件
//      版本号 1.0.0，名称/描述为中文（元数据国际化留待后续版本）。
//   3) 版本号 5.0.1 为 v5.0.0 的补丁迭代（功能新增 + 无破坏性变更）。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.1"；
//   程序集标识与文件版本同为 5.0.1（.0）。
[assembly: AssemblyFileVersion("5.0.1")]
[assembly: AssemblyInformationalVersion("5.0.1")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("5.0.1.0")]
