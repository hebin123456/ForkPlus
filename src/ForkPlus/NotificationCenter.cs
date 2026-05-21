using System;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;

namespace ForkPlus
{
	public sealed class NotificationCenter
	{
		private static readonly object _padlock = new object();

		private static NotificationCenter _current = null;

		public EventHandler MainWindowInitializedChanged;

		public static NotificationCenter Current
		{
			get
			{
				lock (_padlock)
				{
					if (_current == null)
					{
						_current = new NotificationCenter();
					}
					return _current;
				}
			}
		}

		public event EventHandler<EventArgs<ClosableTabItem>> ActiveTabChanged;

		public event EventHandler<RepositoryDataUpdatedEventArgs> RepositoryDataUpdated;

		public event EventHandler<EventArgs<RepositoryUserControl>> RepositoryStatusUpdated;

		public event EventHandler<EventArgs<string>> RepositoryNameChanged;

		public event EventHandler<EventArgs<RepositoryManager.Repository>> RepositoryColorChanged;

		public event EventHandler RepositoryManagerRepositoriesUpdated;

		public event EventHandler<EventArgs<RepositoryUserControl>> RepositoryUserControlTitleChanged;

		public event EventHandler<EventArgs<RepositoryUserControl>> RepositoryUserControlIsDirtyChanged;

		public event EventHandler<EventArgs<RepositoryUserControl>> RepositoryUserControlColorChanged;

		public event EventHandler<EventArgs<ThemeType>> ApplicationThemeChanged;

		public event EventHandler<EventArgs<RevisionListOrientation>> RevisionListOrientatioChanged;

		public event EventHandler ShellChanged;

		public event EventHandler<EventArgs<FileListMode>> FileListModeChanged;

		public event EventHandler<EventArgs<int>> DiffContextSizeChanged;

		public event EventHandler<EventArgs<bool>> DiffIgnoreWhitespacesChanged;

		public event EventHandler<EventArgs<bool>> DiffShowHiddenSymbolsChanged;

		public event EventHandler<EventArgs<bool>> DiffWordWrapChanged;

		public event EventHandler<EventArgs<double>> CodeEditorFontSizeChanged;

		public event EventHandler<EventArgs<bool>> DiffShowChangeMarksChanged;

		public event EventHandler<EventArgs<bool>> DiffShowEntireFileChanged;

		public event EventHandler<EventArgs<DiffLayoutMode>> DiffLayoutModeChanged;

		public event EventHandler<EventArgs<bool>> ImageDiffHighlightPixelsChanged;

		public event EventHandler<EventArgs<int>> UpdateRepoStatusAutomaticallyChanged;

		public event EventHandler<EventArgs<CommitSpellCheckingMode>> CommitSpellCheckingModeChanged;

		public event EventHandler<EventArgs<int>> PageGuideLinePositionChanged;

		public event EventHandler<EventArgs<int>> CommitSubjectLowLimitChanged;

		public event EventHandler<EventArgs<int>> CommitSubjectHighLimitChanged;

		public event EventHandler<EventArgs<bool>> PushAutomaticallyOnCommitChanged;

		public event EventHandler<EventArgs<bool>> CompactBranchLabelsChanged;

		public event EventHandler<EventArgs<bool>> DisableSyntaxHighlightingChanged;

		public event EventHandler ReferenceSortOrderChanged;

		public void RaiseMainWindowInitializedChanged(object sender)
		{
			MainWindowInitializedChanged?.Invoke(sender, EventArgs.Empty);
		}

		public void RaiseActiveTabChanged(object sender, ClosableTabItem newValue)
		{
			this.ActiveTabChanged?.Invoke(this, new EventArgs<ClosableTabItem>(newValue));
		}

		public void RaiseRepositoryDataUpdated(object sender, RepositoryUserControl repositoryUserControl, RepositoryData old, RepositoryData newValue)
		{
			this.RepositoryDataUpdated?.Invoke(this, new RepositoryDataUpdatedEventArgs(repositoryUserControl, old, newValue));
		}

		public void RaiseRepositoryStatusUpdated(object sender, RepositoryUserControl newValue)
		{
			this.RepositoryStatusUpdated?.Invoke(this, new EventArgs<RepositoryUserControl>(newValue));
		}

		public void RaiseRepositoryNameChanged(object sender, string path)
		{
			this.RepositoryNameChanged?.Invoke(this, new EventArgs<string>(path));
		}

		public void RaiseRepositoryColorChanged(object sender, RepositoryManager.Repository repository)
		{
			this.RepositoryColorChanged?.Invoke(this, new EventArgs<RepositoryManager.Repository>(repository));
		}

		public void RaiseRepositoryManagerRepositoriesUpdated(object sender)
		{
			this.RepositoryManagerRepositoriesUpdated?.Invoke(this, EventArgs.Empty);
		}

		public void RaiseRepositoryUserControlTitleChanged(object sender, RepositoryUserControl repositoryUserControl)
		{
			this.RepositoryUserControlTitleChanged?.Invoke(this, new EventArgs<RepositoryUserControl>(repositoryUserControl));
		}

