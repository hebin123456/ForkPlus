// 回归测试（2026-09-04，"TextBox 里面的字和左/上边框贴得太紧"）：
// 原版 WPF 的 TextBox 模板经 PART_ContentHost（ScrollViewer）自动应用 Padding；
// 迁移模板把 TextPresenter 直接放 Border 里——Padding 必须经
// Margin="{TemplateBinding Padding}" 下传 TextPresenter，且每个 TextBox 家族主题必须
// 有非零默认 Padding，否则：显式 Padding（应用内 100+ 处使用）对真实文字无效
// （只作用于 placeholder），未显式设置处文字 0px 贴左/上边框。
// 本测试逐主题断言两条不变量：1) 控件实际 Padding 非零；2) Padding 真实到达渲染层
//（presenter.Margin == 控件 Padding）。覆盖 TextBox / PlaceholderTextBox /
// AutoCompleteTextBox / FilterTextBox / CommitDescriptionTextBox 及两个 keyed 主题
//（CommitPlaceholderTextBox / SearchPanelPlaceholderTextBox，经 Theme= 显式应用）。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class TextBoxPaddingTests
	{
		// 通用不变量：控件 Padding 非零 + Padding 到达 TextPresenter（渲染层真值）
		private static void AssertPaddingReachesPresenter(TextBox tb, string label)
		{
			Assert.NotEqual(default(Thickness), tb.Padding); // 主题默认/显式值必须非零（防 0px 贴边回归）
			TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
			Assert.Equal(tb.Padding, presenter.Margin); // Padding 未下传 → 文字贴边框（模板漏 Margin 绑定回归）
			Assert.True(tb.Padding.Left >= 1 && tb.Padding.Top >= 1,
				label + " 左/上内边距不足（Left=" + tb.Padding.Left + ", Top=" + tb.Padding.Top + "）");
		}

		private static Window ShowInWindow(Control control)
		{
			var window = new Window { Width = 300, Height = 100, Content = control };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			return window;
		}

		// keyed ControlTheme（CommitPlaceholderTextBox 等）经资源系统解析
		private static ControlTheme FindKeyedTheme(string key)
		{
			Assert.True(Application.Current!.TryFindResource(key, out object? value), "keyed 主题缺失：" + key);
			return (value as ControlTheme)!;
		}

		[Fact]
		public void PlainTextBox_ThemeDefaultPadding_ReachesPresenter()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				Window window = ShowInWindow(new TextBox { Text = "hello", Width = 200, Height = 30 });
				AssertPaddingReachesPresenter((TextBox)window.Content, "TextBox 主题默认");
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void PlainTextBox_ExplicitPadding_ReachesPresenter()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				// 应用内 100+ 处显式 Padding（如 4,2,4,2）此前对真实文字无效（只作用于 placeholder）
				var tb = new TextBox { Text = "hello", Width = 200, Height = 30, Padding = new Thickness(4, 2, 4, 2) };
				Window window = ShowInWindow(tb);
				Assert.Equal(new Thickness(4, 2, 4, 2), tb.Padding);
				TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
				Assert.Equal(new Thickness(4, 2, 4, 2), presenter.Margin);
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void PlaceholderTextBoxFamily_ThemePadding_ReachesPresenter()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				// PlaceholderTextBox（CommitPlaceholderTextBox/SearchPanelPlaceholderTextBox 等的基主题）
				Window w1 = ShowInWindow(new global::ForkPlus.UI.Controls.PlaceholderTextBox { Text = "hello", Placeholder = "ph", Width = 200, Height = 30 });
				AssertPaddingReachesPresenter((TextBox)w1.Content, "PlaceholderTextBox");
				w1.Close();

				// AutoCompleteTextBox：独立主题（未 BasedOn PlaceholderTextBox，曾漏默认 Padding）
				Window w2 = ShowInWindow(new global::ForkPlus.UI.Controls.AutoCompleteTextBox { Text = "hello", Width = 200, Height = 30 });
				AssertPaddingReachesPresenter((TextBox)w2.Content, "AutoCompleteTextBox");
				w2.Close();

				// FilterTextBox：主题显式 Padding="2,1,2,1"（模板曾漏 Margin 下传）
				Window w3 = ShowInWindow(new global::ForkPlus.UI.Controls.FilterTextBox { Text = "hello", Width = 200, Height = 30 });
				AssertPaddingReachesPresenter((TextBox)w3.Content, "FilterTextBox");
				w3.Close();

				// CommitDescriptionTextBox：主题显式 Padding="4,4,4,2"
				Window w4 = ShowInWindow(new global::ForkPlus.UI.Controls.CommitDescriptionTextBox { Text = "hello", Width = 200, Height = 60 });
				AssertPaddingReachesPresenter((TextBox)w4.Content, "CommitDescriptionTextBox");
				w4.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void KeyedPlaceholderThemes_ExplicitAndDefaultPadding_ReachPresenter()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				// keyed 主题经 Theme= 显式应用（CommitUserControl/搜索面板等的真实用法）
				var commit = new global::ForkPlus.UI.Controls.PlaceholderTextBox
				{
					Text = "commit subject",
					Width = 300,
					Height = 30,
					Theme = FindKeyedTheme("CommitPlaceholderTextBox")
				};
				Window w1 = ShowInWindow(commit);
				AssertPaddingReachesPresenter(commit, "CommitPlaceholderTextBox");
				w1.Close();

				// CommitUserControl 实际用法：keyed 主题 + 显式 Padding="4,1,74,1"
				var commitExplicit = new global::ForkPlus.UI.Controls.PlaceholderTextBox
				{
					Text = "commit subject",
					Width = 300,
					Height = 30,
					Padding = new Thickness(4, 1, 74, 1),
					Theme = FindKeyedTheme("CommitPlaceholderTextBox")
				};
				Window w2 = ShowInWindow(commitExplicit);
				Assert.Equal(new Thickness(4, 1, 74, 1), commitExplicit.Padding);
				TextPresenter commitPresenter = commitExplicit.GetVisualDescendants().OfType<TextPresenter>().First();
				Assert.Equal(new Thickness(4, 1, 74, 1), commitPresenter.Margin);
				w2.Close();

				var search = new global::ForkPlus.UI.Controls.PlaceholderTextBox
				{
					Text = "search",
					Width = 200,
					Height = 30,
					Theme = FindKeyedTheme("SearchPanelPlaceholderTextBox")
				};
				Window w3 = ShowInWindow(search);
				AssertPaddingReachesPresenter(search, "SearchPanelPlaceholderTextBox");
				w3.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}
	}
}
