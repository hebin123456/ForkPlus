using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Dialogs
{
	public partial class RepositoryStatisticsWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		public RepositoryStatisticsWindow(GitModule gitModule)
		{
			_gitModule = gitModule;
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.ShowCancelButton = false;
			base.SubmitButtonTitle = "Close";
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			RepositoryNameTextBlock.Text = gitModule.Path;
			base.Loaded += RepositoryStatisticsWindow_Loaded;
		}

		private void RepositoryStatisticsWindow_Loaded(object sender, RoutedEventArgs e)
		{
			StatisticsUserControl.ShowStatistics(_gitModule);
		}

	}
}
