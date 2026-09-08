// 回归测试（2026-09-04，"FileDiff 高度计算多了，滚动条可拉到很下面有一大块空白"修复产物）：
// 根因：AvaloniaEdit 12.x 的 TextEditorOptions.AllowScrollBelowDocument 默认 true（WPF
// AvalonEdit 默认 false）。TextView.MeasureOverride 在该选项开启时给滚动 extent 加
// "viewport 高 - 一行"的额外空间（允许滚到文档底部之下），探针实测 Extent=文档高+
// viewport——diff/代码/十六进制编辑器都能拉到很下面，底部一大块空白。WPF 原版 Fork
// 未显式设置该选项（用 WPF 默认 false），无此现象。修复：CodeEditor/HexEditor 构造
// 函数显式置 false 对齐 WPF。
// 本测试守卫：50 行文档的 PART_ScrollViewer 垂直最大偏移 == 文档高 - viewport
//（拉到底恰好是文档末行，无底部空白）。
using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiffEditorScrollExtentProbeTests
	{
		[Fact]
		public void TextEditor_ScrollMax_StopsAtDocumentBottom()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var editor = new DiffCodeEditor();
				var sb = new StringBuilder();
				for (int i = 1; i <= 50; i++)
				{
					sb.AppendLine($"line {i}");
				}
				editor.Text = sb.ToString();

				var host = new Border { Width = 600, Height = 300, Child = editor };
				var window = new Window { Width = 700, Height = 400, Content = host };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var scroll = editor.GetVisualDescendants().OfType<ScrollViewer>()
					.First((ScrollViewer s) => s.Name == "PART_ScrollViewer");
				double lineHeight = editor.TextArea.TextView.DefaultLineHeight;
				double docHeight = 50 * lineHeight;
				double viewportH = scroll.Viewport.Height;
				double expectedMax = docHeight - viewportH; // 拉到底 = 文档末行贴底

				Assert.True(Math.Abs(scroll.ScrollBarMaximum.Y - expectedMax) < lineHeight,
					$"垂直最大偏移 {scroll.ScrollBarMaximum.Y:F1} 应 ≈ 文档高-viewport={expectedMax:F1}（文档高 {docHeight:F1}、viewport {viewportH:F1}）——" +
					$"超出即 AllowScrollBelowDocument 开启（AvaloniaEdit 12.x 默认 true，WPF 默认 false），底部出现可滚动空白");

				// 守卫 2：选项确实关闭（构造函数修复的直接产物）
				Assert.False(editor.Options.AllowScrollBelowDocument,
					"CodeEditor 必须 AllowScrollBelowDocument=false（对齐 WPF AvalonEdit 默认）");

				window.Close();
			});
		}
	}
}
