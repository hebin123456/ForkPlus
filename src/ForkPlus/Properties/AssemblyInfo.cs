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
// 版本号说明（2026-09-10，v4.0.9）：拖动残留/滚动/窗口状态专项修复——拖动中断后残留
//   矩形挡界面（四类拖拽控件 OnPointerExited/OnLoaded 兜底清理 + 启动期布局竞态残留
//   清理）、显示更少标签后滚动条卡底/滚轮滚不上去（EnsureScrollToTop 挂钩 extent/
//   offset 变化即刻钳制归零）、滚轮滚动但 thumb 不跟随（ScrollBy 后手动同步 ScrollBar.
//   Value）、点过滚动条后滚轮失效（Tunnel 预览阶段接管）、窗口最大化没保存/下次启动
//   不最大化（状态改读 Avalonia WindowState）、最大化启动矩形挡界面（延迟到下一渲染
//   帧再设 WindowState）、git mm 子仓标签不能拖动（WpfDataObject 进程内直通表传原始
//   对象引用）、活动管理器 git-mm 标签页仅 git mm 仓显示（单仓屏蔽）、下拉菜单隐藏
//   Lean Branching 分组。
//   上一版 v4.0.8 为跨平台迁移期 UI/交互修复批次；v4.0.6 为崩溃/卡顿专项
//   （native 崩溃转储、UI 冻结看门狗、诊断包导出、reverse P/Invoke 防护等）。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示
//   "4.0.9"；程序集标识与文件版本同为 4.0.9（.0）。
[assembly: AssemblyFileVersion("4.0.9")]
[assembly: AssemblyInformationalVersion("4.0.9")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.0.9.0")]