		public void RaiseRepositoryUserControlIsDirtyChanged(object sender, RepositoryUserControl repositoryUserControl)
		{
			this.RepositoryUserControlIsDirtyChanged?.Invoke(this, new EventArgs<RepositoryUserControl>(repositoryUserControl));
		}

		public void RaiseRepositoryUserControlColorChanged(object sender, RepositoryUserControl repository)
		{
			this.RepositoryUserControlColorChanged?.Invoke(this, new EventArgs<RepositoryUserControl>(repository));
		}

		public void RaiseApplicationThemeChanged(object sender, ThemeType newValue)
		{
			this.ApplicationThemeChanged?.Invoke(this, new EventArgs<ThemeType>(newValue));
		}

		public void RaiseRevisionListOrientatioChanged(object sender, RevisionListOrientation newValue)
		{
			this.RevisionListOrientatioChanged?.Invoke(this, new EventArgs<RevisionListOrientation>(newValue));
		}

		public void RaiseShellChanged(object sender)
		{
			this.ShellChanged?.Invoke(this, EventArgs.Empty);
		}

		public void RaiseFileListModeChanged(object sender, FileListMode newValue)
		{
			this.FileListModeChanged?.Invoke(this, new EventArgs<FileListMode>(newValue));
		}

		public void RaiseDiffContextSizeChanged(object sender, int newValue)
		{
			this.DiffContextSizeChanged?.Invoke(this, new EventArgs<int>(newValue));
		}

		public void RaiseDiffIgnoreWhitespacesChanged(object sender, bool newValue)
		{
			this.DiffIgnoreWhitespacesChanged?.Invoke(this, new EventArgs<bool>(newValue));
		}

		public void RaiseDiffShowHiddenSymbolsChanged(object sender, bool newValue)
		{
			this.DiffShowHiddenSymbolsChanged?.Invoke(this, new EventArgs<bool>(newValue));
		}

		public void RaiseDiffWordWrapChanged(object sender, bool newValue)
		{
			this.DiffWordWrapChanged?.Invoke(this, new EventArgs<bool>(newValue));
		}

		public void RaiseCodeEditorFontSizeChanged(object sender, double newValue)
		{
			this.CodeEditorFontSizeChanged?.Invoke(this, new EventArgs<double>(newValue));
		}

		public void RaiseDiffShowChangeMarksChanged(object sender, bool newValue)
		{
			this.DiffShowChangeMarksChanged?.Invoke(this, new EventArgs<bool>(newValue));
		}

		public void RaiseDiffShowEntireFileChanged(object sender, bool newValue)
		{
			this.DiffShowEntireFileChanged?.Invoke(this, new EventArgs<bool>(newValue));
		}

		public void RaiseDiffLayoutModeChanged(object sender, DiffLayoutMode newValue)
		{
			this.DiffLayoutModeChanged?.Invoke(this, new EventArgs<DiffLayoutMode>(newValue));
		}

		public void RaiseImageDiffHighlightPixelsChanged(object sender, bool newValue)
		{
			this.ImageDiffHighlightPixelsChanged?.Invoke(this, new EventArgs<bool>(newValue));
		}

		public void RaiseUpdateRepoStatusAutomaticallyChanged(object sender, int newValue)
		{
			this.UpdateRepoStatusAutomaticallyChanged?.Invoke(this, new EventArgs<int>(newValue));
		}

		public void RaiseCommitSpellCheckingModeChanged(object sender, CommitSpellCheckingMode newValue)
		{
			this.CommitSpellCheckingModeChanged?.Invoke(this, new EventArgs<CommitSpellCheckingMode>(newValue));
		}

		public void RaisePageGuideLinePositionChanged(object sender, int newValue)
		{
			this.PageGuideLinePositionChanged?.Invoke(this, new EventArgs<int>(newValue));
		}

		public void RaiseCommitSubjectLowLimitChanged(object sender, int newValue)
		{
			this.CommitSubjectLowLimitChanged?.Invoke(this, new EventArgs<int>(newValue));
		}

		public void RaiseCommitSubjectHighLimitChanged(object sender, int newValue)
		{
			this.CommitSubjectHighLimitChanged?.Invoke(this, new EventArgs<int>(newValue));
		}

		public void RaisePushAutomaticallyOnCommitChanged(object sender, bool newValue)
		{
			this.PushAutomaticallyOnCommitChanged?.Invoke(this, new EventArgs<bool>(newValue));
		}

		public void RaiseCompactBranchLabelsChanged(object sender, bool newValue)
		{
			this.CompactBranchLabelsChanged?.Invoke(this, new EventArgs<bool>(newValue));
		}

		public void RaiseDisableSyntaxHighlightingChanged(object sender, bool newValue)
		{
			this.DisableSyntaxHighlightingChanged?.Invoke(this, new EventArgs<bool>(newValue));
		}

		public void RaiseReferenceSortOrderChanged(object sender)
		{
			this.ReferenceSortOrderChanged?.Invoke(this, new EventArgs());
		}
	}
}
