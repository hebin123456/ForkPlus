using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.Helpers
{
	public static class ScrollViewerHelper
	{
		public static ScrollViewer FindScrollViewer(Control control)
		{
			if (control == null)
			{
				return null;
			}
			return control.GetVisualDescendants().OfType<TouchpadAwareScrollViewer>().FirstOrDefault()
				?? control.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		}
	}
}
