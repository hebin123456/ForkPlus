// 回归测试（2026-09-03，"弹窗 TextBox 选中后颜色肉眼无法识别"两轮修复产物）：
// 第一轮（7b13c1f）：自定义 TextBox ControlTheme 的 SelectionBrush/SelectionForegroundBrush
// 未设置且未传入模板 TextPresenter → 补画刷。但用户仍看不清——画刷非空≠选区可见。
// 第二轮（本轮，像素探针实测定位真根因）：TextPresenter 漏绑
// SelectionStart/SelectionEnd（实测程序化设选区后 presenter 恒 0/0，选区索引根本
// 到不了渲染层，画刷再对也不画任何高亮）。Avalonia 官方 Fluent 主题（12.1.1）
// 全部经 TemplateBinding 下传：SelectionStart/SelectionEnd/CaretIndex/PasswordChar/
// TextAlignment/TextWrapping——漏一个对应功能即静默失效。本轮对齐官方主题补齐全部绑定，
// 同因顺带修复：PasswordChar 未下传（AskPassWindow/SshPassphraseWindow 密码明文）、
// TextWrapping 未下传（StatisticsUserControl 排除框不换行）。
// 本测试守卫三层：1) 选区索引流入 presenter；2) 像素级 accent 高亮真实渲染（含基线对照）；
// 3) PasswordChar 掩码下传。像素层用 RenderTargetBitmap（HeadlessAppBootstrap 已启用
// UseSkia + UseHeadlessDrawing=false，真渲染后端；纯 headless 绘图模式 RTB 渲染为空）。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using System;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class TextBoxSelectionStyleTests
	{
		// 渲染控件并统计满足谓词的像素数（BGRA8888）。分辨率 320x90 足够容纳测试 TextBox。
		private static int CountPixels(Control control, Func<Color, bool> predicate)
		{
			using var rtb = new RenderTargetBitmap(new PixelSize(320, 90));
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			rtb.Render(control);
			using var wb = new WriteableBitmap(rtb.PixelSize, rtb.Dpi);
			int count = 0;
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
						byte b = pixels[o], g = pixels[o + 1], r = pixels[o + 2], a = pixels[o + 3];
						if (predicate(Color.FromArgb(a, r, g, b)))
						{
							count++;
						}
					}
				}
			}
			return count;
	}

	[Fact]
		public void ErrorWindow_MessageTextBox_HasVisibleSelectionStyle()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var err = new ErrorWindow("fatal: git error output\nsecond line");
				err.Show();
				Dispatcher.UIThread.RunJobs();

				TextBox msgBox = err.GetVisualDescendants().OfType<TextBox>().First((TextBox b) => b.Name == "MessageTextBox");
				TextPresenter presenter = msgBox.GetVisualDescendants().OfType<TextPresenter>().First();

				ISolidColorBrush selBg = presenter.SelectionBrush as ISolidColorBrush;
				ISolidColorBrush selFg = presenter.SelectionForegroundBrush as ISolidColorBrush;

				Assert.NotNull(selBg);
				Assert.NotEqual(Colors.Transparent, selBg.Color);
				Assert.NotEqual(default, selBg.Color);
				Assert.NotNull(selFg);
				// 前景必须与选区背景不同色，否则选中文字不可读（原 bug 的"看不清"形态）。
				Assert.NotEqual(selBg.Color, selFg.Color);

				err.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void PlainTextBox_HasVisibleSelectionStyle()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var tb = new TextBox { Text = "hello world", Width = 200, Height = 30 };
				var window = new Window { Width = 300, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
				ISolidColorBrush selBg = presenter.SelectionBrush as ISolidColorBrush;
				ISolidColorBrush selFg = presenter.SelectionForegroundBrush as ISolidColorBrush;
				Assert.NotNull(selBg);
				Assert.NotEqual(Colors.Transparent, selBg.Color);
				Assert.NotEqual(default, selBg.Color);
				Assert.NotNull(selFg);
				Assert.NotEqual(selBg.Color, selFg.Color);
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void PlaceholderTextBox_HasVisibleSelectionStyle()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var tb = new PlaceholderTextBox { Text = "hello world", Placeholder = "ph", Width = 200, Height = 30 };
				var window = new Window { Width = 300, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
				ISolidColorBrush selBg = presenter.SelectionBrush as ISolidColorBrush;
				ISolidColorBrush selFg = presenter.SelectionForegroundBrush as ISolidColorBrush;
				Assert.NotNull(selBg);
				Assert.NotEqual(Colors.Transparent, selBg.Color);
				Assert.NotEqual(default, selBg.Color);
				Assert.NotNull(selFg);
				Assert.NotEqual(selBg.Color, selFg.Color);
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		// ── 第二轮根因守卫：选区索引必须流入 TextPresenter（修复前恒 0/0） ──

		[Fact]
		public void ProgrammaticSelection_ReachesTextPresenter_PlainReadOnlyAndPlaceholder()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(bool plain, bool readOnly, bool placeholder) = HeadlessAppBootstrap.Run(delegate
			{
				bool Check(ControlFactory factory)
				{
					var tb = factory();
					tb.SelectionStart = 2;
					tb.SelectionEnd = 10;
					var window = new Window { Width = 300, Height = 100, Content = tb };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
					bool ok = presenter.SelectionStart == 2 && presenter.SelectionEnd == 10;
					window.Close();
					return ok;
				}

				return (
					Check(delegate { return new TextBox { Text = "0123456789ABCDEF", Width = 200, Height = 30 }; }),
					Check(delegate { return new TextBox { Text = "0123456789ABCDEF", Width = 200, Height = 30, IsReadOnly = true }; }),
					Check(delegate { return new PlaceholderTextBox { Text = "0123456789ABCDEF", Placeholder = "ph", Width = 200, Height = 30 }; }));
			});
			// 修复前 presenter.SelectionStart/End 恒为 0/0——画刷非空但选区索引到不了渲染层。
			Assert.True(plain, "普通 TextBox 选区索引未传入 TextPresenter（选区不渲染）");
			Assert.True(readOnly, "只读 TextBox 选区索引未传入 TextPresenter（git mm 输出弹窗场景）");
			Assert.True(placeholder, "PlaceholderTextBox 选区索引未传入 TextPresenter（各类弹窗输入框场景）");
		}

		// ── 像素层守卫：选中后必须真实画出选区高亮（有基线对照，防"恒亮"假阳性）──
	// 2026-09-04（"重命名仓库时选中颜色不对"）起选区为全皮肤固定蓝 TextBox.Selection.Background
	//（原 accent 蓝），像素探针同步改为统计"选区画刷色"像素。

	[Fact]
	public void Selection_RendersSelectionBrushPixels_WithCleanBaseline()
	{
		HeadlessAppBootstrap.EnsureStarted();
		(int selected, int baseline) = HeadlessAppBootstrap.Run(delegate
		{
			Color selectionBg = ResolveSelectionBackgroundColor();
			bool IsSelection(Color c) => c.A == selectionBg.A && c.R == selectionBg.R && c.G == selectionBg.G && c.B == selectionBg.B;

			var selected = new TextBox { Text = "0123456789ABCDEF", Width = 260, Height = 32, FontSize = 14 };
			selected.SelectionStart = 2;
			selected.SelectionEnd = 10;
			var w1 = new Window { Width = 320, Height = 90, Content = selected };
			w1.Show();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			int withSel = CountPixels(selected, IsSelection);
			w1.Close();

			var plain = new TextBox { Text = "0123456789ABCDEF", Width = 260, Height = 32, FontSize = 14 };
			var w2 = new Window { Width = 320, Height = 90, Content = plain };
			w2.Show();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			int withoutSel = CountPixels(plain, IsSelection);
			w2.Close();
			return (withSel, withoutSel);
		});
		// 选中后必须出现数百量级的选区蓝像素（实测 ~792）；未选中时基线应为 0（高亮只来自选区）。
		Assert.True(selected > 100, "选中后未渲染选区高亮像素（实测 " + selected + "）——选区不可见回归");
		Assert.Equal(0, baseline);
	}

	// ── 第三轮守卫（2026-09-04，"重命名仓库时选中颜色不对"）：全皮肤选区必须可读 ──
	// 根因：选区曾绑皮肤 AccentBrush + 硬编码白前景。皮肤 accent 常为高亮度
	//（Monokai #A6E22E / YellowDark #FACC15 / CyanDark #22D3EE …），白字对比度 ~1.3:1，
	// 选中文字肉眼不可读。修复后选区为全皮肤固定 #236BD2 + 白（5.1:1，WCAG AA）。
	// 本测试遍历全部 22 个皮肤字典，逐个解析选区前后景并断言 WCAG 对比度 ≥ 4.5:1——
	// 防止未来有人把选区重新绑回 accent 或换上不可读的组合。

	private static readonly string[] AllSkins =
	{
		"Light", "Dark", "SolarizedLight", "SolarizedDark", "Dracula", "GitHubLight", "GitHubDark",
		"Monokai", "PurpleLight", "PurpleDark", "GreenLight", "GreenDark",
		"RedLight", "RedDark", "OrangeLight", "OrangeDark", "YellowLight", "YellowDark",
		"CyanLight", "CyanDark", "BlueLight", "BlueDark"
	};

	internal static Color ResolveSelectionBackgroundColor()
	{
		var brush = Avalonia.Application.Current!.FindResource("TextBox.Selection.Background") as ISolidColorBrush;
		Assert.NotNull(brush);
		return brush!.Color;
	}

	// WCAG 相对亮度对比度（1.0=完全相同，21.0=黑白）
	private static double ContrastRatio(Color a, Color b)
	{
		static double Luminance(Color c)
		{
			static double Channel(byte v)
			{
				double s = v / 255.0;
				return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
			}
			return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
		}
		double la = Luminance(a);
		double lb = Luminance(b);
		double max = Math.Max(la, lb);
		double min = Math.Min(la, lb);
		return (max + 0.05) / (min + 0.05);
	}

	[Fact]
	public void AllSkins_TextBoxSelectionContrast_IsReadable()
	{
		HeadlessAppBootstrap.EnsureStarted();
		System.Collections.Generic.List<string> failures = new System.Collections.Generic.List<string>();
		HeadlessAppBootstrap.Run(delegate
		{
			var app = Avalonia.Application.Current!;
			foreach (string skin in AllSkins)
			{
				// 换肤：加新皮肤 include 再移除旧的（与 App.InitializeTheme 同机制）
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

				var bg = app.FindResource("TextBox.Selection.Background") as ISolidColorBrush;
				var fg = app.FindResource("TextBox.Selection.Foreground") as ISolidColorBrush;
				if (bg == null || fg == null)
				{
					failures.Add(skin + ": 选区画刷资源缺失(bg=" + (bg != null) + ",fg=" + (fg != null) + ")");
					continue;
				}
				double ratio = ContrastRatio(bg.Color, fg.Color);
				if (ratio < 4.5)
				{
					failures.Add(skin + ": 选区对比度 " + ratio.ToString("F2") + ":1 (<4.5) bg=" + bg.Color + " fg=" + fg.Color);
				}
			}
			// 还原默认 Light 皮肤，避免污染同进程后续 headless 测试的主题假设
			var lastOld = app.Resources.MergedDictionaries
				.OfType<global::Avalonia.Markup.Xaml.Styling.ResourceInclude>()
				.FirstOrDefault(i => i.Source?.OriginalString.Contains("Theme/Generic.") == true);
			var restore = new global::Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://ForkPlus/App.axaml"))
			{
				Source = new Uri("avares://ForkPlus/Theme/Generic.Light.axaml")
			};
			app.Resources.MergedDictionaries.Add(restore);
			if (lastOld != null)
			{
				app.Resources.MergedDictionaries.Remove(lastOld);
			}
			Dispatcher.UIThread.RunJobs();
		});
		Assert.True(failures.Count == 0,
			"以下皮肤选区不可读（WCAG < 4.5:1）：" + string.Join("; ", failures));
	}

	// 选区蓝必须是全皮肤一致的应用选中蓝（与列表/树选中项 #236BD2 对齐），不随皮肤 accent 漂移
	[Fact]
	public void SelectionBackground_MatchesAppWideSelectionBlue()
	{
		HeadlessAppBootstrap.EnsureStarted();
		Color expected = Color.FromRgb(0x23, 0x6B, 0xD2);
		Color actual = HeadlessAppBootstrap.Run(ResolveSelectionBackgroundColor);
		Assert.Equal(expected, actual);
	}

		// ── 同因连带修复守卫：PasswordChar 掩码必须下传（密码框明文回归） ──

		[Fact]
		public void PasswordChar_ReachesTextPresenter()
		{
			HeadlessAppBootstrap.EnsureStarted();
			char propagated = HeadlessAppBootstrap.Run(delegate
			{
				var tb = new TextBox { Text = "secret123", Width = 200, Height = 30, PasswordChar = '●' };
				var window = new Window { Width = 300, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
				window.Close();
				return presenter.PasswordChar;
			});
			// AskPassWindow/SshPassphraseWindow 用 TextBox+PasswordChar 承担密码框语义，
			// 掩码字符不下传 presenter 即明文显示。
			Assert.Equal('●', propagated);
		}

		private delegate TextBox ControlFactory();
	}
}
