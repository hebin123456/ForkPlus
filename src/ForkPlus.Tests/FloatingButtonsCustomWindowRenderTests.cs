// 决定性诊断（2026-09-04，"代码悬停/选区的暂存/丢弃浮窗丢失"）：
// 现有 DiffSelectionFloatingButtonsTests 用普通 Window + 对象级断言（Adorner 存在、
// Tag 定位、Click 事件）。真实应用是 CustomWindow 模板（LayoutTransformControl +
// VisualLayerManager），且 AdornerLayer 会把窗口 Content 换成 Grid。本测试：
// 1) CustomWindow 下 hover hunk → Adorner 存在；
// 2) AdornerLayer 是窗口内容 Grid 的子级（树结构验证）；
// 3) 像素级：悬浮按钮区域与 hover 前相比有可见差异（浮窗真的画出来了）。
using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class FloatingButtonsCustomWindowRenderTests
	{
		private static Diff MakeDiff()
		{
			var lines = new[]
			{
				"context line 1\n",
				"context line 2\n",
				"context line 3\n",
				"deleted line 1\n",
				"deleted line 2\n",
				"added line 1\n",
				"added line 2\n",
				"added line 3\n",
				"context line 4\n",
				"context line 5\n",
				"context line 6\n"
			};
			var subChunk = new SubChunk(
				new Range(0, 3), new Range(3, 5), new Range(5, 8), new Range(8, 11),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(10, 6, 20, 7, null, new[] { subChunk });
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines, new[] { chunk }, null, Diff.FileType.Text, false);
		}

		private static object GetAdorner(object layer)
		{
			FieldInfo f = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_adorner",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(f);
			return f.GetValue(layer);
		}

		private static int CountChangedPixels(Avalonia.Media.Imaging.WriteableBitmap before, Avalonia.Media.Imaging.WriteableBitmap after, int x0, int y0, int x1, int y1, int tolerance = 30)
		{
			int count = 0;
			using (var lb = before.Lock())
			using (var la = after.Lock())
			{
				int width = Math.Min(before.PixelSize.Width, after.PixelSize.Width);
				int height = Math.Min(before.PixelSize.Height, after.PixelSize.Height);
				for (int row = Math.Max(0, y0); row < Math.Min(height, y1); row++)
				{
					IntPtr rowBefore = lb.Address + row * lb.RowBytes;
					IntPtr rowAfter = la.Address + row * la.RowBytes;
					for (int x = Math.Max(0, x0); x < Math.Min(width, x1); x++)
					{
						byte bb = System.Runtime.InteropServices.Marshal.ReadByte(rowBefore, x * 4);
						byte bg = System.Runtime.InteropServices.Marshal.ReadByte(rowBefore, x * 4 + 1);
						byte br = System.Runtime.InteropServices.Marshal.ReadByte(rowBefore, x * 4 + 2);
						byte ab = System.Runtime.InteropServices.Marshal.ReadByte(rowAfter, x * 4);
						byte ag = System.Runtime.InteropServices.Marshal.ReadByte(rowAfter, x * 4 + 1);
						byte ar = System.Runtime.InteropServices.Marshal.ReadByte(rowAfter, x * 4 + 2);
						if (Math.Abs(br - ar) > tolerance || Math.Abs(bg - ag) > tolerance || Math.Abs(bb - ab) > tolerance)
						{
							count++;
						}
					}
				}
			}
			return count;
		}

		[Fact]
		public void HoverHunk_FloatingButtons_RenderInCustomWindow()
		{
			HeadlessAppBootstrap.EnsureStarted();
			var result = new System.Collections.Generic.Dictionary<string, object>();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var diff = MakeDiff();
				var window = new ForkPlus.UI.CustomWindow { Width = 900, Height = 400 };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var editor = new CommitCodeEditor(DiffViewMode.Split);
				editor.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
				editor.IsStaged = false;
				window.Content = editor;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				FieldInfo lf = typeof(CommitCodeEditor).GetField("_diffSelectionLayer",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(lf);
			var layer = lf.GetValue(editor);

			// hover 前：整帧基线（保留位图供区域差异比较）
			var vl6 = editor.TextArea.TextView.GetVisualLine(6);
			Assert.NotNull(vl6);
			double yHunk = vl6.VisualTop - editor.TextArea.TextView.ScrollOffset.Y + 6;
			var before = HeadlessWindowExtensions.CaptureRenderedFrame(window);

			// hover 到 hunk
			HeadlessWindowExtensions.MouseMove(window, new Point(250, yHunk), global::Avalonia.Input.RawInputModifiers.None);
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			HeadlessWindowExtensions.CaptureRenderedFrame(window);
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			var after = HeadlessWindowExtensions.CaptureRenderedFrame(window);

			var adorner = GetAdorner(layer);
			result["adornerExists"] = adorner != null;

			// AdornerLayer 结构验证：应是窗口内容 Grid 的子级
			var adornerControl = adorner as Control;
			AdornerLayer al = null;
			if (adornerControl != null)
			{
				al = adornerControl.GetVisualAncestors().OfType<AdornerLayer>().FirstOrDefault();
			}
			result["adornerLayerInTree"] = al != null;
			if (al != null)
			{
				result["adornerBounds"] = adornerControl.Bounds.ToString();
				result["adornerIsVisible"] = adornerControl.IsVisible;
				result["adornerDesiredSize"] = adornerControl.DesiredSize.ToString();
				result["adornerLayerBounds"] = al.Bounds.ToString();
				result["adornerLayerChildCount"] = al.Children.Count;

				// 像素级（区域差异，主题无关）：浮窗 bounds 区域在 hover 前后应有大量像素变化
				// —— 修复前该区域只有编辑器背景（按钮 0×0 不渲染），修复后有按钮背景+文字。
				var b = adornerControl.Bounds;
				int x0 = Math.Max(0, (int)b.X);
				int y0 = Math.Max(0, (int)b.Y);
				int x1 = Math.Min(900, (int)(b.X + b.Width));
				int y1 = Math.Min(400, (int)(b.Y + b.Height));
				result["changedPixels"] = CountChangedPixels(before, after, x0, y0, x1, y1);
				result["region"] = x0 + "," + y0 + " - " + x1 + "," + y1;
			}
			result["yHunk"] = yHunk;
			before.Dispose();
			after.Dispose();
			window.Close();
			return 0;
			}).GetAwaiter().GetResult();

			Assert.True((bool)result["adornerExists"], "hover 后应有 Adorner");
			Assert.True((bool)result["adornerLayerInTree"],
				"Adorner 应在 AdornerLayer（窗口内容 Grid）内");
			Assert.True((bool)result["adornerIsVisible"], "Adorner 应可见");
			// 尺寸级：主题生效后按钮应有正常尺寸（修复前 40×4 —— AddLogicalChild 空实现导致
			// FloatingButton 无 ControlTheme、测量 0×0，只剩 margin）
			var desired = result["adornerDesiredSize"].ToString();
			Assert.True(desired.Contains(",") &&
				double.Parse(desired.Split(',')[0]) >= 60.0 &&
				double.Parse(desired.Split(',')[1]) >= 15.0,
				"悬浮按钮容器应有正常尺寸（宽≥60、高≥15），实际 desired=" + desired);
			// 像素级：浮窗区域 hover 前后应有大量像素变化（按钮真的画出来了）
			int changed = (int)result["changedPixels"];
			Assert.True(changed > 100,
				"悬浮按钮区域应渲染出可见变化（changedPixels=" + changed
				+ ", bounds=" + result["adornerBounds"] + ", region=" + result["region"] + "）");
		}
	}
}
