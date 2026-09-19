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
// 版本号说明（2026-09-17，v4.1.4）：FileDiff 行间距收紧 + 行密度对齐 WPF 3.13.2 +
//   FileDiff 大区域从下往上拖选（准备暂存）界面失控弹跳/首次拖选冻结——
//   v4.1.2 为修左右行对齐（CJK 行高差）装的行高同步器把含中文文件的全部行槽抬到
//   CJK 自然行高（根因：全局回退 Noto Sans CJK SC 垂直度量 1.448em，Win DWrite
//   实测 1.619em，CJK 行自然高超默认行槽），行间距过宽。行间距两步修复：
//   1) 内嵌 CJK 子集字体度量收紧到 1.16em（hhea/OS/2 win/typo 三处一致），
//      CJK 自然高 15.08px@13px；
//   2) CodeEditor/HexEditor 置 LineHeightFactor=1.0——AvaloniaEdit 默认 1.16 把
//      行槽放大 16%（同视口 43 行 → 36 行的差距来源），置 1.0 后行槽 = 自然行高，
//      行密度与 WPF 3.13.2 一致；CJK 15.08px ≤ 新行槽（ASCII 自然高 ≈ 15.22px@Win/
//      15.13px@Linux），含中文行不再超槽。左右行对齐与行号基线对齐保持不变（守卫
//      测试 SideBySideRegressionTests 四条防线）。拖选修复（CodeEditor 层）：
//   SelectionMouseHandler 置 e.Handled=true 导致抑制处理器从未触发 → 拖选期间
//   caret 跟随的"居中跳变"逐事件上弹到文档顶部；AdornerLayer 首建重建窗口内容树
//   时指针捕获被挪到祖先元素 → 首次拖选冻结。详见 RELEASE_NOTE v4.1.4。
//   上一个正式版 v4.1.2 为凭据链路系列修复 + FileDiff SideBySide 严格对齐系列。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "4.1.4"；
//   程序集标识与文件版本同为 4.1.4（.0）。
[assembly: AssemblyFileVersion("4.1.6")]
[assembly: AssemblyInformationalVersion("4.1.6")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.1.6.0")]
