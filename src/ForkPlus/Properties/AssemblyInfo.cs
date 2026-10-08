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
// 版本号说明（2026-10-08，v5.0.4）：修复内置对比视图插件漏发的热修复—
//   v5.0.3 四平台发布包均未包含 plugins/ 目录：ForkPlus.csproj 的插件拷贝目标只按
//   RID 子目录找源文件，而插件是类库工程、产物落在无 RID 的 net10.0/ 根目录，
//   导致 Exists 守卫恒 false → Copy 空转 → 宿主一个插件都加载不到。本版修正拷贝
//   目标（RID 路径缺失时回退根目录），并在 CI 增加 plugins/*.dll 发布门禁防回归。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.4"；
//   程序集标识与文件版本同为 5.0.4（.0）。
[assembly: AssemblyFileVersion("5.0.4")]
[assembly: AssemblyInformationalVersion("5.0.4")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("5.0.4.0")]
