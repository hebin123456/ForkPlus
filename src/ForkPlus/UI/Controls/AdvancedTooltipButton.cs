using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	public class AdvancedTooltipButton : Button
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Sha _sha;

		private readonly DispatcherTimer _showPopupTimer = new DispatcherTimer();

		private readonly DispatcherTimer _closePopupTimer = new DispatcherTimer();

		private readonly Action _action;

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
			base.MouseEnter += delegate(object s, MouseEventArgs e)
			{
				e.Handled = true;
				_closePopupTimer.Stop();
				_showPopupTimer.Start();
			};
			base.MouseLeave += delegate(object s, MouseEventArgs e)
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
			if (_popup != null && _popup.IsOpen && (!_popup.IsMouseOver || hardClose))
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
				StaysOpen = true,
				AllowsTransparency = true,
				PopupAnimation = PopupAnimation.Fade,
				PlacementTarget = this
			};
			TooltipRevisionDetailsUserControl tooltipRevisionDetailsUserControl = new TooltipRevisionDetailsUserControl(_repositoryUserControl, _sha);
			tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked = (EventHandler)Delegate.Combine(tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked, (EventHandler)delegate
			{
				ClosePopup(hardClose: true);
			});
			VisualTreeAttachmentHelper.TrySetPopupChild(obj, tooltipRevisionDetailsUserControl, GetType().Name + ".Popup");
			obj.MouseLeave += delegate
			{
				_closePopupTimer.Start();
			};
			return obj;
		}
	}
}
