using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public partial class RevisionDetailsWindow : CustomWindow
	{
		private readonly GitModule _gitModule;

		private bool _startUpFinished;

		public RevisionDetailsWindow()
		{
			base.ShowInTaskbar = true;
			// 修复链 23：删除 CenterScreen（多显示器下居中到主显示器而非主窗口所在屏；打开方用 ShowAtOwnerScreen）。
			InitializeComponent();
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				base.Title = PreferencesLocalization.Current("Revision Details");
			}
		}

		public RevisionDetailsWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule, RevisionDiffTarget target, [Null] string fileToSelect)
			: this()
		{
			RevisionDetailsWindow revisionDetailsWindow = this;
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				return;
			}
			_gitModule = gitModule;
			RevisionDetails.Initialize(repositoryUserControl, RevisionDetailsUserControlMode.DetachedWindow);
			RevisionDetails.Loaded += delegate
			{
				revisionDetailsWindow.RevisionDetails.ShowRevisionDetails(target, fileToSelect);
			};
			RevisionDetails.RevisionDetailsUpdated += delegate(object s, RevisionDetails e)
			{
				revisionDetailsWindow.RefreshTitle(e);
			};
			base.SizeChanged += Window_SizeChanged;
			base.Activated += Window_Activated;
		}

		// Migration note（根因）：WPF OnSourceInitialized 在 Avalonia 无对应生命周期（原为死代码，
		// 窗口位置从未恢复）。改 OnOpened override（CustomWindow 已提供 OnOpened 虚链）。
		protected override void OnOpened(EventArgs e)
		{
			base.OnSourceInitialized(e);
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				return;
			}
			this.SetWindowLocationState(ForkPlusSettings.Default.RevisionWindowLocationState);
		}

		// Migration note（根因）：原为非 override 的隐藏方法，CustomWindow.PositionChanged 的
		// 虚分发永远到不了这里（移动窗口保存位置从未执行）。改 override 接入分发链。
		protected override void OnLocationChanged(EventArgs e)
		{
			base.OnLocationChanged(e);
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.RevisionWindowLocationState = this.GetWindowLocationState();
			}
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.RevisionWindowLocationState = this.GetWindowLocationState();
			}
		}

		private void Window_Activated(object sender, EventArgs e)
		{
			if (!_startUpFinished)
			{
				_startUpFinished = true;
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		private void RefreshTitle(RevisionDetails revisionDetails)
		{
			revisionDetails.MessageParts(out var subject, out var _);
			base.Title = revisionDetails.Sha.ToAbbreviatedString() + " " + subject;
		}

	}
}
