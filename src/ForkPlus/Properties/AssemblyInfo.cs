// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → 移除（ThemeInfo 为 WPF 特有，Avalonia 不需要）
// - using System.Windows.Resources → 移除（pack URI 资源为 WPF 特有）
// - [assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)] → 移除
//   （WPF 主题资源字典定位，Avalonia 改用 ControlTheme / Styles 在 App.axaml 中统一注册）
// - 保留其余程序集属性（版本、公司、元数据等）
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;

[assembly: ComVisible(false)]
[assembly: InternalsVisibleTo("ForkPlus.Tests")]
[assembly: AssemblyMetadata("SquirrelAwareVersion", "1")]
[assembly: AssemblyCompany("ForkPlus")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCopyright("Copyright © 2018")]
[assembly: AssemblyFileVersion("3.6.4")]
[assembly: AssemblyInformationalVersion("3.6.4")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("3.6.4.0")]
