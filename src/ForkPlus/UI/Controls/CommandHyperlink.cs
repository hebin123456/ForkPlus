using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ForkPlus.UI.Controls
{
	public class CommandHyperlink : global::Avalonia.Controls.HyperlinkButton
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Sha _sha;

		private readonly DispatcherTimer _showPopupTimer = new DispatcherTimer();

		private readonly DispatcherTimer _closePopupTimer = new DispatcherTimer();

		[Null]
		private Popup _popup;

		private readonly Action _action;

		public CommandHyperlink(RepositoryUserControl repositoryUserControl, Sha sha, string text, Action action)
		{
			// Migration note：WPF Hyperlink(Run) 内联元素；Avalonia HyperlinkButton 无 (Run) 构造，
			// 改为 Content = 文本（由 BugtrackerHyperlinkStyle 提供超链外观）。
			Content = text;
			_action = action;
			_repositoryUserControl = repositoryUserControl;
			_sha = sha;
			_showPopupTimer.Interval = TimeSpan.FromMilliseconds(500.0);
			_closePopupTimer.Interval = TimeSpan.FromMilliseconds(100.0);
			_showPopupTimer.Tick += _showPopupTimer_Tick;
			_closePopupTimer.Tick += _closePopupTimer_Tick;
			// Migration note：WPF `Style = ... as Style`；资源实为 ControlTheme，经 StyleCompat 挂 Theme（base 不能作参数）。
			global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(this, Application.Current.TryFindResource("BugtrackerHyperlinkStyle"));
			base.Click += CommandHyperlink_Click;
			base.PointerEntered += delegate(object s, global::Avalonia.Input.PointerEventArgs e)
			{
				e.Handled = true;
				_closePopupTimer.Stop();
				_showPopupTimer.Start();
			};
			base.PointerExited += delegate(object s, global::Avalonia.Input.PointerEventArgs e)
			{
				e.Handled = true;
				_showPopupTimer.Stop();
				_closePopupTimer.Start();
			};
		}

		private void CommandHyperlink_Click(object sender, RoutedEventArgs e)
		{
			ClosePopup(hardClose: true);
			_showPopupTimer.Stop();
			_action();
		}

		private void _showPopupTimer_Tick(object sender, EventArgs e)
		{
			ShowPopup();
			_showPopupTimer.Stop();
		}

		private void _closePopupTimer_Tick(object sender, EventArgs e)
		{
			ClosePopup();
			_closePopupTimer.Stop();
		}

		private void ShowPopup()
		{
			if (_popup == null || !_popup.IsOpen)
			{
				_popup = CreatePopup();
				if (_popup != null)
				{
					_popup.IsOpen = true;
				}
			}
		}

		private void ClosePopup(bool hardClose = false)
		{
			if (_popup != null && _popup.IsOpen && (!_popup.IsPointerOver|| hardClose))
			{
				_popup.IsOpen = false;
				VisualTreeAttachmentHelper.TrySetPopupChild(_popup, null, GetType().Name + ".Popup");
				_popup = null;
			}
		}

		[Null]
		private Popup CreatePopup()
		{
			if (!(base.Parent is TextBlock placementTarget))
			{
				return null;
			}
			// Migration note：WPF Popup 的 StaysOpen/AllowsTransparency/PopupAnimation(Fade) 在 Avalonia 无对应属性：
			// StaysOpen=true 近似为 IsLightDismissEnabled=false（不因点击外部自动关闭，关闭仍由定时器/点击逻辑控制）；
			// AllowsTransparency / PopupAnimation(Fade) 的透明与淡入动画暂不可用。
			Popup obj = new Popup
			{
				HorizontalOffset = -10.0,
				VerticalOffset = -4.0,
				IsLightDismissEnabled = false,
				PlacementTarget = placementTarget
			};
			// Migration note：WPF 用 Hyperlink 的 ElementStart/ElementEnd GetCharacterRect 求内联文本在 TextBlock 内的矩形；
			// Avalonia 中本控件是 HyperlinkButton（包在 InlineUIContainer 里），改用 TranslatePoint 把自身 Bounds 映射到
			// placementTarget(TextBlock) 坐标系，再水平平移半宽以复刻 WPF 的 placementRectangle.X += Width/2 定位。
			Point? topLeft = this.TranslatePoint(new Point(0.0, 0.0), placementTarget); // Migration note：扩展方法需显式 this 接收者
			Rect placementRectangle = new Rect(topLeft ?? new Point(0.0, 0.0), Bounds.Size);
			placementRectangle = placementRectangle.WithX(placementRectangle.X + placementRectangle.Width / 2.0);
			obj.PlacementRect = placementRectangle;
			TooltipRevisionDetailsUserControl tooltipRevisionDetailsUserControl = new TooltipRevisionDetailsUserControl(_repositoryUserControl, _sha);
			tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked = (EventHandler)Delegate.Combine(tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked, (EventHandler)delegate
			{
				ClosePopup(hardClose: true);
			});
			VisualTreeAttachmentHelper.TrySetPopupChild(obj, tooltipRevisionDetailsUserControl, GetType().Name + ".Popup");
			obj.PointerExited += delegate
			{
				_closePopupTimer.Start();
			};
			return obj;
		}
	}
}
