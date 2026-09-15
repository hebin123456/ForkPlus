// 回归测试（2026-09-04，"FileDiff 高度计算多了，滚动条可拉到很下面有一大块空白"修复产物）：
// 根因：AvaloniaEdit 12.x 的 TextEditorOptions.AllowScrollBelowDocument 默认 true（WPF
// AvalonEdit 默认 false）。TextView.MeasureOverride 在该选项开启时给滚动 extent 加
// "viewport 高 - 一行"的额外空间（允许滚到文档底部之下），探针实测 Extent=文档高+
// viewport——diff/代码/十六进制编辑器都能拉到很下面，底部一大块空白。WPF 原版 Fork
// 未显式设置该选项（用 WPF 默认 false），无此现象。修复：CodeEditor/HexEditor 构造
// 函数显式置 false 对齐 WPF。
// 本测试守卫：50 行文档的 PART_ScrollViewer 垂直最大偏移 == 文档高 - viewport
//（拉到底恰好是文档末行，无底部空白）。
// 2026-09-15 修正（v4.1.3 行密度收紧后本测试在浮点边界翻红）：期望值原硬编码 50 行，
// 但 50 次 AppendLine 的文档实际渲染 51 行（末尾 \n 后的幽灵空行，WPF AvalonEdit
// 同样渲染）——extent = 51 × DefaultLineHeight，与期望差恰好一行。此前该差值仅靠
// 浮点舍入误差（17.55406249999987 < 17.554062499999997）"惊险"通过；LineHeightFactor
// 收到 1.0 后 15.1328125 可被二进制精确表示，差值 == 行高，严格小于判定失败。改为
// 按真实行数（Document.LineCount）计算文档高，断言与 extent 的关系不受行高数值影响。
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
				// 文档实际渲染行数含末尾 \n 的幽灵空行（AppendLine×50 → LineCount=51），
				// WPF AvalonEdit 同样渲染该行——extent 必须按真实行数计算
				int renderedLineCount = editor.Document.LineCount;
				double docHeight = renderedLineCount * lineHeight;
				double viewportH = scroll.Viewport.Height;
				double expectedMax = docHeight - viewportH; // 拉到底 = 文档末行贴底
				// 容差取 0.5px（量化误差级）：AllowScrollBelowDocument 开启时 extent 会多
				// "viewport - 一行"（本例 ≈285px），远超容差，仍能捕获回归
				Assert.True(Math.Abs(scroll.ScrollBarMaximum.Y - expectedMax) < 0.5,
					$"垂直最大偏移 {scroll.ScrollBarMaximum.Y:F1} 应 = 文档高-viewport={expectedMax:F1}（{renderedLineCount} 行 × {lineHeight:F2}、viewport {viewportH:F1}）——" +
					$"超出即 AllowScrollBelowDocument 开启（AvaloniaEdit 12.x 默认 true，WPF 默认 false），底部出现可滚动空白");

				// 守卫 2：选项确实关闭（构造函数修复的直接产物）
				Assert.False(editor.Options.AllowScrollBelowDocument,
					"CodeEditor 必须 AllowScrollBelowDocument=false（对齐 WPF AvalonEdit 默认）");

				window.Close();
			});
		}
	}
}
