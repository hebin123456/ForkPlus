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
// 版本号说明（2026-09-11，v4.0.10）：SSH 密钥删除/滚动/统计/首启专项修复——删除 SSH
//   密钥确认框挂错模态链导致置底、点"删除"无效（确认框 owner 改绑当前模态窗口）、确认框
//   文案未翻译（7 语言词条补齐）、所有裸 ScrollViewer 点过滚动条后滚轮滚不动/thumb 不跟随
//   （ScrollViewerWheelFix 附加属性主题层全局接管）、统计柱状图鼠标悬浮无数据提示
//   （Avalonia 原生 tracker 模板恢复）、首次启动偶发细条标题栏弹窗（代理 owner 改无边框）。
//   上一版 v4.0.9 为拖动残留/滚动/窗口状态专项修复；v4.0.8 为跨平台迁移期 UI/交互修复批次。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示
//   "4.0.10"；程序集标识与文件版本同为 4.0.10（.0）。
[assembly: AssemblyFileVersion("4.0.10")]
[assembly: AssemblyInformationalVersion("4.0.10")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.0.10.0")]
