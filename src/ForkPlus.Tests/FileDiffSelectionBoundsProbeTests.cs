// 探针（2026-10-20，"FileDiff 选中 2 行，外面的浮窗范围有 4 行"）：
// 实测 DrawSelectionBorder 的数据源 BackgroundGeometryBuilder.GetRectsForSegment
// 对不同形状的 2 行选区返回的矩形（top/height），与 2 行可视行高的期望对比。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Parsing;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class FileDiffSelectionBoundsProbeTests
	{
		private const string Patch = @"diff --git a/sample.txt b/sample.txt
index 1111111..2222222 100644
--- a/sample.txt
+++ b/sample.txt
@@ -1,6 +1,7 @@
 context line 1
 context line 2
-deleted line 1
-deleted line 2
+added line 1
+added line 2
+added line 3
 context line 3
 context line 4
";

		[Fact]
		public void Probe_SelectionRectHeights()
		{
			string report = ProbeBody();
			System.IO.File.WriteAllText("/tmp/probe_selection.txt", report);
		}

		private static string ProbeBody()
		{
			HeadlessAppBootstrap.EnsureStarted();
			return HeadlessAppBootstrap.Run(delegate
			{
				var parse = new PatchParser().Parse(Patch);
				Assert.True(parse.Succeeded, "补丁应解析成功");
				Diff diff = parse.Result.Diffs.FirstOrDefault();
				var window = new Window { Width = 900, Height = 400 };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var editor = new CommitCodeEditor(DiffViewMode.Split);
				editor.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
				editor.IsStaged = false;
				window.Content = editor;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				var textView = editor.TextArea.TextView;

				var sb = new System.Text.StringBuilder();
				double defaultLineHeight = textView.DefaultLineHeight;
				sb.AppendLine("DefaultLineHeight=" + defaultLineHeight);

				// 打印全部可视行：行号/文本/VisualTop/Height
				foreach (AvaloniaEdit.Rendering.VisualLine vl in textView.VisualLines.ToList())
				{
					sb.AppendLine("VL#" + vl.FirstDocumentLine.LineNumber + " [" + vl.FirstDocumentLine.ToString().Trim() + "] VisualTop=" + vl.VisualTop + " H=" + vl.Height + " TextLines=" + vl.TextLines.Count);
				}

				int off1 = editor.Text.IndexOf("added line 1", StringComparison.Ordinal);
				int off2 = editor.Text.IndexOf("added line 2", StringComparison.Ordinal);
				int off3 = editor.Text.IndexOf("added line 3", StringComparison.Ordinal);
				Assert.True(off1 >= 0 && off2 >= 0 && off3 >= 0);

				// 形状 A：行1中段 → 行2中段（鼠标拖拽最常见）
				Probe("A(mid,mid)", editor, textView, off1 + 2, off2 + 2, sb);
				// 形状 B：行1行首 → 行3行首（拖到下一行行首，含换行）
				Probe("B(start,nextStart)", editor, textView, off1, off3, sb);
				// 形状 C：行1行首 → 行2行尾
				int off2Eol = off2 + "added line 2".Length;
				Probe("C(start,eol2)", editor, textView, off1, off2Eol, sb);

				window.Close();
				return sb.ToString();
			});
		}

		private static void Probe(string label, CommitCodeEditor editor, TextView textView, int start, int end, System.Text.StringBuilder sb)
		{
			editor.TextArea.Selection = Selection.Create(editor.TextArea, start, end);
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			ISegment seg = editor.TextArea.Selection.SurroundingSegment;
			sb.AppendLine("---- " + label + " seg=[" + seg.Offset + "," + (seg.Offset + seg.Length) + "] len=" + seg.Length);
			double num = 0.0, num2 = 0.0;
			bool first = true;
			int count = 0;
			foreach (Rect item in BackgroundGeometryBuilder.GetRectsForSegment(textView, seg, extendToFullWidthAtLineEnd: true))
			{
				if (first) { num = item.Top; }
				num2 += item.Height;
				first = false;
				count++;
				sb.AppendLine("  rect#" + count + " top=" + item.Top + " H=" + item.Height + " W=" + item.Width);
			}
			sb.AppendLine("  => num(top)=" + num + " num2(sumH)=" + num2 + " rectCount=" + count);
			editor.TextArea.ClearSelection();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
		}
	}
}
