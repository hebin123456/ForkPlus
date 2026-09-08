using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class ContentContainer : Grid
	{
		private global::Avalonia.Controls.Control _childControl; // Migration note：WPF UIElement → Avalonia Control（Children.Remove/TryAddChild 需 Control）

		public void ShowControl(global::Avalonia.Controls.Control control)
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
