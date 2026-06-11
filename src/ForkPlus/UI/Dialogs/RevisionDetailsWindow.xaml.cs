using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Dialogs
{
	public partial class RevisionDetailsWindow : CustomWindow
	{
		private readonly GitModule _gitModule;

		private bool _startUpFinished;

		public RevisionDetailsWindow()
		{
			base.ShowInTaskbar = true;
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
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

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				return;
			}
			this.SetWindowLocationState(ForkPlusSettings.Default.RevisionWindowLocationState);
		}

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
