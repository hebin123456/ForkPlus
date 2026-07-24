// 阶段 4.5：WPF System.Windows.* → Avalonia.*。WPF DependencyPropertyChangedEventArgs → Avalonia.AvaloniaPropertyChangedEventArgs。
// WPF OnPropertyChanged(DependencyPropertyChangedEventArgs) → Avalonia OnPropertyChanged(AvaloniaPropertyChangedEventArgs)（e.Property/e.NewValue API 兼容）。
using Avalonia;

namespace ForkPlus.UI.Controls
{
	public class RepositoryManagerEditableTextBlock : EditableTextBlock
	{
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
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
