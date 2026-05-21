using System.Windows;

namespace ForkPlus.UI.Controls
{
	public class RepositoryManagerEditableTextBlock : EditableTextBlock
	{
		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
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
						base.Value = newString;
					}
					base.IsInEditMode = false;
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
