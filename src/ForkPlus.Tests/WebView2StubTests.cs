// WebView2Stub v3 混合架构回归测试（2026-09-04，"引入官方 Native WebView 替代自研渲染"）。
//
// 测试宿主（testhost/*.Tests）下 NativeAvailable() 恒 false → 控件级用例全部锁住
// 降级渲染路径的行为契约；原生路径的引擎无关部分（prefers-color-scheme CSS 强制改写）
// 是纯文本函数，直接断言改写后结构合法。
//
// 覆盖点：
//  A. ForcePreferredColorScheme：git mm 手册/AI 输出的暗色块跟随应用皮肤（而非 OS）。
//  B. 降级路径结构：ScrollViewer + Auto 滚动条（真机 bug"git mm 无滚动条"的降级面回归）。
//  C. NavigationCompleted 兼容桥：内容布局完成后触发（AiDevelopmentWindow 气泡自动高度依赖）。
//  D. CoreWebView2.ExecuteScriptAsync 转发：修复前是返回 "null" 的空壳——流式滚底
//     （AiStreamingWebView）与气泡高度测量（AiDevelopmentWindow）经 CoreWebView2 调用，
//     两条路径下全部静默失效。这里锁住 scrollHeight 返回可解析像素值 + scrollTo 真滚动。
//  E. WebMessageReceived 兼容桥：降级渲染器按钮回调 → CoreWebView2.WebMessageReceived
//     （AiCodeReviewWindow 的 preview/apply suggestion 按钮链路）。
using System;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;
using ForkPlus.UI.WpfCompat;
using Xunit;
using Wv2 = Microsoft.Web.WebView2.Wpf.WebView2;

namespace ForkPlus.Tests
{
	// HeadlessAvalonia 集合（2026-09-07 补，全量回归 BinaryDiff 用例修复）：
	// 本类大量调用 HeadlessAppBootstrap.Run（UI 线程 InvokeAsync），此前缺集合属性
	// 与 HeadlessAvalonia 集合并行——Run 收尾的 CloseLeftoverTestWindows 会误关并行
	// 用例正在使用的窗口（BinaryDiff CustomWindow 在 Task.Delay 阻塞 UI 线程期间，
	// 本类排队的 Run 体经对方 RunJobs() 插队执行，窗口被关 → ObjectDisposedException）。
	// 所有触碰 Dispatcher.UIThread / HeadlessAppBootstrap 的测试类必须入该集合串行。
	[Collection("HeadlessAvalonia")]
	public class WebView2StubTests
	{
		// ── A. CSS 强制改写（引擎无关，原生路径用） ──

		[Fact]
		public void ForcePreferredColorScheme_Dark_MakesDarkBlockAlwaysApply()
		{
			// GitMmReferenceWindow.CreateHtmlDocument 的真实暗色块
			string css = "@media (prefers-color-scheme: dark){body{color:#ddd;background:#1e1e1e;}}"
				+ "body{color:#222;background:#fff;}";

			string forced = Wv2.ForcePreferredColorScheme(css, dark: true);

			// 暗色块改为"原条件 OR 恒真"，结构与闭合括号保持合法
			Assert.Contains("(prefers-color-scheme: dark), (min-width: 0px)", forced, StringComparison.Ordinal);
			Assert.Contains("){body{color:#ddd;background:#1e1e1e;}}", forced, StringComparison.Ordinal);
			// 亮色基础样式不受影响
			Assert.Contains("body{color:#222;background:#fff;}", forced, StringComparison.Ordinal);
		}

		[Fact]
		public void ForcePreferredColorScheme_Light_MakesDarkBlockNeverApply()
		{
			string css = "@media (prefers-color-scheme: dark){body{color:#ddd;background:#1e1e1e;}}";

			string forced = Wv2.ForcePreferredColorScheme(css, dark: false);

			// 暗色块改为"原条件 AND 恒假"，结构合法
			Assert.Contains("(prefers-color-scheme: dark) and (max-width: 0.001px)", forced, StringComparison.Ordinal);
			Assert.Contains("){body{color:#ddd;background:#1e1e1e;}}", forced, StringComparison.Ordinal);
		}

		[Fact]
		public void ForcePreferredColorScheme_CompactForm_And_NoToken_Passthrough()
		{
			// 无空格写法（minifier 输出）也要改写
			string compact = "@media(prefers-color-scheme:dark){body{color:#ddd;}}";
			Assert.Contains("(prefers-color-scheme:dark), (min-width: 0px)", Wv2.ForcePreferredColorScheme(compact, true), StringComparison.Ordinal);

			// 没有媒体查询的文档原样返回
			string plain = "<html><body><p>hi</p></body></html>";
			Assert.Same(plain, Wv2.ForcePreferredColorScheme(plain, true));
			Assert.Null(Wv2.ForcePreferredColorScheme(null, true));
		}

		// ── B/C/D/E. 降级路径（headless 测试宿主恒走降级渲染器） ──

		private static string TallBody(int paragraphs)
		{
			StringBuilder body = new StringBuilder();
			for (int i = 0; i < paragraphs; i++)
			{
				body.Append("<p>Paragraph line ").Append(i).Append(" with some text.</p>");
			}
			return body.ToString();
		}

		[Fact]
		public void NavigateToString_Headless_RendersFallbackScrollViewerWithAutoScrollbar()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var webView = new Wv2();
				webView.NavigateToString("<html><body>" + TallBody(40) + "</body></html>");
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs();

