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
// 版本号说明（2026-10-06，v5.0.3）：插件体系三项增强——
//   1) 插件多语言：宿主把当前界面语言 code 下发给插件（缺省英文），插件可实现
//      IPluginMetadata.GetDisplayName/GetDescription(language) 返回本地化名称/描述，
//      并订阅 PluginEnvironment.LanguageChanged 响应语言热切换。
//   2) 插件安装：偏好设置 → 插件页新增「安装插件...」，选择插件压缩包（zip）后解压、
//      剥离顶层目录、平铺安装到 plugins/ 并重新加载。
//   3) 插件卸载：新增「卸载选中」支持批量卸载，内置的图片/Hex 两个插件受保护不可卸载；
//      文件被占用时延迟到下次启动删除。
//   4) AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.3"；
//   程序集标识与文件版本同为 5.0.3（.0）。
[assembly: AssemblyFileVersion("5.0.3")]
[assembly: AssemblyInformationalVersion("5.0.3")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("5.0.3.0")]
