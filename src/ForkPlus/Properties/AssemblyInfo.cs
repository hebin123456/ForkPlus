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
// 版本号说明（2026-09-30，v4.3.1）：本版为图片对比视图放大查看——
//   1) 并排 / 滑动 / 洋葱皮三种图片对比模式支持在图片区域滚轮缩放、放大超出可视
//      区域后按住拖动平移；四个视图共用同一个 ImageZoomState（缩放 + 归一化中心点），
//      左右两栏与各模式天然同步，两张图尺寸不一致时按中心点对齐。
//   2) 缩放不为 100% 时工具条出现「还原大小 (N%)」按钮，点击回到贴合视图的初始
//      状态（各视图同步还原）；换文件时自动回到初始缩放。
//   3) ScrollViewerWheelFix / TouchpadAwareScrollViewer 按命中元素放行滚轮，
//      落在可缩放图片上时归图片缩放而非滚动页面。
//   详见 RELEASE_NOTE v4.3.1。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "4.3.1"；
//   程序集标识与文件版本同为 4.3.1（.0）。
[assembly: AssemblyFileVersion("4.3.1")]
[assembly: AssemblyInformationalVersion("4.3.1")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.3.1.0")]
