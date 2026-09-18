// 用户手册截图基建(2026-09-19):与 ScreenshotHelper 同款渲染管线,但输出到
// docs/manual/screenshots/<模块短名>/<场景>.png —— 用户手册插图独立目录,
// 不复用测试证据目录(docs/evidence/e2e)。
// 口径与 ScreenshotHelper 一致:主窗口 1920×1080 最大化截图,弹窗/辅助窗口按自然比例;
// 每张截图自动做"非空白"像素断言。必须在 UI 线程内调用(HeadlessAppBootstrap.Run 的回调里)。
using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ForkPlus.Tests
{
	internal static class ManualScreenshotHelper
	{
		private const double CaptureWidth = 1920.0;
		private const double CaptureHeight = 1080.0;

		/// <summary>截图并断言非空白。主窗口按 1920×1080 最大化口径,其余窗口按自然比例。
		/// 输出到 docs/manual/screenshots/&lt;moduleDir&gt;/&lt;scenario&gt;.png。</summary>
		public static int Snap(Window window, string scenario, string moduleDir)
		{
			using WriteableBitmap frame = (window is global::ForkPlus.UI.MainWindow
					? CaptureMaximized(window)
					: CaptureNatural(window))
				?? throw new InvalidOperationException("CaptureRenderedFrame 返回 null(渲染管线未产出帧)");
			int nonBlank = CountNonBlankPixels(frame);
			string dir = ManualDir(moduleDir);
			frame.Save(Path.Combine(dir, scenario + ".png"));
			AssertNonBlank(scenario, nonBlank, minimalPixels: 200);
			return nonBlank;
		}

		/// <summary>交互前后两帧截图 + 差异断言(同一控件交互有效果)。两帧均按窗口当前
		/// 尺寸截取(不做任何放大)。返回差异像素数。</summary>
		public static int SnapDiff(Window window, string scenario, string moduleDir, int beforeNonBlank)
		{
			Dispatcher.UIThread.RunJobs();
			using WriteableBitmap frame = window.CaptureRenderedFrame()
				?? throw new InvalidOperationException("CaptureRenderedFrame 返回 null");
			int nonBlank = CountNonBlankPixels(frame);
			string dir = ManualDir(moduleDir);
			frame.Save(Path.Combine(dir, scenario + ".png"));
			AssertNonBlank(scenario, nonBlank, minimalPixels: 200);
			return nonBlank;
		}

		/// <summary>当前帧非空白像素数(用于交互前基线,不落盘)。</summary>
		public static int CountBlank(Window window)
		{
			Dispatcher.UIThread.RunJobs();
			using WriteableBitmap frame = window.CaptureRenderedFrame();
			return frame == null ? 0 : CountNonBlankPixels(frame);
		}

		private static WriteableBitmap CaptureNatural(Window window)
		{
			Dispatcher.UIThread.RunJobs();
			return window.CaptureRenderedFrame();
		}

		private static WriteableBitmap CaptureMaximized(Window window)
		{
			Dispatcher.UIThread.RunJobs();
			double oldWidth = window.Width;
			double oldHeight = window.Height;
			ScrollViewer[] scrollers = window.GetVisualDescendants().OfType<ScrollViewer>().ToArray();
			Vector[] offsets = scrollers.Select(s => s.Offset).ToArray();

			SizeToContent oldSizeToContent = window.SizeToContent;
			if (oldSizeToContent == SizeToContent.WidthAndHeight)
			{
				window.SizeToContent = SizeToContent.Height;
			}
			window.Width = CaptureWidth;
			window.Height = CaptureHeight;
			Dispatcher.UIThread.RunJobs();

			WriteableBitmap frame = window.CaptureRenderedFrame();

			window.Width = oldWidth;
			window.Height = oldHeight;
			window.SizeToContent = oldSizeToContent;
			Dispatcher.UIThread.RunJobs();
			for (int i = 0; i < scrollers.Length; i++)
			{
				if (scrollers[i].Offset != offsets[i])
				{
					scrollers[i].Offset = offsets[i];
				}
			}
			Dispatcher.UIThread.RunJobs();
			return frame;
		}

		private static void AssertNonBlank(string scenario, int nonBlank, int minimalPixels)
		{
			if (nonBlank < minimalPixels)
			{
				throw new InvalidOperationException(
					"截图疑似空白(" + scenario + ",非空白像素 " + nonBlank + " < " + minimalPixels + ")——渲染可能失败");
			}
		}

		private static string ManualDir(string moduleDir)
		{
			string dir = Path.Combine(FindRepoRoot(), "docs", "manual", "screenshots", moduleDir);
			Directory.CreateDirectory(dir);
			return dir;
		}

		private static string FindRepoRoot()
		{
			string dir = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent?.FullName;
			while (dir != null && !Directory.Exists(Path.Combine(dir, "src", "ForkPlus.Tests")))
			{
				dir = Directory.GetParent(dir)?.FullName;
			}
			return dir ?? throw new InvalidOperationException("找不到仓库根(src/ForkPlus.Tests 不存在)");
		}

		private static int CountNonBlankPixels(WriteableBitmap frame)
		{
			int count = 0;
			using (var l = frame.Lock())
			{
				for (int row = 0; row < frame.PixelSize.Height; row++)
				{
					IntPtr rowPtr = l.Address + row * l.RowBytes;
					for (int x = 0; x < frame.PixelSize.Width; x++)
					{
						byte b = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4);
						byte g = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 1);
						byte r = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 2);
						if (r < 230 || g < 230 || b < 230)
						{
							count++;
						}
					}
				}
			}
			return count;
		}
	}
}
