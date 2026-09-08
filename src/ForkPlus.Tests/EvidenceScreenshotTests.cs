// 证据截图（2026-09-04，"修复9截图上库，修复8修复后也要证据上库"）：
// 用无头 Skia 渲染管线把修复后的真实 UI 截帧存 PNG 到仓库 docs/evidence/，
// 随代码一起提交，供人工核验"修复确实可见、非自欺"。
//   fix9-before-hover.png / fix9-after-hover.png —— diff 悬浮 Stage/Discard 浮窗（修复9）
//   fix8-hexdiff.png —— 二进制文件 Hex Diff 视图（修复8）
//   fix10-before/after-{monokai,yellowdark,dark,light}.png —— 重命名内联编辑框选区颜色（修复10）
//   fix14-after-textbox-padding.png / fix14-after-rename-editor-padding.png —— TextBox 文字
//     不再贴左/上边框（修复14：主题默认 Padding=2,1 经 Margin 下传 TextPresenter）
// 截图同时做像素断言（非全空白、hover 前后有可见差异），测试本身就是回归证明。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class EvidenceScreenshotTests
	{
		// 仓库根：从测试输出目录向上找含 src/ForkPlus.Tests 的目录
		private static string FindRepoRoot()
		{
			string dir = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent?.FullName;
			while (dir != null && !Directory.Exists(Path.Combine(dir, "src", "ForkPlus.Tests")))
			{
				dir = Directory.GetParent(dir)?.FullName;
			}
			return dir ?? throw new InvalidOperationException("找不到仓库根（src/ForkPlus.Tests 不存在）");
		}

		private static string EvidenceDir()
		{
			string dir = Path.Combine(FindRepoRoot(), "docs", "evidence");
			Directory.CreateDirectory(dir);
			return dir;
		}

		private static void SaveFrame(Avalonia.Media.Imaging.WriteableBitmap frame, string fileName)
		{
			string path = Path.Combine(EvidenceDir(), fileName);
			frame.Save(path);
		}

		private static int CountNonBlankPixels(Avalonia.Media.Imaging.WriteableBitmap frame)
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

		// ============================ 修复9：悬浮 Stage/Discard 浮窗 ============================

		private static Diff MakeFix9Diff()
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

		[Fact]
		public void Fix9_FloatingButtons_ScreenshotEvidence()
		{
			HeadlessAppBootstrap.EnsureStarted();
			int[] changedInAdornerRegion = new int[1];
			string[] regionDesc = new string[1];
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var diff = MakeFix9Diff();
				// 真实窗口模板：CustomWindow 默认主题含 LayoutTransformControl + VisualLayerManager
				var window = new ForkPlus.UI.CustomWindow { Width = 900, Height = 400 };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var editor = new CommitCodeEditor(DiffViewMode.Split);
				editor.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
				editor.IsStaged = false;
				window.Content = editor;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				var vl6 = editor.TextArea.TextView.GetVisualLine(6);
				Assert.NotNull(vl6);
				double yHunk = vl6.VisualTop - editor.TextArea.TextView.ScrollOffset.Y + 6;

				// hover 前：基线截图（保留位图供区域差异比较）
				var before = HeadlessWindowExtensions.CaptureRenderedFrame(window);

				// hover 到 hunk → 浮窗出现
				HeadlessWindowExtensions.MouseMove(window, new Point(250, yHunk), global::Avalonia.Input.RawInputModifiers.None);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				HeadlessWindowExtensions.CaptureRenderedFrame(window);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				var after = HeadlessWindowExtensions.CaptureRenderedFrame(window);

				// 找到浮窗 Adorner，取其 bounds 区域做前后差异统计（hover 高亮会改变行背景，
				// 全局非空白像素数不是可靠指标；浮窗区域差异才是"浮窗画出来了"的直接证据）
				FieldInfo lf = typeof(CommitCodeEditor).GetField("_diffSelectionLayer",
					BindingFlags.NonPublic | BindingFlags.Instance);
				Assert.NotNull(lf);
				var layer = lf.GetValue(editor);
				FieldInfo af = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_adorner",
					BindingFlags.NonPublic | BindingFlags.Instance);
				Assert.NotNull(af);
				var adorner = af.GetValue(layer) as Control;
				Assert.NotNull(adorner);
				var b = adorner.Bounds;
				int x0 = Math.Max(0, (int)b.X);
				int y0 = Math.Max(0, (int)b.Y);
				int x1 = Math.Min(900, (int)(b.X + b.Width));
				int y1 = Math.Min(400, (int)(b.Y + b.Height));
				regionDesc[0] = x0 + "," + y0 + " - " + x1 + "," + y1;

				int changed = 0;
				using (var lb = before.Lock())
				using (var la = after.Lock())
				{
					for (int row = y0; row < y1; row++)
					{
						IntPtr rowBefore = lb.Address + row * lb.RowBytes;
						IntPtr rowAfter = la.Address + row * la.RowBytes;
						for (int x = x0; x < x1; x++)
						{
							byte bb = System.Runtime.InteropServices.Marshal.ReadByte(rowBefore, x * 4);
							byte bg = System.Runtime.InteropServices.Marshal.ReadByte(rowBefore, x * 4 + 1);
							byte br = System.Runtime.InteropServices.Marshal.ReadByte(rowBefore, x * 4 + 2);
							byte ab = System.Runtime.InteropServices.Marshal.ReadByte(rowAfter, x * 4);
							byte ag = System.Runtime.InteropServices.Marshal.ReadByte(rowAfter, x * 4 + 1);
							byte ar = System.Runtime.InteropServices.Marshal.ReadByte(rowAfter, x * 4 + 2);
							if (Math.Abs(br - ar) > 30 || Math.Abs(bg - ag) > 30 || Math.Abs(bb - ab) > 30)
							{
								changed++;
							}
						}
					}
				}
				changedInAdornerRegion[0] = changed;

				SaveFrame(before, "fix9-before-hover.png");
				SaveFrame(after, "fix9-after-hover.png");
				before.Dispose();
				after.Dispose();
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();

			string beforePath = Path.Combine(EvidenceDir(), "fix9-before-hover.png");
			string afterPath = Path.Combine(EvidenceDir(), "fix9-after-hover.png");
			Assert.True(File.Exists(beforePath) && new FileInfo(beforePath).Length > 1000, "fix9-before-hover.png 应生成");
			Assert.True(File.Exists(afterPath) && new FileInfo(afterPath).Length > 1000, "fix9-after-hover.png 应生成");
			// 浮窗 bounds 区域 hover 前后应有大量像素变化（浮窗真的画出来了）
			Assert.True(changedInAdornerRegion[0] > 100,
				"浮窗区域应渲染出可见变化（changedPixels=" + changedInAdornerRegion[0]
				+ ", region=" + regionDesc[0] + "）");
		}

		// ============================ 修复10：重命名内联编辑框选区颜色 ============================
		// 症状："重命名仓库，仓库名那个位置被选中的颜色不对"。根因：TextBox/PlaceholderTextBox/
		// AutoCompleteTextBox/ComboBoxEditableTextBox 四个 ControlTheme 的选区绑皮肤 AccentBrush +
		// 硬编码白前景——Monokai(#A6E22E)/YellowDark(#FACC15)/CyanDark(#22D3EE) 等浅色 accent 皮肤上
		// 白字几乎不可读，Dark 的 #3E9FF8 也偏"洗白"。修复：全皮肤固定选区蓝 #236BD2（与列表/树
		// 选中项 Item.SelectedActive.Background 一致）+ 白字（5.1:1，WCAG AA）。
		// 证据：before 截图（fix10-before-*.png，修复前 accent 选区）由诊断探针在修复前截取入库；
		// 本测试在修复后渲染同一场景出 after 截图，并像素断言选区蓝真实画出（非自欺）。

		// 与 RepositoryManagerUserControl.axaml 仓库行模板同构的最小场景：EditableTextBlock 进编辑态
		private static (Window window, ForkPlus.UI.Controls.RepositoryManagerEditableTextBlock etb) BuildFix10Scenario()
		{
			var etb = new ForkPlus.UI.Controls.RepositoryManagerEditableTextBlock
			{
				FontSize = 14,
				Height = 22,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
				Value = "my-repository-name",
				Width = 220
			};
			var row = new Grid { Height = 22, Background = new SolidColorBrush(Color.Parse("#2C2C2D")) };
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			Grid.SetColumn(etb, 1);
			row.Children.Add(etb);
			var window = new Window { Width = 420, Height = 120, Content = row };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			return (window, etb);
		}

		// 换肤（与 App.InitializeTheme 同机制：加新皮肤 include 再移除旧的）
		private static void SwitchSkin(string skin)
		{
			var app = global::Avalonia.Application.Current!;
			var oldInclude = app.Resources.MergedDictionaries
				.OfType<global::Avalonia.Markup.Xaml.Styling.ResourceInclude>()
				.FirstOrDefault(i => i.Source?.OriginalString.Contains("Theme/Generic.") == true);
			var newInclude = new global::Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://ForkPlus/App.axaml"))
			{
				Source = new Uri("avares://ForkPlus/Theme/Generic." + skin + ".axaml")
			};
			app.Resources.MergedDictionaries.Add(newInclude);
			if (oldInclude != null)
			{
				app.Resources.MergedDictionaries.Remove(oldInclude);
			}
			Dispatcher.UIThread.RunJobs();
		}

		[Fact]
		public void Fix10_RenameSelection_ScreenshotEvidence()
		{
			HeadlessAppBootstrap.EnsureStarted();
			int[] selectionPixelsMonokai = new int[1];
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				// 皮肤文件名 → 证据文件后缀（与 fix10-before-*.png 一一对应）
				(string skin, string file)[] cases =
				{
					("Monokai", "fix10-after-monokai.png"),
					("YellowDark", "fix10-after-yellowdark.png"),
					("Dark", "fix10-after-dark.png"),
					("Light", "fix10-after-light.png")
				};
				Color selectionBlue = Color.FromRgb(0x23, 0x6B, 0xD2);
				foreach (var (skin, file) in cases)
				{
					SwitchSkin(skin);
					var (window, etb) = BuildFix10Scenario();
					etb.IsInEditMode = true;
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					using var frame = HeadlessWindowExtensions.CaptureRenderedFrame(window);
					Assert.NotNull(frame);
					SaveFrame(frame, file);
					if (skin == "Monokai")
					{
						// 像素断言：最糟皮肤（荧光绿 accent）上选区蓝必须真实渲染（数百像素量级）。
						// CaptureRenderedFrame 的 Lock() 帧缓冲实测为 RGBA 字节序（byte0=R）——
						// 与 RenderTargetBitmap.CopyPixels 的 BGRA 不同，精确匹配必须按对序读。
						using var l = frame.Lock();
						int count = 0;
						for (int y = 0; y < l.Size.Height; y++)
						{
							IntPtr rowPtr = l.Address + y * l.RowBytes;
							for (int x = 0; x < l.Size.Width; x++)
							{
								byte r = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4);
								byte g = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 1);
								byte b = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 2);
								if (r == selectionBlue.R && g == selectionBlue.G && b == selectionBlue.B)
								{
									count++;
								}
							}
						}
						selectionPixelsMonokai[0] = count;
					}
					window.Close();
				}
				// 还原默认 Light 皮肤，避免污染同进程后续 headless 测试的主题假设
				SwitchSkin("Light");
				return 0;
			}).GetAwaiter().GetResult();

			foreach (string file in new[] { "fix10-after-monokai.png", "fix10-after-yellowdark.png", "fix10-after-dark.png", "fix10-after-light.png" })
			{
				string path = Path.Combine(EvidenceDir(), file);
				Assert.True(File.Exists(path) && new FileInfo(path).Length > 1000, file + " 应生成");
			}
			Assert.True(selectionPixelsMonokai[0] > 100,
			"Monokai 皮肤上选区蓝未渲染（像素=" + selectionPixelsMonokai[0] + "）——修复不可见");
	}

	// ============================ 修复14：TextBox 文字贴左/上边框 ============================

	// 根因（2026-09-04，"TextBox 里面的字和左/上边框贴得太紧"）：原版 WPF 的 TextBox 模板经
	// PART_ContentHost（ScrollViewer）自动应用 Padding；迁移模板把 TextPresenter 直接放
	// Border 里且未下传 Padding——所有显式 Padding="4,2,4,2" 的使用处（100+ 处）对真实文字
	// 全部无效（只作用于 placeholder），未显式设置处文字 0px 贴边框。修复：主题默认
	// Padding="2,1" + Margin="{TemplateBinding Padding}" 下传 TextPresenter（TextBox/
	// PlaceholderTextBox/AutoCompleteTextBox/FilterTextBox/ComboBox 编辑模板全覆盖），
	// 重命名编辑框（EditableTextBlock）不再用局部值 0 覆盖主题默认。
	// 证据：本测试渲染修复后的普通 TextBox 与重命名编辑框，像素断言 1px 边框内侧的
	// 左 2px / 上 1px 内边距条带无文字墨色（修复前文字 0px 贴边），文字墨色真实存在于内边距之外。

	[Fact]
	public void Fix14_TextBoxPadding_ScreenshotEvidence()
	{
		HeadlessAppBootstrap.EnsureStarted();
		(int leftGapInk, int topGapInk, int textInk) = (0, 0, 0);
		Dispatcher.UIThread.InvokeAsync(delegate
		{
			// ── 场景 1：普通 TextBox（主题默认 Padding=2,1，Light 皮肤）──
			var tb = new TextBox { Text = "padding-check 0123", Width = 220, Height = 30, FontSize = 13 };
			var w1 = new Window { Width = 300, Height = 90, Content = tb };
			w1.Show();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			using (var frame = HeadlessWindowExtensions.CaptureRenderedFrame(w1))
			{
				Assert.NotNull(frame);
				SaveFrame(frame, "fix14-after-textbox-padding.png");
				// 墨色判定：与 TextBox 背景色亮度差 > 80（文字前景高对比；边框灰/AA 混色 ~50 不计）
				Color bg = ((Application.Current!.TryFindResource("TextBox.Static.Background", out object? bgRes)
					? bgRes : null) as ISolidColorBrush)?.Color ?? Colors.White;
				int bgLum = (bg.R + bg.G + bg.B) / 3;
				bool IsInk(Color c)
				{
					int lum = (c.R + c.G + c.B) / 3;
					return Math.Abs(lum - bgLum) > 80;
				}
				// TextBox 在窗口中的位置（含 1px 边框）
				Point origin = tb.TranslatePoint(new Point(0, 0), w1)!.Value;
				double tx = origin.X, ty = origin.Y;
				double w = tb.Bounds.Width, h = tb.Bounds.Height;
				using (var l = frame.Lock())
				{
					Color Read(double x, double y)
					{
						IntPtr rowPtr = l.Address + (int)y * l.RowBytes;
						return Color.FromArgb(
							System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, (int)x * 4 + 3),
							System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, (int)x * 4),
							System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, (int)x * 4 + 1),
							System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, (int)x * 4 + 2));
					}
					// 左内边距条带（边框 1px 内侧的 2px）：不得有墨色（修复前文字 0px 贴左边框）
					for (int y = (int)ty + 3; y <= (int)(ty + h) - 4; y++)
					{
						for (int x = (int)tx + 1; x <= (int)tx + 2; x++)
						{
							if (IsInk(Read(x, y)))
							{
								leftGapInk++;
							}
						}
					}
					// 上内边距条带（边框 1px 内侧的 1px）：不得有墨色（修复前文字贴上边框）
					for (int x = (int)tx + 3; x <= (int)(tx + w) - 4; x++)
					{
						if (IsInk(Read(x, (int)ty + 1)))
						{
							topGapInk++;
						}
					}
					// 文字墨色必须真实存在于内边距之外（x≥边框1+左内边距2，y≥边框1+上内边距1）
					for (int y = (int)ty + 2; y <= (int)(ty + h) - 3; y++)
					{
						for (int x = (int)tx + 3; x <= (int)(tx + w) - 4; x++)
						{
							if (IsInk(Read(x, y)))
							{
								textInk++;
							}
						}
					}
				}
			}
			w1.Close();

			// ── 场景 2：重命名编辑框（同 Fix10 场景进编辑态，真实重命名路径；内边距不变量
			//    由 RenameEditorStyleTests 属性断言守卫，此处出视觉证据截图）──
			var (window, etb) = BuildFix10Scenario();
			etb.IsInEditMode = true;
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			System.Threading.Tasks.Task.Delay(100).GetAwaiter().GetResult();
			Dispatcher.UIThread.RunJobs();
			TextBox editor = window.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
			Assert.NotNull(editor);
			using (var frame2 = HeadlessWindowExtensions.CaptureRenderedFrame(window))
			{
				Assert.NotNull(frame2);
				SaveFrame(frame2, "fix14-after-rename-editor-padding.png");
			}
			window.Close();
			return 0;
		}).GetAwaiter().GetResult();

		Assert.True(leftGapInk == 0, "左边框内侧 2px 内边距条带出现文字墨色（" + leftGapInk + " 像素）——文字仍贴左边框");
		Assert.True(topGapInk == 0, "上边框内侧 1px 内边距条带出现文字墨色（" + topGapInk + " 像素）——文字仍贴上边框");
		Assert.True(textInk > 100, "内边距之外未见文字墨色（" + textInk + " 像素）——文字未渲染");
		foreach (string file in new[] { "fix14-after-textbox-padding.png", "fix14-after-rename-editor-padding.png" })
		{
			string path = Path.Combine(EvidenceDir(), file);
			Assert.True(File.Exists(path) && new FileInfo(path).Length > 1000, file + " 应生成");
		}
	}

		// ============================ 修复8：二进制 Hex Diff 视图 ============================

		private static string CreateFix8BinaryRepo()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpevidence_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			string oldDir = Directory.GetCurrentDirectory();
			try
			{
				Directory.SetCurrentDirectory(root);
				RunGit("init -q");
				RunGit("config user.email test@example.com");
				RunGit("config user.name Test");
				// 初始版本：前 512 字节递增序列（可读 hex）
				byte[] v1 = new byte[512];
				for (int i = 0; i < v1.Length; i++) v1[i] = (byte)(i % 251);
				File.WriteAllBytes(Path.Combine(root, "logo.bin"), v1);
				RunGit("add logo.bin");
				RunGit("commit -q -m base");
				// 修改版：一部分字节变化 + 追加数据（有 diff 高亮也有长度差异）
				byte[] v2 = new byte[640];
				for (int i = 0; i < v2.Length; i++) v2[i] = (byte)((i * 7 + 13) % 253);
				File.WriteAllBytes(Path.Combine(root, "logo.bin"), v2);
			}
			finally
			{
				Directory.SetCurrentDirectory(oldDir);
			}
			return root;
		}

		private static void RunGit(string args)
		{
			var psi = new System.Diagnostics.ProcessStartInfo("git", args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new Exception("git " + args + " 失败: " + err);
			}
		}

		[Fact]
		public void Fix8_HexDiff_ScreenshotEvidence()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repoRoot = CreateFix8BinaryRepo();
			try
			{
				var module = new GitModule(repoRoot, Path.Combine(repoRoot, ".git"), null, null);
				GitCommandResult<ChangedFilesCollection> statusResult = new GetChangedFilesGitCommand().Execute(module);
				Assert.True(statusResult.Succeeded, "git status 失败: " + statusResult.Error);
				ChangedFile binFile = statusResult.Result.ChangedFiles.FirstOrDefault(f => f.Path.EndsWith(".bin"));
				Assert.NotNull(binFile);

				GitCommandResult<DiffContent> diffResult = new GetWorkingDirectoryFileChangesGitCommand().Execute(
					module, binFile, null, 3, 4, false, false, false, resolvedConflict: false);
				Assert.True(diffResult.Succeeded, "diff 加载失败: " + diffResult.Error);

				int[] pixels = new int[1];
				object[] subView = new object[1];
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					var repoControl = new RepositoryUserControl();
					typeof(RepositoryUserControl).GetProperty("GitModule")!
						.SetValue(repoControl, module);

					var control = new FileDiffControl();
					control.RepositoryUserControl = repoControl;
					var window = new ForkPlus.UI.CustomWindow { Width = 1000, Height = 600, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					control.Content = diffResult;
					// 二进制路径：JobQueue + Dispatcher.Post + SetContent 内部 Task.Run
					Task.Delay(600).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					Task.Delay(200).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();

					subView[0] = control.CurrentSubView;
					using (var frame = HeadlessWindowExtensions.CaptureRenderedFrame(window))
					{
						SaveFrame(frame, "fix8-hexdiff.png");
						pixels[0] = CountNonBlankPixels(frame);
					}
					window.Close();
					return 0;
				}).GetAwaiter().GetResult();

				string shotPath = Path.Combine(EvidenceDir(), "fix8-hexdiff.png");
				Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length > 1000, "fix8-hexdiff.png 应生成");
				Assert.True(subView[0] is ForkPlus.UI.Controls.Editor.Hex.HexDiffUserControl,
					"二进制 diff 应显示 HexDiff 视图，实际: " + (subView[0]?.GetType().Name ?? "<null>"));
				Assert.True(pixels[0] > 2000,
					"Hex Diff 渲染近乎全空白（非空白像素=" + pixels[0] + "）");
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { }
			}
		}
		// ============================ 修复11：git mm 手册滚动条 + 文字选区 ============================
		// 用户报告（2026-09-04）："git mm 文档显示那个界面，好像没有滚动条，选中文字的样式也不对"。
		// headless 测试宿主下 WebView2 兼容层走降级渲染（ScrollViewer + MarkdownHtmlRenderer），
		// 滚动条与 SelectableTextBlock 选区都在本窗口渲染管线内，可像素级取证；
		// 原生路径（Win / macOS / 装了 WPE WebKit 的 Linux）由真浏览器引擎渲染，滚动/选区天然正确。
		[Fact]
		public void Fix11_GitMmManual_ScrollbarAndSelection_ScreenshotEvidence()
		{
			HeadlessAppBootstrap.EnsureStarted();
			int[] contentPixels = new int[1];
			int[] selectionDiffPixels = new int[1];
			bool[] scrollBarVisible = new bool[1];
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var window = new ForkPlus.UI.Dialogs.GitMmReferenceWindow();
				window.Show();
				// Loaded → InitializeManualWebView（async void）：等导航 + 降级渲染 + 兼容桥事件
				Dispatcher.UIThread.RunJobs();
				Task.Delay(300).GetAwaiter().GetResult();
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Dispatcher.UIThread.RunJobs();

				// 手册真实渲染进降级路径（文档缺失/转换失败时 Content 为 null）
				ScrollViewer viewer = Assert.IsType<ScrollViewer>(window.ManualWebView.Content);
				Assert.True(viewer.Extent.Height > viewer.Viewport.Height,
					"手册内容应溢出视口（" + viewer.Extent.Height + " vs " + viewer.Viewport.Height + "）");
				// 滚动条断言：找降级渲染器模板自己的 PART_VerticalScrollBar（判定：祖父就是 viewer
				// 本身）。viewer 的后代里还嵌着 Markdown 代码块的子 ScrollViewer（垂直条 Disabled、
				// IsVisible=false），按 Orientation 或窗口级 FirstOrDefault 都会拿错。
				ScrollBar bar = viewer.GetVisualDescendants().OfType<ScrollBar>()
					.FirstOrDefault(b => b.Orientation == Avalonia.Layout.Orientation.Vertical
						&& b.Parent is Control templateRoot && templateRoot.Parent == viewer);
				scrollBarVisible[0] = bar != null && bar.IsVisible && bar.Maximum > 0;

				// 截帧 A：滚动条可见态。frameA 不提前 dispose——选区对比帧 B 还要用它做像素差。
			Avalonia.Media.Imaging.WriteableBitmap frameA = HeadlessWindowExtensions.CaptureRenderedFrame(window);
			Assert.NotNull(frameA);
			SaveFrame(frameA, "fix11-gitmm-scrollbar.png");
			contentPixels[0] = CountNonBlankPixels(frameA);

				// 选中第一段手册文字：同一滚动位置截帧对比，选区高亮应产生可见像素差。
				// 注意：手册走 Inlines 复杂内容（Text 属性为空，实际文本在 Inlines.Text），
				// 取首个有实际文本的块设选区。
				Avalonia.Controls.SelectableTextBlock selectable = viewer.GetVisualDescendants()
					.OfType<Avalonia.Controls.SelectableTextBlock>()
					.FirstOrDefault(b => !string.IsNullOrEmpty(b.Text ?? b.Inlines?.Text));
				Assert.NotNull(selectable);
				string text = selectable.Text ?? selectable.Inlines?.Text ?? "";
				Assert.True(text.Length > 0, "手册文本块不应为空");
				selectable.SelectionStart = 0;
				selectable.SelectionEnd = Math.Min(48, text.Length);
				Dispatcher.UIThread.RunJobs();
				using (var frameB = HeadlessWindowExtensions.CaptureRenderedFrame(window))
				{
					Assert.NotNull(frameB);
					SaveFrame(frameB, "fix11-gitmm-selection.png");
					selectionDiffPixels[0] = CountDifferentPixels(frameA, frameB);
				}
				frameA.Dispose();
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();

			Assert.True(scrollBarVisible[0], "内容溢出时垂直滚动条应可见（修复：git mm 无滚动条）");
			Assert.True(contentPixels[0] > 2000,
				"手册渲染近乎全空白（非空白像素=" + contentPixels[0] + "）");
			Assert.True(selectionDiffPixels[0] > 100,
				"文字选区高亮未渲染（像素差=" + selectionDiffPixels[0] + "）——选中样式修复不可见");
		}

		// ============================ 修复12：颜色管理器弹窗锚定 + 打开态 ============================
		// 用户报告（2026-09-04）："颜色管理器那个面板弹出来不对"（按下即弹 + 跟随指针漂移 + 失焦关不掉）。
		// 证据：① 列表态截帧；② 生产路径（ColorPreview_Click 抬起弹出 + PlacementTarget 锚定 Bottom）
		// 打开后的弹窗内容截帧；③ 几何断言——弹窗位于被点色块正下方，不再跟随指针。
		[Fact]
		public void Fix12_ColorManagerPopup_ScreenshotEvidence()
		{
			HeadlessAppBootstrap.EnsureStarted();
			int[] closedPixels = new int[1];
			int[] popupPixels = new int[1];
			bool[] belowSwatch = new bool[1];
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var dialog = new ForkPlus.UI.Dialogs.CustomColorsDialog();
				dialog.Show();
				dialog.UpdateLayout();
				Dispatcher.UIThread.RunJobs();

				using (var frame = HeadlessWindowExtensions.CaptureRenderedFrame(dialog))
				{
					Assert.NotNull(frame);
					SaveFrame(frame, "fix12-colors-closed.png");
					closedPixels[0] = CountNonBlankPixels(frame);
				}

				// 生产打开路径：反射调用 ColorPreview_Click（sender=色块 Border，e 未使用）
				Avalonia.Controls.Border swatch = dialog.GetVisualDescendants().OfType<Avalonia.Controls.Border>()
					.FirstOrDefault(b => b.Tag is ForkPlus.UI.Dialogs.CustomColorsDialog.CustomColorItem);
				Assert.NotNull(swatch);
				MethodInfo click = typeof(ForkPlus.UI.Dialogs.CustomColorsDialog).GetMethod("ColorPreview_Click",
					BindingFlags.NonPublic | BindingFlags.Instance);
				Assert.NotNull(click);
				click.Invoke(dialog, new object[] { swatch, null });
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs();

				Avalonia.Controls.Primitives.Popup popup = dialog.ColorPickerPopup;
				Assert.True(popup.IsOpen, "点击色块（抬起）应打开颜色选择器 Popup");

				// 弹窗内容（headless 下 Popup 经 OverlayPopupHost 渲染进主窗口视觉树）
				Visual popupContent = popup.Child;
				Assert.NotNull(popupContent);

				// 几何证据：弹窗顶边在色块底边下方、水平基本对齐（Placement=Bottom 锚定，不跟随指针）。
				// Avalonia 12 的 Popup 无 Host 属性、PopupRoot 无 Position（headless 下也没有独立
				// PopupRoot 窗口，走 OverlayPopupHost 弹层），统一用 TransformToVisual 换算到对话框坐标。
				Matrix? toDialog = popupContent.TransformToVisual(dialog);
				Matrix? swatchToDialog = swatch.TransformToVisual(dialog);
				Assert.NotNull(toDialog);
				Assert.NotNull(swatchToDialog);
				Point popupTopLeft = toDialog.Value.Transform(new Point(0, 0));
				Point swatchBottomLeft = swatchToDialog.Value.Transform(new Point(0, swatch.Bounds.Height));
				belowSwatch[0] = popupTopLeft.Y >= swatchBottomLeft.Y - 2
					&& Math.Abs(popupTopLeft.X - swatchBottomLeft.X) < 260;

				// 弹窗内容截帧（RenderTargetBitmap 直接渲染弹层内容）
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(
					new PixelSize(Math.Max(1, (int)Math.Ceiling(popupContent.Bounds.Width)),
						Math.Max(1, (int)Math.Ceiling(popupContent.Bounds.Height))));
				rtb.Render(popupContent);
				rtb.Save(Path.Combine(EvidenceDir(), "fix12-colors-popup-open.png"));
				using (var wb = new Avalonia.Media.Imaging.WriteableBitmap(rtb.PixelSize, rtb.Dpi))
				{
					using (ILockedFramebuffer fb = wb.Lock())
					{
						rtb.CopyPixels(fb);
						int len = fb.RowBytes * fb.Size.Height;
						byte[] pixels = new byte[len];
						System.Runtime.InteropServices.Marshal.Copy(fb.Address, pixels, 0, len);
						const int bpp = 4;
						for (int y = 0; y < fb.Size.Height; y++)
						{
							int row = y * fb.RowBytes;
							for (int x = 0; x < fb.Size.Width; x++)
							{
								int o = row + x * bpp;
								byte b = pixels[o], g = pixels[o + 1], r = pixels[o + 2];
								if (r < 230 || g < 230 || b < 230)
								{
									popupPixels[0]++;
								}
							}
						}
					}
				}
				rtb.Dispose();

				popup.IsOpen = false;
				Dispatcher.UIThread.RunJobs();
				dialog.Close();
				return 0;
			}).GetAwaiter().GetResult();

			Assert.True(closedPixels[0] > 2000, "颜色列表渲染近乎全空白");
			Assert.True(popupPixels[0] > 500, "颜色选择器弹窗内容渲染近乎全空白（非空白像素=" + popupPixels[0] + "）");
			Assert.True(belowSwatch[0], "弹窗应锚定在被点色块正下方（Placement=Bottom），不跟随指针");
		}

		// ============================ 修复13：颜色选择器渐变坐标 ============================
		// 用户报告（2026-09-04）："颜色选择器那个面板弹出来不对，整个不是彩色的而是黑色的"。
		// 根因（RelativePoint 探针实锤）：WPF LinearGradientBrush 的 StartPoint/EndPoint 是
		// 0~1 相对坐标；Avalonia 用 RelativePoint——裸数字 "1,0" 解析为 Absolute（绝对像素）！
		// 渐变轴坍缩成 1px 长 + SpreadMode 默认 Pad 用末 stop 填满整块：
		//   HsvValRect（透明→黑）→ 除顶 1px 外全黑盖住底色/白渐变 → 画布全黑；
		//   HueCanvas 彩虹 7 stops → 末 stop Red 填满 → 色相条纯红。
		// 修复：三处渐变坐标改百分比（"0%,0%"→"100%,0%" 等）。
		// 本测试两层守卫：1) 画刷结构（StartPoint/EndPoint 必须 Relative，防 XAML 改回去）；
		// 2) 像素级——彩虹条 G/B 通道显著存在（纯红时≈0），HSV 画布上半区非黑（修复前全黑）。
		[Fact]
		public void Fix13_ColorPickerGradients_RelativeUnitsAndPixels_ScreenshotEvidence()
		{
			HeadlessAppBootstrap.EnsureStarted();
			int[] hueGreenPixels = new int[1];
			int[] hueBluePixels = new int[1];
			int[] hsvNonBlackPixels = new int[1];
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var dialog = new ForkPlus.UI.Dialogs.CustomColorsDialog();
				dialog.Show();
				Dispatcher.UIThread.RunJobs();

				// 生产打开路径（同 Fix12）：抬起弹出 + 锚定
				Avalonia.Controls.Border swatch = dialog.GetVisualDescendants().OfType<Avalonia.Controls.Border>()
					.FirstOrDefault(b => b.Tag is ForkPlus.UI.Dialogs.CustomColorsDialog.CustomColorItem);
				Assert.NotNull(swatch);
				MethodInfo click = typeof(ForkPlus.UI.Dialogs.CustomColorsDialog).GetMethod("ColorPreview_Click",
					BindingFlags.NonPublic | BindingFlags.Instance);
				Assert.NotNull(click);
				click.Invoke(dialog, new object[] { swatch, null });
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				Visual popupContent = dialog.ColorPickerPopup.Child;
				Assert.NotNull(popupContent);

				// ── 结构守卫：三处渐变画刷的坐标必须 Relative（裸数字=Absolute 即回归） ──
				Avalonia.Controls.Shapes.Rectangle satRect = dialog.GetVisualDescendants()
					.OfType<Avalonia.Controls.Shapes.Rectangle>().First(r => r.Name == "HsvSatRect");
				Avalonia.Controls.Shapes.Rectangle valRect = dialog.GetVisualDescendants()
					.OfType<Avalonia.Controls.Shapes.Rectangle>().First(r => r.Name == "HsvValRect");
				Canvas hueCanvas = dialog.GetVisualDescendants().OfType<Canvas>().First(c => c.Name == "HueCanvas");
				Avalonia.Controls.Shapes.Rectangle hueRainbow = hueCanvas.GetVisualDescendants()
					.OfType<Avalonia.Controls.Shapes.Rectangle>().First();
				var satBrush = Assert.IsType<LinearGradientBrush>(satRect.Fill);
				var valBrush = Assert.IsType<LinearGradientBrush>(valRect.Fill);
				var rainbowBrush = Assert.IsType<LinearGradientBrush>(hueRainbow.Fill);
				Assert.All(new[] { satBrush, valBrush, rainbowBrush }, delegate (LinearGradientBrush b)
				{
					Assert.Equal(RelativeUnit.Relative, b.StartPoint.Unit);
					Assert.Equal(RelativeUnit.Relative, b.EndPoint.Unit);
				});

				// ── 像素守卫：渲染弹层，按 HsvCanvas/HueCanvas 实际位置分区域统计 ──
				var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(
					new PixelSize(Math.Max(1, (int)Math.Ceiling(popupContent.Bounds.Width)),
						Math.Max(1, (int)Math.Ceiling(popupContent.Bounds.Height))));
				rtb.Render(popupContent);
				rtb.Save(Path.Combine(EvidenceDir(), "fix13-colorpicker-gradient.png"));

				Canvas hsvCanvas = dialog.GetVisualDescendants().OfType<Canvas>().First(c => c.Name == "HsvCanvas");
				Matrix? hsvToContent = hsvCanvas.TransformToVisual(popupContent);
				Matrix? hueToContent = hueCanvas.TransformToVisual(popupContent);
				Assert.NotNull(hsvToContent);
				Assert.NotNull(hueToContent);
				Rect hsvRect = new Rect(hsvToContent.Value.Transform(new Point(0, 0)),
					new Size(hsvCanvas.Bounds.Width, hsvCanvas.Bounds.Height));
				Rect hueRect = new Rect(hueToContent.Value.Transform(new Point(0, 0)),
					new Size(hueCanvas.Bounds.Width, hueCanvas.Bounds.Height));

				using (var wb = new Avalonia.Media.Imaging.WriteableBitmap(rtb.PixelSize, rtb.Dpi))
				using (rtb)
				{
					using (ILockedFramebuffer fb = wb.Lock())
					{
						// 先把 rtb 像素拷进 wb 再读（第一版漏了 CopyPixels 直接读空白 wb，
						// 全 0 像素导致假失败）
						rtb.CopyPixels(fb);
						int len = fb.RowBytes * fb.Size.Height;
						byte[] pixels = new byte[len];
						System.Runtime.InteropServices.Marshal.Copy(fb.Address, pixels, 0, len);
						const int bpp = 4;
						for (int y = 0; y < fb.Size.Height; y++)
						{
							int row = y * fb.RowBytes;
							for (int x = 0; x < fb.Size.Width; x++)
							{
								int o = row + x * bpp;
								byte b = pixels[o], g = pixels[o + 1], r = pixels[o + 2];
								if (hueRect.Contains(new Point(x, y)))
								{
									// 彩虹条：黄绿青段 G 高、青蓝紫段 B 高。修复前纯红两者≈0。
									if (g > 100) { hueGreenPixels[0]++; }
									if (b > 100) { hueBluePixels[0]++; }
								}
								// HSV 画布上半区（白色饱和度渐变 + 色相底色；下半被黑渐变盖）：
								// 修复前全黑（仅顶 1px 残留白线）。
								if (hsvRect.Contains(new Point(x, y)) && y - hsvRect.Y < hsvRect.Height * 0.5)
								{
									if (Math.Max(r, Math.Max(g, b)) > 60) { hsvNonBlackPixels[0]++; }
								}
							}
						}
					}
				}

				dialog.ColorPickerPopup.IsOpen = false;
				Dispatcher.UIThread.RunJobs();
				dialog.Close();
				return 0;
			}).GetAwaiter().GetResult();

			Assert.True(hueGreenPixels[0] > 350,
				"色相条应有绿色系区段（G>100 像素=" + hueGreenPixels[0] + "）——修复前整条纯红");
			Assert.True(hueBluePixels[0] > 350,
				"色相条应有蓝色系区段（B>100 像素=" + hueBluePixels[0] + "）——修复前整条纯红");
			Assert.True(hsvNonBlackPixels[0] > 2000,
				"HSV 画布上半区应渲染白渐变+色相底色而非全黑（非黑像素=" + hsvNonBlackPixels[0] + "）");
		}

		/// <summary>逐像素对比两帧（容忍 8 级抗锯齿噪声），返回可见差异像素数。</summary>
		private static int CountDifferentPixels(
			Avalonia.Media.Imaging.WriteableBitmap a, Avalonia.Media.Imaging.WriteableBitmap b)
		{
			Assert.Equal(a.PixelSize, b.PixelSize);
			int count = 0;
			using (ILockedFramebuffer fa = a.Lock())
			using (ILockedFramebuffer fb = b.Lock())
			{
				int len = fa.RowBytes * fa.Size.Height;
				byte[] pa = new byte[len];
				byte[] pb = new byte[len];
				System.Runtime.InteropServices.Marshal.Copy(fa.Address, pa, 0, len);
				System.Runtime.InteropServices.Marshal.Copy(fb.Address, pb, 0, len);
				const int bpp = 4;
				for (int y = 0; y < fa.Size.Height; y++)
				{
					int row = y * fa.RowBytes;
					for (int x = 0; x < fa.Size.Width; x++)
					{
						int o = row + x * bpp;
						int diff = Math.Abs(pa[o] - pb[o]) + Math.Abs(pa[o + 1] - pb[o + 1])
							+ Math.Abs(pa[o + 2] - pb[o + 2]);
						if (diff > 24)
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
