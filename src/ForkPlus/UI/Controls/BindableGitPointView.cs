using System.Windows;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class BindableGitPointView : GitPointView
	{
		public static readonly DependencyProperty GitPointProperty = DependencyProperty.Register("GitPoint", typeof(IGitPoint), typeof(BindableGitPointView), new FrameworkPropertyMetadata(null));

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

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
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
