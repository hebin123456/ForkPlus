using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	// WS4 仓库健康仪表盘窗口：ForkPlusDialogWindow 外壳（Close 按钮），
	// 内容区为 RepositoryHealthUserControl。与 RepositoryStatisticsWindow 同款布局：
	// 标题 Dock（固定标题 + 仓库名 AutoTooltipTextBlock）+ TouchpadAwareScrollViewer。
	// 窗口持有 RepositoryUserControl 引用（健康检查需要 JobQueue / RepositoryData / AddUndoable）。
	public partial class RepositoryHealthWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		public RepositoryHealthWindow(RepositoryUserControl repositoryUserControl)
		{
			_repositoryUserControl = repositoryUserControl;
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.ShowCancelButton = false;
			base.SubmitButtonTitle = PreferencesLocalization.Current("Close");
			ResizeMode = ResizeMode.CanResizeWithGrip;
			RepositoryNameTextBlock.Text = repositoryUserControl.GitModule?.Path;
			base.Loaded += RepositoryHealthWindow_Loaded;
		}

		private void RepositoryHealthWindow_Loaded(object sender, RoutedEventArgs e)
		{
			RepositoryHealthUserControl.ShowHealth(_repositoryUserControl);
		}
	}
}
