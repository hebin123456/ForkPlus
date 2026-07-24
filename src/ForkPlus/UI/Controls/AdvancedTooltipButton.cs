using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF Button + MouseEnter/MouseLeave → Avalonia Button + PointerEntered/PointerExited。
	// WPF DispatcherTimer 在 Avalonia 中为 Avalonia.Threading.DispatcherTimer（同名命名空间差异）。
	// WPF Popup.StaysOpen=true → Avalonia Popup.IsLightDismissEnabled=false。
	// WPF Popup.IsMouseOver → 手动跟踪 _isPopupMouseOver（在 popup.Child 上订阅 Pointer 事件）。
	public class AdvancedTooltipButton : Button
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Sha _sha;

		private readonly DispatcherTimer _showPopupTimer = new DispatcherTimer();

		private readonly DispatcherTimer _closePopupTimer = new DispatcherTimer();

		private readonly Action _action;

		private bool _isPopupMouseOver;

		[Null]
		private Popup _popup;

		public AdvancedTooltipButton(RepositoryUserControl repositoryUserControl, Sha sha, Action action)
		{
			_repositoryUserControl = repositoryUserControl;
			_sha = sha;
			_action = action;
			_showPopupTimer.Interval = TimeSpan.FromMilliseconds(500.0);
			_closePopupTimer.Interval = TimeSpan.FromMilliseconds(100.0);
			_showPopupTimer.Tick += _showPopupTimer_Tick;
			_closePopupTimer.Tick += _closePopupTimer_Tick;
			base.Click += AdvancedTooltipButton_Click;
			base.PointerEntered += delegate(object s, PointerEventArgs e)
			{
				e.Handled = true;
				_closePopupTimer.Stop();
				_showPopupTimer.Start();
			};
			base.PointerExited += delegate(object s, PointerEventArgs e)
			{
				e.Handled = true;
				_showPopupTimer.Stop();
				_closePopupTimer.Start();
			};
		}

		private void AdvancedTooltipButton_Click(object sender, RoutedEventArgs e)
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
				_popup.IsOpen = true;
			}
		}

		private void ClosePopup(bool hardClose = false)
		{
			// 阶段 4.5：WPF popup.IsMouseOver → 手动跟踪 _isPopupMouseOver。
			if (_popup != null && _popup.IsOpen && (!_isPopupMouseOver || hardClose))
			{
				_popup.IsOpen = false;
				VisualTreeAttachmentHelper.TrySetPopupChild(_popup, null, GetType().Name + ".Popup");
				_popup = null;
			}
		}

		private Popup CreatePopup()
		{
			Popup obj = new Popup
			{
				HorizontalOffset = 0.0,
				VerticalOffset = -4.0,
				// 阶段 4.5：WPF StaysOpen=true → Avalonia IsLightDismissEnabled=false。
				IsLightDismissEnabled = false,
				PlacementTarget = this
			};
			TooltipRevisionDetailsUserControl tooltipRevisionDetailsUserControl = new TooltipRevisionDetailsUserControl(_repositoryUserControl, _sha);
			tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked = (EventHandler)Delegate.Combine(tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked, (EventHandler)delegate
			{
				ClosePopup(hardClose: true);
			});
			// 阶段 4.5：WPF popup.MouseLeave → 在 popup.Child 上订阅 PointerExited。
			tooltipRevisionDetailsUserControl.PointerEntered += delegate
			{
				_isPopupMouseOver = true;
				_closePopupTimer.Stop();
			};
			tooltipRevisionDetailsUserControl.PointerExited += delegate
			{
				_isPopupMouseOver = false;
				_closePopupTimer.Start();
			};
			VisualTreeAttachmentHelper.TrySetPopupChild(obj, tooltipRevisionDetailsUserControl, GetType().Name + ".Popup");
			return obj;
		}
	}
}
