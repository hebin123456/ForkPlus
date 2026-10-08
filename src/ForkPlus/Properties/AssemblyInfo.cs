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
// 版本号说明（2026-10-08，v5.0.5）：让文本差异也能命中对比视图插件——
//   文本文件（如 .json / .dbc）默认仍由内置文本编辑器渲染，但若插件被用户绑定、
//   或以精确扩展名声明认领，则由 FileDiffControl / CommitFileDiffControl 加载两侧
//   字节并转交 PluginDiffViewControl；候选经 DiffViewPluginRegistry.ResolveClaimed
//   解析（用户绑定 > 精确扩展名，不含 "*" 通配兜底，避免 Hex 兜底把文本抢走）。
//   同时把 MaxHexDiffSize 由 50MB 提到 100MB，扩大非图片二进制插件的字节供给范围。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.5"；
//   程序集标识与文件版本同为 5.0.5（.0）。
[assembly: AssemblyFileVersion("5.0.5")]
[assembly: AssemblyInformationalVersion("5.0.5")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("5.0.5.0")]
