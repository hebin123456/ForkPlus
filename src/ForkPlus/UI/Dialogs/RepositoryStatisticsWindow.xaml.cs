using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class RepositoryStatisticsWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;
		[Null]
		private readonly string _initialRef;
		private readonly bool _scrollToCodeLines;

		public RepositoryStatisticsWindow(GitModule gitModule)
			: this(gitModule, null, false)
		{
		}

		/// <param name="initialRef">初始统计的 refSpec（分支/tag/sha）。null/空 = Workspace snapshot。</param>
		/// <param name="scrollToCodeLines">显示后是否滚动到代码行数统计区域。</param>
		public RepositoryStatisticsWindow(GitModule gitModule, [Null] string initialRef, bool scrollToCodeLines)
		{
			_gitModule = gitModule;
			_initialRef = initialRef;
			_scrollToCodeLines = scrollToCodeLines;
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.ShowCancelButton = false;
			base.SubmitButtonTitle = PreferencesLocalization.Current("Close");
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			RepositoryNameTextBlock.Text = gitModule.Path;
			base.Loaded += RepositoryStatisticsWindow_Loaded;
		}

		private void RepositoryStatisticsWindow_Loaded(object sender, RoutedEventArgs e)
		{
			StatisticsUserControl.ShowStatistics(_gitModule, _initialRef, _scrollToCodeLines);
		}

	}
}