				// 降级路径 = ScrollViewer 承载，垂直滚动条 Auto（真机 bug"git mm 无滚动条"回归面）
				ScrollViewer viewer = Assert.IsType<ScrollViewer>(webView.Content);
				Assert.Equal(ScrollBarVisibility.Auto, viewer.VerticalScrollBarVisibility);
				Assert.Equal(ScrollBarVisibility.Disabled, viewer.HorizontalScrollBarVisibility);
				// 内容真实渲染进视觉树（非空壳）
				Assert.NotNull(viewer.Content);
			});
		}

		[Fact]
		public void NavigateToString_Headless_RaisesNavigationCompletedAfterLayout()
		{
			bool completed = false;
			bool success = false;
			HeadlessAppBootstrap.Run(delegate
			{
				var webView = new Wv2();
				webView.CoreWebView2.NavigationCompleted += delegate(object sender, CoreWebView2NavigationCompletedEventArgs e)
				{
					completed = true;
					success = e.IsSuccess;
				};
				webView.NavigateToString("<html><body><p>content</p></body></html>");
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Dispatcher.UIThread.RunJobs();
			});
			Assert.True(completed, "NavigationCompleted 应在降级渲染完成后触发");
			Assert.True(success);
		}

		[Fact]
		public void CoreWebView2_ExecuteScriptAsync_ScrollHeight_ReturnsPixelNumber()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var webView = new Wv2();
				Window window = HostInWindow(webView, 400, 300);
				try
				{
				webView.NavigateToString("<html><body>" + TallBody(30) + "</body></html>");
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs();

				// 回归：修复前 CoreWebView2.ExecuteScriptAsync 是返回 "null" 的空壳，
				// AiDevelopmentWindow 的 double.TryParse 直接失败、气泡高度全错
				string height = webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollHeight")
					.GetAwaiter().GetResult();
				Assert.NotEqual("null", height);
				double parsed = double.Parse(height, CultureInfo.InvariantCulture);
				Assert.True(parsed > 0, "scrollHeight 应为正像素值，实际: " + height);
				}
				finally
				{
					window.Close();
				}
			});
		}

		[Fact]
		public void CoreWebView2_ExecuteScriptAsync_ScrollTo_ScrollsFallbackToBottom()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var webView = new Wv2();
				Window window = HostInWindow(webView, 400, 300);
				try
				{
				webView.NavigateToString("<html><body>" + TallBody(60) + "</body></html>");
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs();

				ScrollViewer viewer = Assert.IsType<ScrollViewer>(webView.Content);
				Assert.True(viewer.Extent.Height > viewer.Viewport.Height, "内容应溢出视口");

				// 回归：修复前该调用经 CoreWebView2 空壳返回 "null"，流式滚底静默失效
				string result = webView.CoreWebView2.ExecuteScriptAsync(
					"window.scrollTo(0, document.documentElement.scrollHeight || document.body.scrollHeight)")
					.GetAwaiter().GetResult();
				Assert.Equal("true", result);
				Assert.True(viewer.Offset.Y > 0, "scrollTo 应把降级内容滚到底部");
				}
				finally
				{
					window.Close();
				}
			});
		}

		/// <summary>把控件挂进 headless Window 并完成一次布局（离树 ContentControl 不应用模板，
		/// ScrollViewer 的 Extent/Offset 只有挂树布局后才有值）。</summary>
		private static Window HostInWindow(Control control, double width, double height)
		{
			var window = new Window
			{
				Width = width,
				Height = height,
				Content = control,
				ShowActivated = false,
			};
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs();
			return window;
		}

		[Fact]
		public void FallbackButton_Click_ForwardsWebMessageThroughCoreWebView2()
		{
			string received = null;
			HeadlessAppBootstrap.Run(delegate
			{
				var webView = new Wv2();
				webView.CoreWebView2.WebMessageReceived += delegate(object sender, CoreWebView2WebMessageReceivedEventArgs e)
				{
					received = e.TryGetWebMessageAsString();
				};
				// AiCodeReviewWindow 建议卡按钮的真实 HTML 形状
				webView.NavigateToString(
					"<html><body><button onclick='previewSuggestion(3)'>Preview suggestion</button></body></html>");
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs();

				Button button = Find<Button>(webView);
				Assert.NotNull(button);
				button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				Assert.Equal("preview-suggestion:3", received);
			});
		}

		[Fact]
		public void PreferredColorScheme_Setter_RenavigatesCurrentDocument()
		{
			// 原生路径下 Profile.PreferredColorScheme 赋值 → 重导航（CSS 强制跟随应用皮肤）；
			// 降级路径下同样重渲染。这里用导航完成计数锁住"赋值触发重导航"的语义。
			HeadlessAppBootstrap.Run(delegate
			{
				var webView = new Wv2();
				int navigations = 0;
				webView.CoreWebView2.NavigationCompleted += delegate
				{
					navigations++;
				};
				webView.NavigateToString("<html><body><p>theme doc</p></body></html>");
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Dispatcher.UIThread.RunJobs();
				int afterFirst = navigations;
				Assert.True(afterFirst >= 1);

				webView.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Dispatcher.UIThread.RunJobs();

				Assert.True(navigations > afterFirst, "切换 PreferredColorScheme 应触发重导航/重渲染");
			});
		}

		private static T Find<T>(Control root) where T : Control
		{
			return FindCore<T>(root);
		}

		private static T FindCore<T>(Control c) where T : Control
		{
			if (c is T match)
			{
				return match;
			}
			if (c is Decorator { Child: { } child })
			{
				return FindCore<T>(child);
			}
			if (c is ContentControl { Content: Control content })
			{
				return FindCore<T>(content);
			}
			if (c is ScrollViewer { Content: Control sc })
			{
				return FindCore<T>(sc);
			}
			if (c is Panel panel)
			{
				foreach (Control k in panel.Children)
				{
					T r = FindCore<T>(k);
					if (r != null)
					{
						return r;
					}
				}
			}
			return null;
		}
	}
}
