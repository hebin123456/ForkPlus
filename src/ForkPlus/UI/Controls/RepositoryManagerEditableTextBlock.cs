using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	public class RepositoryManagerEditableTextBlock : EditableTextBlock
	{
		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property != EditableTextBlock.IsInEditModeProperty)
			{
				return;
			}
			if ((bool)e.NewValue)
			{
				ShowEditor(base.Value, delegate(bool success, string newString)
				{
					if (success)
					{
						if (DataContext is RepositoryManagerRepositoryItem repositoryItem)
						{
							repositoryItem.Name = newString;
						}
						else
						{
							SetCurrentValue(EditableTextBlock.ValueProperty, newString);
						}
					}
					if (DataContext is RepositoryManagerTreeViewItem treeViewItem)
					{
						treeViewItem.IsInEditMode = false;
					}
				});
			}
			else
			{
				HideEditor();
				Focus();
			}
		}
	}
}
