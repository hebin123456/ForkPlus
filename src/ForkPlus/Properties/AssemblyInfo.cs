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
// 版本号说明（2026-09-30，v4.2.1）：本版为仓库健康检查 + 提交预览/Reflog 系列体验修复——
//   1) 仓库健康检查窗口（菜单位于基准测试之上）：定宽 900、内容分区 100% 宽铺满
//      （根因：Fluent Expander 默认 ControlTheme 设 HorizontalAlignment=Left 且 MinHeight=48，
//      分区收缩成"又大又窄"，5 个 Expander 显式 Stretch + 分区 Grid 化后铺满）、
//      删除分支二次确认 + 受保护分支确认（RepositorySettings.ProtectedBranches 模式匹配）+
//      删除后陈旧/已合并/分叉三区联动刷新（原先只清当前分区，重叠分支在其他分区残留）。
//   2) 提交预览区 v3（Pull 拉取 / Push 强推覆盖 / Reset 丢失提交三处共用）：折叠头与摘要行
//      同左缘（原 DiffListExpanderStyle 头模板固定缩进 43px 显靠右）、sha 与 subject 同字体
//      对齐（原 Consolas/UI 字体基线差致歪斜）、sha 改超链接点击打开 RevisionDetailsWindow
//      变更详情面板（完整 SHA 由 GetCommitsBetweenGitCommand 以 %H 随行携带）。
//   3) Reflog 窗口：表头随列表行几何校准对齐；"跳转到此提交"确认弹窗显式 SetOwnerCompat
//      归属宿主（原 ForkPlusDialogWindow 构造默认登记 MainWindow，关闭时主窗口压到 Reflog 上面）。
//   4) 丢弃更改专用确认窗口（DiscardChangesWindow，WS2.3）、重开已关闭标签页
//      （ReopenClosedTabCommand，Ctrl+Shift+T；原 New Tag 快捷键改 Ctrl+Shift+G）。
//   详见 RELEASE_NOTE v4.2.1。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "4.2.1"；
//   程序集标识与文件版本同为 4.2.1（.0）。
[assembly: AssemblyFileVersion("4.2.1")]
[assembly: AssemblyInformationalVersion("4.2.1")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.2.1.0")]
