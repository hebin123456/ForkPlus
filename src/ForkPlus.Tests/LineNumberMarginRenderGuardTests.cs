// 回归测试（2026-09-12，v4.0.12 两处界面崩溃/报错修复）：
//
// Bug 1（AppDomain 致命崩溃，crash-20260912-103134-182 / -103202-083 双转储实证）：
//   CodeEditorLineNumberMargin.Render 在视觉行失效期（Redraw()/文档变更后、下一轮
//   Measure 重建前）直接 foreach TextView.VisualLines——AvaloniaEdit 的 getter 在
//   _visibleVisualLines == null 时抛 VisualLinesInvalidException；而 Avalonia 渲染管线
//   存在同步提交路径（WndProc → HandlePaint → ImmediateRenderRequested →
//   Compositor.Commit → CompositingRenderer.UpdateCore → margin.Render），异常沿
//   调用链上抛到消息循环即进程终止（IsTerminating=True）。
//   本用例确定性复现该时序：布局建立视觉行 → TextView.Redraw() 同步置空视觉行
//   （ClearVisualLines()，AvaloniaEdit TextView 源码）→ 不跑布局直接
//   RenderTargetBitmap.Render(margin)（ImmediateRenderer 不做 Measure，失效状态
//   得以保持）→ 修复前此处抛 VisualLinesInvalidException，修复后正常渲染；
//   随后布局重建再渲染一次，守卫"防护不得误伤正常路径"。
//
// Bug 2（"Cannot initialize TextEditorContextMenu style: Object reference not set to
//   an instance of an object"，每次启动/切主题必现，crash-20260912 日志 10:32:20 实证）：
//   WPF 原版 hack 反射查找 PresentationFramework 内部类型
//   System.Windows.Documents.TextEditorContextMenu+EditorContextMenu；迁移 Avalonia 后
//   typeof(TextElement).Assembly 是 Avalonia.Controls，不存在该类型，GetType() 返回
//   null，原代码直接对 null 调 GetNestedType → NullReferenceException 被 catch 打
//   Error 日志，样式注册从未生效。本用例挂临时 NLog MemoryTarget 捕获 ForkPlus.Log
//   的 Error 级，反射调用两次（覆盖幂等路径——每次切主题都会走到这里，WPF 原版
//   第二次 Add 重复键同样抛异常刷日志），断言不再出现该错误日志。
using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using Xunit;

namespace ForkPlus.Tests
{
        [Collection("HeadlessAvalonia")]
        public class LineNumberMarginRenderGuardTests
        {
                // ===== Bug 1：视觉行失效期渲染行号边距不得抛 VisualLinesInvalidException =====

                [Fact]
                public void CodeEditorLineNumberMargin_Render_WhileVisualLinesInvalid_DoesNotThrow()
                {
                        HeadlessAppBootstrap.Run(delegate
                        {
                                // 生产同款装配：TextContentControl 构造时把 CodeEditorLineNumberMargin
                                // 挂进 TextArea.LeftMargins（TextContentControl : CodeEditor）
                                var editor = new TextContentControl();
                                editor.Text = "line1\nline2\nline3\nline4\nline5";
                                var window = new Window { Width = 400, Height = 200, Content = editor };
                                window.Show();
                                Dispatcher.UIThread.RunJobs();

                                // 布局完成：视觉行已构建（TextView.MeasureOverride → EnsureVisualLines）
                                var textView = editor.TextArea.TextView;
                                Assert.True(textView.VisualLinesValid, "布局后视觉行应有效");

                                // 同步失效视觉行，复现崩溃时序（文档变更/Redraw 后、Measure 重建前）
                                textView.Redraw();
                                Assert.False(textView.VisualLinesValid, "Redraw() 后视觉行应失效");

                                var margin = editor.TextArea.LeftMargins.OfType<CodeEditorLineNumberMargin>().Single();
                                // 修复前：此处抛 VisualLinesInvalidException（与 crash 转储同源异常）；
                                // 修复后：视觉行无效期跳过行号绘制，背景/分隔线照常
                                using (var rtb = new RenderTargetBitmap(new PixelSize(60, 120)))
                                {
                                        rtb.Render(margin);
                                }

                                // 防护不得误伤正常路径：布局重建后视觉行恢复有效、行号恢复渲染
                                Dispatcher.UIThread.RunJobs();
                                Assert.True(textView.VisualLinesValid, "布局重建后视觉行应恢复有效");
                                using (var rtb2 = new RenderTargetBitmap(new PixelSize(60, 120)))
                                {
                                        rtb2.Render(margin);
                                }
                                window.Close();
                        });
                }

                // ===== Bug 2：TextEditorContextMenu 样式初始化不再空引用刷错误日志 =====

                [Fact]
                public void InitializeTextEditorContextMenuStyle_OnAvalonia_NoErrorLogged()
                {
                        HeadlessAppBootstrap.Run(delegate
                        {
                                // 前提自检：Avalonia.Controls 程序集确实没有 WPF 内部类型
                                // TextEditorContextMenu——这是本修复"类型不存在时静默跳过"策略的环境基础
                                Type wpfType = typeof(Avalonia.Controls.Documents.TextElement).Assembly
                                        .GetType("System.Windows.Documents.TextEditorContextMenu");
                                Assert.Null(wpfType);

                                // 挂临时 MemoryTarget 只捕 ForkPlus.Log 的 Error 级（App.axaml.cs 的
                                // Log.Error 落在该静态类的 GetCurrentClassLogger 上）；不动生产配置
                                // 对象，整体换探针配置、finally 恢复
                                var original = NLog.LogManager.Configuration;
                                var memoryTarget = new NLog.Targets.MemoryTarget("ForkPlusTests_TecmsProbe")
                                {
                                        Layout = "${message}"
                                };
                                var probeConfig = new NLog.Config.LoggingConfiguration();
                                probeConfig.AddTarget(memoryTarget);
                                probeConfig.AddRule(NLog.LogLevel.Error, NLog.LogLevel.Fatal, memoryTarget, "ForkPlus.Log");
                                NLog.LogManager.Configuration = probeConfig;
                                try
                                {
                                        var app = (global::ForkPlus.App)Avalonia.Application.Current;
                                        Assert.NotNull(app);
                                        MethodInfo method = typeof(global::ForkPlus.App).GetMethod("InitializeTextEditorContextMenuStyle",
                                                BindingFlags.NonPublic | BindingFlags.Instance);
                                        Assert.NotNull(method);

                                        // 两次调用覆盖幂等路径（每次切主题都会走到）
                                        method.Invoke(app, null);
                                        method.Invoke(app, null);

                                        string[] errors = memoryTarget.Logs.ToArray();
                                        Assert.False(errors.Any(l => l != null && l.Contains("Cannot initialize TextEditorContextMenu style")),
                                                "样式初始化不应再打错误日志，实际捕获：" + string.Join(" | ", errors));
                                }
                                finally
                                {
                                        NLog.LogManager.Configuration = original;
                                }
                        });
                }
        }
}
