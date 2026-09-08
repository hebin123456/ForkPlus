using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Accounts.AiServices;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// v3.9.0：统一的 AI 操作按钮。封装统一样式、Loading 态（⏳前缀+tooltip切换）、
	/// AI 配置检测（未配置时自动隐藏）。所有嵌入式 AI 按钮应使用此控件。
	/// </summary>
	public class AiActionButton : Button
	{
		public static readonly global::Avalonia.StyledProperty<string> ActionVerbProperty =
    global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<AiActionButton, string>("ActionVerb", null, (owner, e) => OnActionVerbChanged(owner, e));

		private string _savedToolTip;
		private bool _isBusy;

		/// <summary>Migration note：WPF Control.ToolTip 属性 → Avalonia ToolTip.SetTip/GetTip 附加属性转发。</summary>
		private object ToolTip
		{
			get => global::Avalonia.Controls.ToolTip.GetTip(this);
			set => global::Avalonia.Controls.ToolTip.SetTip(this, value);
		}

		/// <summary>动作动词，显示为 "🤖 AI {verb}"。为空时显示 "🤖 AI"。</summary>
		public string ActionVerb
		{
			get => (string)GetValue(ActionVerbProperty);
			set => SetValue(ActionVerbProperty, value);
		}

		public AiActionButton()
		{
			Padding = new Thickness(8, 2, 8, 2);
			Height = 22;
			FontSize = 12;
			VerticalAlignment = VerticalAlignment.Center;
			UpdateContent();
			Loaded += AiActionButton_Loaded;
		}

		private void AiActionButton_Loaded(object sender, RoutedEventArgs e)
		{
			RefreshVisibility();
		}

		/// <summary>根据 AI 配置状态刷新按钮可见性。</summary>
		public void RefreshVisibility()
		{
			IsVisible = OpenAiService.IsAiReviewConfigured() ? true : false;
		}

		/// <summary>设置 Loading 状态：禁用按钮、切换 tooltip、内容加 ⏳ 前缀。</summary>
		public void SetBusy(bool busy, string busyToolTip = null)
		{
			if (busy)
			{
				_savedToolTip = ToolTip?.ToString();
				ToolTip = busyToolTip;
				IsEnabled = false;
			}
			else
			{
				ToolTip = busyToolTip ?? _savedToolTip;
				_savedToolTip = null;
				IsEnabled = true;
			}
			_isBusy = busy;
			UpdateContent();
		}

		private void UpdateContent()
		{
			string prefix = _isBusy ? "⏳" : "🤖";
			string verb = ActionVerb;
			string text = string.IsNullOrWhiteSpace(verb) ? prefix + " AI" : prefix + " AI " + verb;
			Content = new TextBlock
			{
				Text = text,
				FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji")
			};
		}

		private static void OnActionVerbChanged(global::Avalonia.AvaloniaObject d, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			((AiActionButton)d).UpdateContent();
		}
	}
}
