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
// 版本号说明（2026-09-11，v4.0.11）：二分查找通知条不可用修复 + 好/坏互斥 + 右键菜单快捷键——
//   WPF 迁移丢失 NotificationBar 动画，仓库菜单"二分查找"后标签下方通知条不出现（merge/rebase/
//   cherry-pick/revert/bisect/gitignore 建议等全部通知条不可见，二分查找的 Good/Bad/Skip 只在
//   通知条上）；修复为 IsControlVisible 直接驱动通知条高度 + DoubleTransition(0.7s) 对齐 WPF
//   观感。同提交既标好又标坏会先落库再报错并毒化 bisect 会话，执行 good/bad 前按 git 实时 refs
//   预检相反标记、冲突时直接返回 git 同款错误不执行命令。右键菜单快捷键原仅显示不生效
//   （Avalonia MenuItem.InputGesture 仅作提示文本），WpfCompat 层补齐 WPF 语义（ContextMenu 与
//   宿主窗 Tunnel KeyDown 按手势匹配并点击）。上一版 v4.0.10 为 SSH 密钥删除/滚动/统计/首启专项
//   修复；v4.0.9 为拖动残留/滚动/窗口状态专项修复。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示
//   "4.0.11"；程序集标识与文件版本同为 4.0.11（.0）。
[assembly: AssemblyFileVersion("4.0.11")]
[assembly: AssemblyInformationalVersion("4.0.11")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.0.11.0")]
