using Avalonia;
using ForkPlus.Git;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class BindableGitPointView : GitPointView
	{
		public static readonly global::Avalonia.StyledProperty<IGitPoint> GitPointProperty =
    global::Avalonia.AvaloniaProperty.Register<BindableGitPointView, IGitPoint>("GitPoint", null);

		public IGitPoint GitPoint
		{
			get
			{
				return (IGitPoint)GetValue(GitPointProperty);
			}
			set
			{
				SetValue(GitPointProperty, value);
			}
		}

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == GitPointProperty)
			{
				IGitPoint value = (IGitPoint)e.NewValue;
				base.Value = value;
			}
		}
	}
}
