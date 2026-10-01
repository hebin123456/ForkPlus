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
// 版本号说明（2026-10-01，v4.3.2）：本版为图片对比视图里的动图（GIF / 动态 WebP /
//   APNG）直接播放——
//   1) 用 SkiaSharp 的 SKCodec 统一解码动图帧序列（Avalonia 自身 Bitmap 只解首帧），
//      并排视图按当前帧渲染；帧数 / 总像素超阈值时退回静态首帧，避免内存爆掉。
//   2) 各列底部居中悬浮播放控制条：播放/暂停、上一帧/下一帧、播放速度
//      （0.5×/1×/2×）与帧序号；切视图/换文件自动暂停并释放帧内存。
//   3) 动图关闭像素差异高亮（逐帧差异无意义）；控制条文案随 8 种界面语言发布。
//   详见 RELEASE_NOTE v4.3.2。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "4.3.2"；
//   程序集标识与文件版本同为 4.3.2（.0）。
[assembly: AssemblyFileVersion("4.3.2")]
[assembly: AssemblyInformationalVersion("4.3.2")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.3.2.0")]
