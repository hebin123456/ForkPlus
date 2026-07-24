// 阶段 4.5：WPF System.Windows.* → Avalonia.*。WPF Grid → Avalonia.Controls.Grid。WPF UIElement → Avalonia.Controls.Control。
// WPF Panel.Children(UIElementCollection) → Avalonia Panel.Children(Controls 集合)，Remove/Add API 兼容。
using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.UI.Controls
{
	public class ContentContainer : Grid
	{
		private Control _childControl;

		public void ShowControl(Control control)
		{
			base.Children.Remove(_childControl);
			if (!VisualTreeAttachmentHelper.TryAddChild(this, control, GetType().Name + ".ShowControl"))
			{
				_childControl = null;
				return;
			}
			_childControl = control;
		}

		public void ShowContent()
		{
			if (_childControl != null)
			{
				base.Children.Remove(_childControl);
				_childControl = null;
			}
		}
	}
}
