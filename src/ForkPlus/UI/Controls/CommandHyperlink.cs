// 阶段 4.5：WPF System.Windows.* → Avalonia.*。
// WPF Hyperlink(Run) 构造 → Avalonia Hyperlink() + Inlines.Add(Run)。
// WPF MouseEnter/MouseLeave → Avalonia PointerEntered/PointerExited。
// WPF DispatcherTimer → Avalonia.Threading.DispatcherTimer。
// WPF Popup.StaysOpen/AllowsTransparency/PopupAnimation/PlacementRectangle → Avalonia 等价（见内联注释）。
// WPF Popup.IsMouseOver → 手动跟踪 _isPopupMouseOver（在 popup.Child 上订阅 Pointer 事件）。
// WPF Inline.Style → Avalonia Inline 不支持 Style 属性（TODO 待容器层处理）。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

		// 阶段 4.5：缓存 popup 鼠标悬停状态（替代 WPF Popup.IsMouseOver）。
		private bool _isPopupMouseOver;

		[Null]
		private Popup _popup;

		private readonly Action _action;

		public CommandHyperlink(RepositoryUserControl repositoryUserControl, Sha sha, string text, Action action)
			: base()
		{
			_action = action;
			_repositoryUserControl = repositoryUserControl;
			_sha = sha;
			// 阶段 4.5：WPF base(new Run(text)) → Avalonia base() + Inlines.Add(new Run(text))。
			Inlines.Add(new Run(text));
			_showPopupTimer.Interval = TimeSpan.FromMilliseconds(500.0);
			_closePopupTimer.Interval = TimeSpan.FromMilliseconds(100.0);
			_showPopupTimer.Tick += _showPopupTimer_Tick;
			_closePopupTimer.Tick += _closePopupTimer_Tick;
			// TODO(4.5-g): Avalonia Inline 不支持 Style 属性（WPF TextElement 特性）。
			// BugtrackerHyperlinkStyle 需在容器控件层或 XAML 模板中应用。
			// base.Style = Application.Current.TryFindResource("BugtrackerHyperlinkStyle") as Style;
			base.Click += CommandHyperlink_Click;
			// 阶段 4.5：WPF MouseEnter → Avalonia PointerEntered。
			base.PointerEntered += delegate(object s, PointerEventArgs e)
			{
				e.Handled = true;
				_closePopupTimer.Stop();
				_showPopupTimer.Start();
			};
			// 阶段 4.5：WPF MouseLeave → Avalonia PointerExited。
			base.PointerExited += delegate(object s, PointerEventArgs e)
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
			// 阶段 4.5：WPF _popup.IsMouseOver → 手动跟踪 _isPopupMouseOver。
			if (_popup != null && _popup.IsOpen && (!_isPopupMouseOver || hardClose))
			{
				_popup.IsOpen = false;
				VisualTreeAttachmentHelper.TrySetPopupChild(_popup, null, GetType().Name + ".Popup");
				_popup = null;
			}
		}

		[Null]
		private Popup CreatePopup()
		{
			// TODO(4.5): 需运行时验证 Avalonia Inline.Parent 是否返回 TextBlock（WPF Inline.Parent 语义）。
			if (!(base.Parent is TextBlock placementTarget))
			{
				return null;
			}
			Popup obj = new Popup
			{
				HorizontalOffset = -10.0,
				VerticalOffset = -4.0,
				// 阶段 4.5：WPF StaysOpen=true → Avalonia IsLightDismissEnabled=false。
				IsLightDismissEnabled = false,
				// 阶段 4.5：WPF AllowsTransparency/PopupAnimation → Avalonia 无等价（移除）。
				PlacementTarget = placementTarget
			};
			// TODO(4.5): 需运行时验证 Avalonia Inline 无 ElementStart/ElementEnd/GetCharacterRect 等价 API。
			// 原 WPF 代码通过 base.ElementStart.GetCharacterRect / base.ElementEnd.GetCharacterRect
			// 计算 PlacementRectangle；Avalonia Popup 无 PlacementRectangle，需通过 HorizontalOffset/VerticalOffset 实现定位。
			// Rect placementRectangle = Rect.Union(base.ElementStart.GetCharacterRect(LogicalDirection.Forward), base.ElementEnd.GetCharacterRect(LogicalDirection.Backward));
			// placementRectangle.X += placementRectangle.Width / 2.0;
			// obj.PlacementRectangle = placementRectangle;
			TooltipRevisionDetailsUserControl tooltipRevisionDetailsUserControl = new TooltipRevisionDetailsUserControl(_repositoryUserControl, _sha);
			tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked = (EventHandler)Delegate.Combine(tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked, (EventHandler)delegate
			{
				ClosePopup(hardClose: true);
			});
			// 阶段 4.5：WPF popup.IsMouseOver → 手动跟踪 _isPopupMouseOver（在 popup.Child 上订阅 Pointer 事件）。
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
