using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class ChangeRemoteTrackingWindow : ForkPlusDialogWindow
	{
		public class RemoteBranchItem : INotifyPropertyChanged
		{
			public RemoteBranchItemType ItemType { get; private set; }

			public RemoteBranch RemoteBranch { get; private set; }

			public Visibility IconVisibility { get; private set; }

			public string Title { get; private set; }

			public string ShortName { get; private set; }

			public event PropertyChangedEventHandler PropertyChanged;

			public static RemoteBranchItem CreateRemoteBranchItem(RemoteBranch remoteBranch)
			{
				return new RemoteBranchItem(remoteBranch.Name, RemoteBranchItemType.RemoteBranch, remoteBranch, showIcon: true);
			}

			public static RemoteBranchItem CreateNoTrackingItem()
			{
				return new RemoteBranchItem(PreferencesLocalization.Current("No tracking"), RemoteBranchItemType.NoTracking);
			}

			public static RemoteBranchItem CreateSeparator()
			{
				return new RemoteBranchItem("", RemoteBranchItemType.Separator);
			}

			public RemoteBranchItem(string title, RemoteBranchItemType type, [Null] RemoteBranch remoteBranch = null, bool showIcon = false)
			{
				Title = title;
				ShortName = remoteBranch?.ShortName ?? Title;
				ItemType = type;
				RemoteBranch = remoteBranch;
				IconVisibility = ((!showIcon) ? Visibility.Collapsed : Visibility.Visible);
			}
		}

		public enum RemoteBranchItemType
		{
			RemoteBranch,
			Separator,
			NoTracking
		}

		private readonly GitModule _gitModule;

		private readonly LocalBranch _localBranch;

		private readonly RepositoryReferences _references;

	// 阶段 3：承接"选中项与当前 upstream 比较"校验 + 命令预览拼接。
	// RemoteBranchItem（含 WPF Visibility）整体留 View 作列表项；VM 仅持选中项纯数据投影。
	private readonly ChangeRemoteTrackingWindowViewModel _viewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				PushSelectionToViewModel();
				return _viewModel.IsSubmitAllowed;
			}
		}

		protected override string GetCommandPreview()
		{
			PushSelectionToViewModel();
			return _viewModel.CommandPreview;
		}

		/// <summary>把 ComboBox 选中项的纯数据投影推入 VM（RemoteBranch + 是否 NoTracking）。</summary>
		private void PushSelectionToViewModel()
		{
			RemoteBranchItem selectedItem = RemoteBranchesComboBox.SelectedItem as RemoteBranchItem;
			_viewModel.SelectedRemoteBranch = selectedItem?.RemoteBranch;
			_viewModel.IsNoTrackingSelected = selectedItem != null && selectedItem.ItemType == RemoteBranchItemType.NoTracking;
		}

		public ChangeRemoteTrackingWindow(GitModule gitModule, LocalBranch localBranch, RepositoryReferences references)
		{
			_gitModule = gitModule;
			_localBranch = localBranch;
			_references = references;
			_viewModel = new ChangeRemoteTrackingWindowViewModel(_localBranch);
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Change tracking reference");
			base.DialogDescription = PreferencesLocalization.Current("Change branch remote tracking reference");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Change");
			GitPointView.Value = _localBranch;
			Refresh();
			UpdateSubmitButton();
		}

		protected override void OnSubmit()
		{
			RemoteBranch trackingReference = (RemoteBranchesComboBox.SelectedItem as RemoteBranchItem)?.RemoteBranch;
			string name = PreferencesLocalization.Current((trackingReference == null) ? "Remove tracking reference" : "Update tracking reference");
			string message = PreferencesLocalization.Current((trackingReference == null) ? "Removing tracking reference..." : "Updating tracking reference...");
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, message);
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult updateTrackingResult = new UpdateTrackingReferenceGitCommand().Execute(_gitModule, _localBranch, trackingReference, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(updateTrackingResult);
				});
			}, JobFlags.SaveToLog);
		}

		private void RemoteBranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void Refresh()
		{
			RemoteBranchItem remoteBranchItem = RemoteBranchItem.CreateNoTrackingItem();
			RemoteBranchItem[] array = _references.RemoteBranches.Map((RemoteBranch x) => RemoteBranchItem.CreateRemoteBranchItem(x));
			List<RemoteBranchItem> list = new List<RemoteBranchItem>(array.Length + 2);
			list.Add(remoteBranchItem);
			list.Add(RemoteBranchItem.CreateSeparator());
			list.AddRange(array);
			RemoteBranchesComboBox.ItemsSource = list;
			string upstreamFullReference = _localBranch.UpstreamFullReference;
			if (upstreamFullReference != null)
			{
				RemoteBranchesComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(array, (RemoteBranchItem x) => x.RemoteBranch.FullReference == upstreamFullReference);
			}
			else
			{
				RemoteBranchesComboBox.SelectedItem = remoteBranchItem;
			}
		}

	}
}
