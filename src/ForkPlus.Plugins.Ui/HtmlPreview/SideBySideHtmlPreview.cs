// 并排双 WebView HTML 预览控件（HTML 预览共享组件的一部分）。
//
// 左=旧 / 右=新，两侧各自独立渲染一份自包含 HTML 文档；调用方（插件）负责把容器内的
// HTML 与资源组装成自包含文档（图片 / 样式可经 HtmlResourceInliner 内联为 data URI）。
// WebView 不可用时两侧显示提示文案；某侧文档为 null 时显示「不存在」占位。
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NativeWebView = Avalonia.Controls.NativeWebView;
using WebViewEnvironmentRequestedEventArgs = Avalonia.Controls.WebViewEnvironmentRequestedEventArgs;
using WebViewNavigationCompletedEventArgs = Avalonia.Controls.WebViewNavigationCompletedEventArgs;

namespace ForkPlus.Plugins.Ui.HtmlPreview
{
	/// <summary>并排双 WebView HTML 预览控件（左=旧 / 右=新）。</summary>
	public sealed class SideBySideHtmlPreview : UserControl
	{
		private readonly HtmlPane _left = new("旧");
		private readonly HtmlPane _right = new("新");

		public SideBySideHtmlPreview()
		{
			var grid = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("*,*"),
			};
			Grid.SetColumn(_left, 0);
			Grid.SetColumn(_right, 1);
			grid.Children.Add(_left);
			grid.Children.Add(_right);
			Content = grid;
		}

		/// <summary>某侧内容为 null（该侧不存在 / 无默认页）时的占位文案；调用方可按界面语言设置。</summary>
		public string AbsentText { get; set; } = "不存在";

		/// <summary>WebView 不可用时的提示文案（探测到的原因以 "(…)" 追加）；调用方可按界面语言设置。</summary>
		public string UnavailableText { get; set; } = "当前环境不支持渲染预览";

		/// <summary>设置两侧内容。<paramref name="leftHtml"/> / <paramref name="rightHtml"/>
		/// 为 null 表示该侧不存在（显示占位）。</summary>
		public void SetContent(string leftTitle, string leftHtml, string rightTitle, string rightHtml)
		{
			_left.AbsentText = AbsentText;
			_left.UnavailableText = UnavailableText;
			_right.AbsentText = AbsentText;
			_right.UnavailableText = UnavailableText;
			_left.SetPane(leftTitle, leftHtml);
			_right.SetPane(rightTitle, rightHtml);
		}

		/// <summary>单侧预览面板：标题 + 内容（WebView 或占位）。</summary>
		private sealed class HtmlPane : Grid
		{
			private readonly TextBlock _title;
			private readonly Border _host;
			private NativeWebView _view;
			private string _pendingHtml;
			private bool _ready;

			public HtmlPane(string defaultTitle)
			{
				RowDefinitions = new RowDefinitions("Auto,*");
				_title = new TextBlock
				{
					Text = defaultTitle,
					Margin = new Thickness(8, 6, 8, 6),
					FontWeight = FontWeight.SemiBold,
					TextTrimming = TextTrimming.CharacterEllipsis,
				};
				_host = new Border { Background = SurfaceBrush() };
				SetRow(_title, 0);
				SetRow(_host, 1);
				Children.Add(_title);
				Children.Add(_host);
			}

			/// <summary>该侧不存在 / 无默认页时的占位文案。</summary>
			public string AbsentText { get; set; } = "不存在";

			/// <summary>WebView 不可用时的提示文案（原因以 "(…)" 追加）。</summary>
			public string UnavailableText { get; set; } = "当前环境不支持渲染预览";

			public void SetPane(string title, string html)
			{
				_title.Text = string.IsNullOrEmpty(title) ? string.Empty : title;
				if (html == null)
				{
					_host.Child = Placeholder(string.IsNullOrEmpty(AbsentText) ? "不存在" : AbsentText);
					return;
				}
				if (!HtmlWebViewSupport.IsAvailable)
				{
					string reason = HtmlWebViewSupport.UnavailableReason;
					string baseText = string.IsNullOrEmpty(UnavailableText) ? "当前环境不支持渲染预览" : UnavailableText;
					string text = (string.IsNullOrEmpty(reason) || reason == "headless")
						? baseText
						: baseText + " (" + reason + ")";
					_host.Child = Placeholder(text);
					return;
				}
				if (_view == null)
				{
					CreateView();
				}
				if (_ready)
				{
					Navigate(html);
				}
				else
				{
					// 适配器尚未就绪（控件刚挂树）：暂存，AdapterCreated 后再导航
					_pendingHtml = html;
				}
			}

			private void CreateView()
			{
				_view = new NativeWebView { Background = Brushes.Transparent };
				_view.EnvironmentRequested += delegate (object sender, WebViewEnvironmentRequestedEventArgs e)
				{
					HtmlWebViewSupport.ConfigureEnvironment(e);
				};
				_view.AdapterCreated += delegate
				{
					_ready = true;
					if (_pendingHtml != null)
					{
						string pending = _pendingHtml;
						_pendingHtml = null;
						Navigate(pending);
					}
				};
				_view.NavigationCompleted += delegate (object sender, WebViewNavigationCompletedEventArgs e)
				{
					// 抑制右键菜单（与宿主 WebView2Stub 同口径）；失败不影响渲染
					_ = TryInvokeAsync("document.addEventListener('contextmenu',function(e){e.preventDefault();});");
				};
				_host.Child = _view;
			}

			private void Navigate(string html)
			{
				try
				{
					_view.NavigateToString(html, new Uri("about:blank"));
				}
				catch
				{
					// 导航失败（适配器中途销毁等）不炸 UI
				}
			}

			private async Task TryInvokeAsync(string script)
			{
				try
				{
					if (_view != null)
					{
						await _view.InvokeScript(script);
					}
				}
				catch
				{
					// 脚本注入失败无副作用
				}
			}

			private static Control Placeholder(string text)
			{
				return new TextBlock
				{
					Text = text,
					Margin = new Thickness(12),
					TextWrapping = TextWrapping.Wrap,
					VerticalAlignment = VerticalAlignment.Center,
					HorizontalAlignment = HorizontalAlignment.Center,
					Opacity = 0.7,
				};
			}
		}

		/// <summary>按当前应用明暗铺底色：原生引擎首帧前的透明表面会呈现为黑色，
		/// 铺底色后加载期与正文底色一致。</summary>
		private static IBrush SurfaceBrush()
		{
			bool dark = false;
			try
			{
				dark = Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
			}
			catch
			{
				dark = false;
			}
			return new SolidColorBrush(dark
				? Color.FromRgb(0x28, 0x28, 0x28)
				: Color.FromRgb(0xFA, 0xFA, 0xFA));
		}
	}
}
