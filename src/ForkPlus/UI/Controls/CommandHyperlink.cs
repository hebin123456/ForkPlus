using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	public class CommandHyperlink : Hyperlink
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Sha _sha;

		private readonly DispatcherTimer _showPopupTimer = new DispatcherTimer();

		private readonly DispatcherTimer _closePopupTimer = new DispatcherTimer();

		[Null]
		private Popup _popup;

		private readonly Action _action;

		public CommandHyperlink(RepositoryUserControl repositoryUserControl, Sha sha, string text, Action action)
			: base(new Run(text))
		{
			_action = action;
			_repositoryUserControl = repositoryUserControl;
			_sha = sha;
			_showPopupTimer.Interval = TimeSpan.FromMilliseconds(500.0);
			_closePopupTimer.Interval = TimeSpan.FromMilliseconds(100.0);
			_showPopupTimer.Tick += _showPopupTimer_Tick;
			_closePopupTimer.Tick += _closePopupTimer_Tick;
			base.Style = Application.Current.TryFindResource("BugtrackerHyperlinkStyle") as Style;
			base.Click += CommandHyperlink_Click;
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
			if (_popup != null && _popup.IsOpen && (!_popup.IsMouseOver || hardClose))
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
			Popup obj = new Popup
			{
				HorizontalOffset = -10.0,
				VerticalOffset = -4.0,
				StaysOpen = true,
				AllowsTransparency = true,
				PopupAnimation = PopupAnimation.Fade,
				PlacementTarget = placementTarget
			};
			Rect placementRectangle = Rect.Union(base.ElementStart.GetCharacterRect(LogicalDirection.Forward), base.ElementEnd.GetCharacterRect(LogicalDirection.Backward));
			placementRectangle.X += placementRectangle.Width / 2.0;
			obj.PlacementRectangle = placementRectangle;
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
