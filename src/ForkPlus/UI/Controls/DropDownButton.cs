using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	public class DropDownButton : ToggleButton
	{
		protected override void OnChecked(RoutedEventArgs e)
		{
			base.OnChecked(e);
			base.ContextMenu.PlacementTarget = this;
			base.ContextMenu.Placement = PlacementMode.Bottom;
			base.ContextMenu.Closed += ContextMenu_Closed;
			base.ContextMenu.Open();
			base.IsChecked = true;
		}

		protected override void OnUnchecked(RoutedEventArgs e)
		{
			base.OnUnchecked(e);
			base.ContextMenu.Closed -= ContextMenu_Closed;
			base.ContextMenu.Close();
		}

		private void ContextMenu_Closed(object sender, RoutedEventArgs e)
		{
			base.IsChecked = false;
		}
	}
}
