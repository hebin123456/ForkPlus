using System.Windows;
using System.Windows.Controls;

namespace ForkPlus.UI.Controls
{
	public class ContentContainer : Grid
	{
		private UIElement _childControl;

		public void ShowControl(UIElement control)
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
