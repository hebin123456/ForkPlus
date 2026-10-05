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
// 版本号说明（2026-10-05，v5.0.0）：本版为对比视图插件化架构——
//   1) 对比视图拆为独立插件 DLL（plugins/ 目录动态加载，主程序不重新编译即可增删）：
//      内置两个——图片对比（forkplus.image：并排/Swipe/洋葱皮/Hex，含动图播放与 LFS）；
//      Hex 对比（forkplus.hex：二进制文件通配兜底，文件卡片 + Hex 切换）。
//   2) 契约层（ForkPlus.Plugins.Abstractions）+ 共享组件层（ForkPlus.Plugins.Ui）+
//      宿主能力桥（PluginEnvironment / IDiffViewHost）：插件不感知 Git 领域类型与主工程。
//   3) 路由：用户绑定（DiffViewPluginRegistry.BindExtension）> 精确扩展名 > 通配兜底。
//   行为与 v4.3.2 一致（图片对比器 + 十六进制对比器两套视图）。详见 RELEASE_NOTE v5.0.0。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.0"；
//   程序集标识与文件版本同为 5.0.0（.0）。
[assembly: AssemblyFileVersion("5.0.0")]
[assembly: AssemblyInformationalVersion("5.0.0")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("5.0.0.0")]
