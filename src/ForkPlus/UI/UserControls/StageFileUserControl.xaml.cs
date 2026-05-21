using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public partial class StageFileUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		public EventHandler<FileListEventArgs> SelectionChanged;

		public EventHandler<EventArgs> StagedFilesItemSourceChanged;

		public EventHandler<EventArgs> ShowDiffPopup;

		private static readonly string StageAllIconName;

		private static readonly string UnstageAllIconName;

		private bool _stopHandlingSelectionEvents;

		private readonly DelayedAction<string> _refreshFilterAction;

		private const int LargeFileListAutoSelectionThreshold = 5000;

		public ChangedFile[] AllUnstagedFiles => UnstagedFilesFileListUserControl.Items;

		public ChangedFile[] AllStagedFiles => StagedFilesFileListUserControl.Items;

		public ChangedFile[] ExpandedUnstagedFiles => UnstagedFilesFileListUserControl.ExpandedItems;

		public ChangedFile[] ExpandedStagedFiles => StagedFilesFileListUserControl.ExpandedItems;

		public ChangedFile[] ExpandedSelectedUnstagedFiles => UnstagedFilesFileListUserControl.ExpandedSelectedItems;

		public ChangedFile[] ExpandedSelectedStagedFiles => StagedFilesFileListUserControl.ExpandedSelectedItems;

		public ChangedFile[] SelectedUnstagedFiles => UnstagedFilesFileListUserControl.SelectedItems;

		public ChangedFile[] SelectedStagedFiles => StagedFilesFileListUserControl.SelectedItems;

		public int StagedItemsCount { get; private set; }

		public bool ContainsUnmergedItems { get; private set; }

		public FileListMode FileListsMode
		{
			get
			{
				return UnstagedFilesFileListUserControl.Mode;
			}
			set
			{
				UnstagedFilesFileListUserControl.Mode = value;
				StagedFilesFileListUserControl.Mode = value;
				UnstagedFilesFileListUserControl.Refresh();
				StagedFilesFileListUserControl.Refresh();
			}
		}

		public bool Enabled
		{
			get
			{
				if (UnstagedFilesFileListUserControl.IsEnabled)
				{
					return StagedFilesFileListUserControl.IsEnabled;
				}
				return false;
			}
			set
			{
				UnstagedFilesFileListUserControl.IsEnabled = value;
				StagedFilesFileListUserControl.IsEnabled = value;
				RefreshStageAllButton();
				RefreshStageButtons();
			}
		}

		public bool IsStagedListSelected => StagedFilesFileListUserControl.TreeView.SelectedItems.Count > 0;

		public bool IsUnstagedListSelected => UnstagedFilesFileListUserControl.TreeView.SelectedItems.Count > 0;

		public bool IsFiltered => string.IsNullOrEmpty(FilterTextBox.FilterRequest);

		public event EventHandler Unstage;

		public event EventHandler Stage;

		public event EventHandler StageAll;

		public event EventHandler UnstageAll;

		public event EventHandler<ContextMenu> UnstagedFilesContextMenuOpening;

		public event EventHandler<ContextMenu> StagedFilesContextMenuOpening;

		public event EventHandler<ContextMenu> FileListSettingsMenuOpened;

		static StageFileUserControl()
		{
			StageAllIconName = "StageAllIcon";
			UnstageAllIconName = "UnstageAllIcon";
			KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(StageFileUserControl), new FrameworkPropertyMetadata(KeyboardNavigationMode.Local));
		}

		public StageFileUserControl()

		{
			InitializeComponent();
			ApplyLocalization();
			_refreshFilterAction = new DelayedAction<string>(UpdateFilter, 0.1);
			UnstagedFilesFileListUserControl.TreeView.AllowDragDrop = true;
			FileListUserControl unstagedFilesFileListUserControl = UnstagedFilesFileListUserControl;
			unstagedFilesFileListUserControl.SelectionChanged = (EventHandler<FileListEventArgs>)Delegate.Combine(unstagedFilesFileListUserControl.SelectionChanged, new EventHandler<FileListEventArgs>(UnstageFilesFileListUserControl_SelectionChanged));
			FileListUserControl unstagedFilesFileListUserControl2 = UnstagedFilesFileListUserControl;
			unstagedFilesFileListUserControl2.ItemDoubleClick = (EventHandler<FileListEventArgs>)Delegate.Combine(unstagedFilesFileListUserControl2.ItemDoubleClick, new EventHandler<FileListEventArgs>(UnstageFilesFileListUserControl_ItemDoubleClick));
			FileListUserControl unstagedFilesFileListUserControl3 = UnstagedFilesFileListUserControl;
			unstagedFilesFileListUserControl3.ItemsDrop = (EventHandler<FileListTreeView.DropEventArgs>)Delegate.Combine(unstagedFilesFileListUserControl3.ItemsDrop, new EventHandler<FileListTreeView.DropEventArgs>(UnstageFilesFileListUserControl_ItemsDrop));
			StagedFilesFileListUserControl.TreeView.AllowDragDrop = true;
			FileListUserControl stagedFilesFileListUserControl = StagedFilesFileListUserControl;
			stagedFilesFileListUserControl.SelectionChanged = (EventHandler<FileListEventArgs>)Delegate.Combine(stagedFilesFileListUserControl.SelectionChanged, new EventHandler<FileListEventArgs>(StageFilesFileListUserControl_SelectionChanged));
			FileListUserControl stagedFilesFileListUserControl2 = StagedFilesFileListUserControl;
			stagedFilesFileListUserControl2.ItemDoubleClick = (EventHandler<FileListEventArgs>)Delegate.Combine(stagedFilesFileListUserControl2.ItemDoubleClick, new EventHandler<FileListEventArgs>(StageFilesFileListUserControl_ItemDoubleClick));
			FileListUserControl stagedFilesFileListUserControl3 = StagedFilesFileListUserControl;
			stagedFilesFileListUserControl3.ItemsDrop = (EventHandler<FileListTreeView.DropEventArgs>)Delegate.Combine(stagedFilesFileListUserControl3.ItemsDrop, new EventHandler<FileListTreeView.DropEventArgs>(StageFilesFileListUserControl_ItemsDrop));
			RefreshFileListMode();
			WeakEventManager<NotificationCenter, EventArgs<FileListMode>>.AddHandler(NotificationCenter.Current, "FileListModeChanged", delegate
			{
				RefreshFileListMode();
			});
			StageButton.Click += StageButton_Click;
			UnstageButton.Click += UnstageButton_Click;
			base.PreviewKeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.F && Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.LeftShift))
				{
					FilterTextBox.ShowWithAnimation();
					e.Handled = true;
				}
				else if (e.Key == Key.Escape)
				{
					FilterTextBox.HideWithAnimation();
				}
			};
			UnstagedFilesFileListUserControl.PreviewKeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Space && !Keyboard.IsKeyDown(Key.LeftCtrl))
				{
					e.Handled = true;
					ShowDiffPopup?.Invoke(this, EventArgs.Empty);
				}
			};
			StagedFilesFileListUserControl.PreviewKeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Space && !Keyboard.IsKeyDown(Key.LeftCtrl))
				{
					e.Handled = true;
					ShowDiffPopup?.Invoke(this, EventArgs.Empty);
				}
			};
			FilterTextBox.FilterRequestChanged += delegate
			{
				_refreshFilterAction.InvokeWithDelay(FilterTextBox.FilterRequest);
			};
		}

		public void SetData(ChangedFile[] unstagedFiles, ChangedFile[] stagedFiles, bool selectFirstAvailableFile = true)
		{
			selectFirstAvailableFile = selectFirstAvailableFile && unstagedFiles.Length + stagedFiles.Length < LargeFileListAutoSelectionThreshold;
			UnstagedFilesFileListUserControl.SetItemSource(unstagedFiles, forceRefresh: false, unstagedFiles.Length != 0);
			StagedFilesFileListUserControl.SetItemSource(stagedFiles, forceRefresh: false, stagedFiles.Length != 0);
			ContainsUnmergedItems = unstagedFiles.Any((ChangedFile x) => x.ChangeType == ChangeType.Unmerged);
			StagedItemsCount = stagedFiles.Length;
			StagedFilesItemSourceChanged?.Invoke(this, EventArgs.Empty);
			if (selectFirstAvailableFile)
			{
				RefreshSelection();
			}
			RefreshStageAllButton();
			RefreshStageButtons();
		}

		public async Task SetDataAsync(ChangedFile[] unstagedFiles, ChangedFile[] stagedFiles, bool selectFirstAvailableFile = true)
		{
			selectFirstAvailableFile = selectFirstAvailableFile && unstagedFiles.Length + stagedFiles.Length < LargeFileListAutoSelectionThreshold;
			await UnstagedFilesFileListUserControl.SetItemSourceAsync(unstagedFiles, forceRefresh: false, unstagedFiles.Length != 0);
			await StagedFilesFileListUserControl.SetItemSourceAsync(stagedFiles, forceRefresh: false, stagedFiles.Length != 0);
			ContainsUnmergedItems = unstagedFiles.Any((ChangedFile x) => x.ChangeType == ChangeType.Unmerged);
			StagedItemsCount = stagedFiles.Length;
			StagedFilesItemSourceChanged?.Invoke(this, EventArgs.Empty);
			if (selectFirstAvailableFile)
			{
				RefreshSelection();
			}
			RefreshStageAllButton();
			RefreshStageButtons();
		}

		public void ApplyLocalization()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			UnstagedTitleTextBlock.Text = Preferences.PreferencesLocalization.Translate("Unstaged", language);
			StagedTitleTextBlock.Text = Preferences.PreferencesLocalization.Translate("Staged", language);
			FilterTextBox.Placeholder = Preferences.PreferencesLocalization.Translate("Filter", language);
		}

		public void RefreshStageAllButton()
		{
			if (UnstagedFilesFileListUserControl.ContainsVisibleItems)
			{
				StageAllButton.IsEnabled = Enabled;
				StageAllButton.ToolTip = Preferences.PreferencesLocalization.Translate("Stage All", ForkPlusSettings.Default.UiLanguage);
				StageAllButtonIcon.SetResourceReference(Image.SourceProperty, StageAllIconName);
			}
			else if (StagedFilesFileListUserControl.ContainsVisibleItems)
			{
				StageAllButton.IsEnabled = Enabled;
				StageAllButton.ToolTip = Preferences.PreferencesLocalization.Translate("Unstage All", ForkPlusSettings.Default.UiLanguage);
				StageAllButtonIcon.SetResourceReference(Image.SourceProperty, UnstageAllIconName);
			}
			else
			{
				StageAllButton.Disable();
			}
		}

		public void RefreshStageButtons()
		{
			if (KeyboardHelper.IsShiftDown)
			{
				StageButton.Content = Preferences.PreferencesLocalization.Translate("Stage All", ForkPlusSettings.Default.UiLanguage);
				UnstageButton.Content = Preferences.PreferencesLocalization.Translate("Unstage All", ForkPlusSettings.Default.UiLanguage);
				Button stageButton = StageButton;
				int isEnabled;
				if (Enabled)
				{
					ChangedFile[] items = UnstagedFilesFileListUserControl.Items;
					isEnabled = ((((items != null && items.Length != 0) ? 1 : 0) > (false ? 1 : 0)) ? 1 : 0);
				}
				else
				{
					isEnabled = 0;
				}
				stageButton.IsEnabled = (byte)isEnabled != 0;
				Button unstageButton = UnstageButton;
				int isEnabled2;
				if (Enabled)
				{
					ChangedFile[] items2 = StagedFilesFileListUserControl.Items;
					isEnabled2 = ((((items2 != null && items2.Length != 0) ? 1 : 0) > (false ? 1 : 0)) ? 1 : 0);
				}
				else
				{
					isEnabled2 = 0;
				}
				unstageButton.IsEnabled = (byte)isEnabled2 != 0;
			}
			else
			{
				StageButton.Content = Preferences.PreferencesLocalization.Translate("Stage", ForkPlusSettings.Default.UiLanguage);
				UnstageButton.Content = Preferences.PreferencesLocalization.Translate("Unstage", ForkPlusSettings.Default.UiLanguage);
				StageButton.IsEnabled = Enabled && UnstagedFilesFileListUserControl.SelectedItems.Length != 0;
				UnstageButton.IsEnabled = Enabled && StagedFilesFileListUserControl.SelectedItems.Length != 0;
			}
		}

		public void FocusActiveListView()
		{
			if (UnstagedFilesFileListUserControl.SelectedItems.Length != 0)
			{
				UnstagedFilesFileListUserControl.FocusSelectedElement();
			}
			else if (StagedFilesFileListUserControl.SelectedItems.Length != 0)
			{
				StagedFilesFileListUserControl.FocusSelectedElement();
			}
		}

		public void SelectNext()
		{
			if (IsStagedListSelected)
			{
				StagedFilesFileListUserControl.SelectNextFile();
			}
			else if (IsUnstagedListSelected)
			{
				UnstagedFilesFileListUserControl.SelectNextFile();
			}
		}

		public void SelectPrevious()
		{
			if (IsStagedListSelected)
			{
				StagedFilesFileListUserControl.SelectPreviousFile();
			}
			else if (IsUnstagedListSelected)
			{
				UnstagedFilesFileListUserControl.SelectPreviousFile();
			}
		}

		private void RefreshSelection()
		{
			if (UnstagedFilesFileListUserControl.SelectedItems.Length == 0 && StagedFilesFileListUserControl.SelectedItems.Length == 0)
			{
				if (UnstagedFilesFileListUserControl.Items.Length != 0 && UnstagedFilesFileListUserControl.SelectFirstAvailableFile())
				{
					UnstagedFilesFileListUserControl.FocusSelectedElement();
				}
				else if (StagedFilesFileListUserControl.Items.Length != 0 && StagedFilesFileListUserControl.SelectFirstAvailableFile())
				{
					StagedFilesFileListUserControl.FocusSelectedElement();
				}
			}
		}

		private void StageFilesFileListUserControl_SelectionChanged(object sender, FileListEventArgs arg)
		{
			if (!_stopHandlingSelectionEvents)
			{
				_stopHandlingSelectionEvents = true;
				UnstagedFilesFileListUserControl.ClearSelection();
				_stopHandlingSelectionEvents = false;
				SelectionChanged?.Invoke(this, arg);
				RefreshStageButtons();
			}
		}

		private void StageFilesFileListUserControl_ItemDoubleClick(object sender, FileListEventArgs arg)
		{
			this.Unstage?.Invoke(this, EventArgs.Empty);
		}

		private void StageFilesFileListUserControl_ItemsDrop(object sender, FileListTreeView.DropEventArgs arg)
		{
			this.Stage?.Invoke(this, EventArgs.Empty);
		}

		private void UnstageFilesFileListUserControl_SelectionChanged(object sender, FileListEventArgs arg)
		{
			if (!_stopHandlingSelectionEvents)
			{
				_stopHandlingSelectionEvents = true;
				StagedFilesFileListUserControl.ClearSelection();
				_stopHandlingSelectionEvents = false;
				SelectionChanged?.Invoke(this, arg);
				RefreshStageButtons();
			}
		}

		private void UnstageFilesFileListUserControl_ItemDoubleClick(object sender, FileListEventArgs arg)
		{
			this.Stage?.Invoke(this, EventArgs.Empty);
		}

		private void UnstageFilesFileListUserControl_ItemsDrop(object sender, FileListTreeView.DropEventArgs arg)
		{
			this.Unstage?.Invoke(this, EventArgs.Empty);
		}

		private void StageButton_Click(object sender, RoutedEventArgs e)
		{
			if (KeyboardHelper.IsShiftDown)
			{
				this.StageAll?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				this.Stage?.Invoke(this, EventArgs.Empty);
			}
		}

		private void UnstageButton_Click(object sender, RoutedEventArgs e)
		{
			if (KeyboardHelper.IsShiftDown)
			{
				this.UnstageAll?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				this.Unstage?.Invoke(this, EventArgs.Empty);
			}
		}

		private void UnstagedFilesFileListUserControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (!HasSelectedItems(UnstagedFilesFileListUserControl))
			{
				e.Handled = true;
			}
			else
			{
				this.UnstagedFilesContextMenuOpening?.Invoke(this, UnstagedFilesFileListUserControl.ContextMenu);
			}
		}

		private void StagedFilesFileListUserControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (!HasSelectedItems(StagedFilesFileListUserControl))
			{
				e.Handled = true;
			}
			else
			{
				this.StagedFilesContextMenuOpening?.Invoke(this, StagedFilesFileListUserControl.ContextMenu);
			}
		}

		private void FileListSettingsDropdownButton_ContextMenuOpened(object sender, RoutedEventArgs e)
		{
			this.FileListSettingsMenuOpened?.Invoke(this, FileListSettingsDropdownButton.ContextMenu);
		}

		private void UnstagedFilesFileListUserControl_ColumnHeaderSizeChanged(object sender, EventArgs e)
		{
			StagedFilesFileListUserControl.RestoreGridViewColumnWidth();
		}

		private void StagedFilesFileListUserControl_ColumnHeaderSizeChanged(object sender, EventArgs e)
		{
			UnstagedFilesFileListUserControl.RestoreGridViewColumnWidth();
		}

		private void RefreshFileListMode()
		{
			FileListsMode = ForkPlusSettings.Default.FileListMode;
		}

		public void RefreshUnstagedStatusLabel(bool hideUntrackedFiles, bool showIgnoredFiles)
		{
			if (hideUntrackedFiles)
			{
				UnstagedStatusTextBlock.Text = Preferences.PreferencesLocalization.Current("untracked files hidden");
			}
			else if (showIgnoredFiles)
			{
				UnstagedStatusTextBlock.Text = Preferences.PreferencesLocalization.Current("ignored files included");
			}
			else
			{
				UnstagedStatusTextBlock.Text = string.Empty;
			}
		}

		private static bool HasSelectedItems(FileListUserControl fileList)
		{
			return fileList.TreeView.SelectedItems.Count > 0;
		}

		private void StageAllButton_Click(object sender, RoutedEventArgs e)
		{
			if (UnstagedFilesFileListUserControl.ContainsVisibleItems)
			{
				this.StageAll?.Invoke(this, EventArgs.Empty);
				RefreshStageAllButton();
			}
			else if (StagedFilesFileListUserControl.ContainsVisibleItems)
			{
				this.UnstageAll?.Invoke(this, EventArgs.Empty);
				RefreshStageAllButton();
			}
		}

		private void ShowDiffPopupButton_Click(object sender, RoutedEventArgs e)
		{
			ShowDiffPopup?.Invoke(this, EventArgs.Empty);
		}

		private void FilterButton_Click(object sender, RoutedEventArgs e)
		{
			if (FilterTextBox.IsAnimationPlaceholderVisible)
			{
				FilterTextBox.HideWithAnimation();
			}
			else
			{
				FilterTextBox.ShowWithAnimation();
			}
		}

		private void UpdateFilter(string filterString)
		{
			UnstagedFilesFileListUserControl.FilterString = filterString;
			StagedFilesFileListUserControl.FilterString = filterString;
			RefreshStageAllButton();
		}

	}
}
