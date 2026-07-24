using Avalonia;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class BindableGitPointView : GitPointView
	{
		// 阶段 4.5：WPF DependencyProperty.Register + FrameworkPropertyMetadata
		// → Avalonia StyledProperty.Register + OnPropertyChanged override。
		public static readonly StyledProperty<IGitPoint> GitPointProperty =
			AvaloniaProperty.Register<BindableGitPointView, IGitPoint>(nameof(GitPoint));

		public IGitPoint GitPoint
		{
			get => GetValue(GitPointProperty);
			set => SetValue(GitPointProperty, value);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == GitPointProperty)
			{
				base.Value = GitPoint;
			}
		}
	}
}
