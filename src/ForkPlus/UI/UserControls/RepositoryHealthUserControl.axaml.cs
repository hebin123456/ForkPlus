using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.WpfCompat;

namespace ForkPlus.UI.UserControls
{
	// WS4 仓库健康仪表盘内容区：单页折叠布局。
	//   概览条（常驻）：.git 体积 / pack 数与体积 / 松散对象数与体积 / 关注项计数
	//   陈旧分支（默认展开）：全部本地分支按 CommitterDate 升序，≥90 天标黄，可勾选批量删除
	//   已合并分支（默认展开）：GetMergedBranchesGitCommand（排除基准与当前分支），可勾选批量删除
	//   分叉分支（默认折叠）：UpstreamStatusCache 中 Ahead>0 且 Behind>0 的分支
	//   大文件 Top10（默认折叠，只读）：GetBiggestBlobsGitCommand，右键复制路径/看文件历史
	//   体积明细（默认折叠）：.git 顶层子项体积分类
	// 加载走 RepositoryUserControl.JobQueue + 令牌防过期（仿 StatisticsUserControl 的
	// RefreshAiStats 写法）；数据在后台线程算好 VM 再 Dispatcher.Post 回 UI 渲染。
	public partial class RepositoryHealthUserControl : UserControl, ILocalizableControl
	{
		/// <summary>陈旧分支阈值（天）：CommitterDate 距今超过该值标为陈旧。</summary>
		private const int StaleBranchDays = 90;

		/// <summary>大文件 Top-N。与 GetBiggestBlobsGitCommand.DefaultMaxCount 保持一致。</summary>
		private const int LargestFilesCount = GetBiggestBlobsGitCommand.DefaultMaxCount;

		/// <summary>分支行 ViewModel（陈旧/已合并/分叉三区共用）：勾选态 + 名称 + 明细文案。</summary>
		public class BranchHealthItemViewModel : INotifyPropertyChanged
		{
			public string FullReference { get; }

			public string Name { get; }

			public string DetailText { get; }

			public IBrush DetailBrush { get; }

			/// <summary>是否陈旧（≥阈值天数）。删除联动刷新关注项计数用（陈旧区含全部
			/// 本地分支，其余区恒 false；渲染文案仍用 DetailText/DetailBrush）。</summary>
			public bool IsStale { get; }

			private bool _isChecked;

			public bool IsChecked
			{
				get
				{
					return _isChecked;
				}
				set
				{
					if (_isChecked != value)
					{
						_isChecked = value;
						PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsChecked"));
					}
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public BranchHealthItemViewModel(string fullReference, string name, string detailText, IBrush detailBrush, bool isStale = false)
			{
				FullReference = fullReference;
				Name = name;
				DetailText = detailText;
				DetailBrush = detailBrush;
				IsStale = isStale;
			}
		}

		/// <summary>大文件行 ViewModel：路径 + 体积。</summary>
		public class BigBlobItemViewModel
		{
			public string Path { get; }

			public long Size { get; }

			public string SizeText { get; }

			public BigBlobItemViewModel(string path, long size, string sizeText)
			{
				Path = path;
				Size = size;
				SizeText = sizeText;
			}
		}

		/// <summary>体积明细行 ViewModel：名称 + 体积（字节，用于排序）+ 体积文案 + 占比。</summary>
		public class GitDirSizeItemViewModel
		{
			public string Name { get; }

			public long Size { get; }

			public string SizeText { get; }

			public string ShareText { get; }

			public GitDirSizeItemViewModel(string name, long size, string sizeText, string shareText)
			{
				Name = name;
				Size = size;
				SizeText = sizeText;
				ShareText = shareText;
			}
		}

		/// <summary>后台线程算好的整页数据（VM 已构造，回 UI 只做绑定）。</summary>
		private class RepositoryHealthData
		{
			public long GitSize;
			public int PackCount;
			public long PackSize;
			public int LooseCount;
			public long LooseSize;
			public int StaleCount;
			public ObservableCollection<BranchHealthItemViewModel> StaleBranches;
			public ObservableCollection<BranchHealthItemViewModel> MergedBranches;
			public ObservableCollection<BranchHealthItemViewModel> DivergedBranches;
			public List<BigBlobItemViewModel> LargestFiles;
			public List<GitDirSizeItemViewModel> SizeBreakdown;
		}

		[Null]
		private RepositoryUserControl _repositoryUserControl;

		[Null]
		private GitModule _gitModule;

		/// <summary>刷新令牌：每次 ShowHealth 自增，过期任务的回投结果直接丢弃。</summary>
		private int _refreshToken;

		public RepositoryHealthUserControl()
		{
			InitializeComponent();
			ApplyLocalization();
			StaleEmptyText.Foreground = global::ForkPlus.UI.Theme.ApplicationColors.GreenBrush;
			MergedEmptyText.Foreground = global::ForkPlus.UI.Theme.ApplicationColors.GreenBrush;
			DivergedEmptyText.Foreground = global::ForkPlus.UI.Theme.ApplicationColors.GreenBrush;
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
		}

		/// <summary>开始分析并渲染。git 命令与目录枚举全部在 JobQueue 后台线程执行。</summary>
		public void ShowHealth(RepositoryUserControl repositoryUserControl)
		{
			_repositoryUserControl = repositoryUserControl;
			_gitModule = repositoryUserControl.GitModule;
			LoadingTextBlock.Text = Translate("Analyzing repository health...");
			LoadingTextBlock.Show();
			ContentContainer.Collapse();
			int token = ++_refreshToken;
			GitModule gitModule = _gitModule;
			// RepositoryData 是不可变快照，后台线程只读安全
			RepositoryReferences references = repositoryUserControl.RepositoryData?.References;
			UpstreamStatusCache upstreamStatus = repositoryUserControl.RepositoryData?.UpstreamStatus;
			// LeanBranchingMainBranch 在 UI 线程读好快照（设置读取不跨线程）
			string mergedBaseRef = ResolveMergedBaseRef(gitModule);
			// 主题 Brush 必须在 UI 线程预取：Application.Current.TryGetResource 有线程亲和，
			// 后台线程取会抛 InvalidOperationException（JobQueue 吞掉 → 仪表盘永不开张）。
			// 预取后仅传引用，VM 渲染发生在 UI 线程，无跨线程访问。
			global::Avalonia.Media.IBrush staleBrush = global::ForkPlus.UI.Theme.ApplicationColors.YellowBrush;
			global::Avalonia.Media.IBrush normalBrush = global::ForkPlus.UI.Theme.SecondaryLabelBrush;
			global::Avalonia.Media.IBrush mergedBrush = global::ForkPlus.UI.Theme.ApplicationColors.GreenBrush;
			repositoryUserControl.JobQueue.Add("RepositoryHealth", delegate (JobMonitor monitor)
			{
				RepositoryHealthData data = ComputeHealthData(gitModule, references, upstreamStatus, mergedBaseRef, staleBrush, normalBrush, mergedBrush);
				Dispatcher.Post(delegate
				{
					// 窗口重开/仓库切换后 token 变化，丢弃过期结果
					if (token != _refreshToken)
					{
						return;
					}
					RenderHealthData(data);
				});
			}, JobFlags.LongRunning, showMessageWhenDone: false);
		}

		private static string ResolveMergedBaseRef([Null] GitModule gitModule)
		{
			string baseRef = gitModule?.Settings.LeanBranchingMainBranch;
			if (string.IsNullOrWhiteSpace(baseRef))
			{
				baseRef = "HEAD";
			}
			return baseRef;
		}

		// ============================ 后台计算 ============================

		private RepositoryHealthData ComputeHealthData([Null] GitModule gitModule, [Null] RepositoryReferences references, [Null] UpstreamStatusCache upstreamStatus, string mergedBaseRef, global::Avalonia.Media.IBrush staleBrush, global::Avalonia.Media.IBrush normalBrush, global::Avalonia.Media.IBrush mergedBrush)
		{
			RepositoryHealthData data = new RepositoryHealthData();
			data.StaleBranches = new ObservableCollection<BranchHealthItemViewModel>();
			data.MergedBranches = new ObservableCollection<BranchHealthItemViewModel>();
			data.DivergedBranches = new ObservableCollection<BranchHealthItemViewModel>();
			data.LargestFiles = new List<BigBlobItemViewModel>();
			data.SizeBreakdown = new List<GitDirSizeItemViewModel>();
			if (gitModule == null)
			{
				return data;
			}
			ComputeOverviewAndBreakdown(gitModule, data);
			ComputeBranchSections(gitModule, references, upstreamStatus, mergedBaseRef, data, staleBrush, normalBrush, mergedBrush);
			ComputeLargestFiles(gitModule, data);
			return data;
		}

		/// <summary>概览条 + 体积明细：.NET 枚举 GitDir()（目录不存在/符号链接均兜底）。</summary>
		private void ComputeOverviewAndBreakdown(GitModule gitModule, RepositoryHealthData data)
		{
			string gitDir = gitModule.GitDir();
			if (!Directory.Exists(gitDir))
			{
				return;
			}
			data.GitSize = GetDirectorySize(gitDir);
			string objectsDir = Path.Combine(gitDir, "objects");
			string packDir = Path.Combine(objectsDir, "pack");
			string infoDir = Path.Combine(objectsDir, "info");
			int packCount = 0;
			long packSize = 0;
			if (Directory.Exists(packDir))
			{
				foreach (string file in SafeEnumerateFiles(packDir))
				{
					if (file.EndsWith(".pack", StringComparison.OrdinalIgnoreCase))
					{
						packCount++;
						packSize += SafeFileSize(file);
					}
				}
			}
			data.PackCount = packCount;
			data.PackSize = packSize;
			int looseCount = 0;
			long looseSize = 0;
			if (Directory.Exists(objectsDir))
			{
				foreach (string dir in SafeEnumerateDirectories(objectsDir))
				{
					string name = Path.GetFileName(dir);
					if (name == "pack" || name == "info")
					{
						continue;
					}
					foreach (string file in SafeEnumerateFiles(dir))
					{
						looseCount++;
						looseSize += SafeFileSize(file);
					}
				}
			}
			data.LooseCount = looseCount;
			data.LooseSize = looseSize;

			// 体积明细：pack 目录 / 松散对象（含 objects/info）/ .git 顶层各目录 / 其余顶层文件合并为 Other
			long packDirSize = Directory.Exists(packDir) ? GetDirectorySize(packDir) : 0;
			long objectsSize = Directory.Exists(objectsDir) ? GetDirectorySize(objectsDir) : 0;
			long infoSize = Directory.Exists(infoDir) ? GetDirectorySize(infoDir) : 0;
			AddSizeBreakdownRow(data, Translate("Packed objects"), packDirSize, data.GitSize);
			AddSizeBreakdownRow(data, Translate("Loose objects"), objectsSize - packDirSize - infoSize, data.GitSize);
			AddSizeBreakdownRow(data, "info", infoSize, data.GitSize);
			foreach (string dir in SafeEnumerateDirectories(gitDir))
			{
				string name = Path.GetFileName(dir);
				if (name == "objects")
				{
					continue;
				}
				AddSizeBreakdownRow(data, name, GetDirectorySize(dir), data.GitSize);
			}
			long otherSize = 0;
			foreach (string file in SafeEnumerateFiles(gitDir))
			{
				otherSize += SafeFileSize(file);
			}
			AddSizeBreakdownRow(data, Translate("Other"), otherSize, data.GitSize);
			// 按体积降序展示（"Packed objects"/"Loose objects" 本就是大头，通常会靠前）
			data.SizeBreakdown.Sort((GitDirSizeItemViewModel lhs, GitDirSizeItemViewModel rhs) => rhs.Size.CompareTo(lhs.Size));
		}

		/// <summary>陈旧/已合并/分叉三区。Brush 由 UI 线程预取传入（FindBrush 线程亲和，见 ShowHealth）。</summary>
		private void ComputeBranchSections(GitModule gitModule, [Null] RepositoryReferences references, [Null] UpstreamStatusCache upstreamStatus, string mergedBaseRef, RepositoryHealthData data, global::Avalonia.Media.IBrush staleBrush, global::Avalonia.Media.IBrush normalBrush, global::Avalonia.Media.IBrush mergedBrush)
		{
			if (references == null)
			{
				return;
			}
			LocalBranch[] branchesByDate = references.LocalBranches.OrderBy((LocalBranch x) => x.CommitterDate).ToArray();
			// —— 陈旧分支：全部本地分支按 CommitterDate 升序，≥阈值标黄 ——
			foreach (LocalBranch branch in branchesByDate)
			{
				int daysOld = Math.Max(0, (int)(DateTime.Now - branch.CommitterDate).TotalDays);
				bool isStale = daysOld >= StaleBranchDays;
				if (isStale)
				{
					data.StaleCount++;
				}
				data.StaleBranches.Add(new BranchHealthItemViewModel(
					branch.FullReference,
					branch.Name,
					string.Format(CultureInfo.CurrentUICulture, Translate("{0} days old"), daysOld),
					isStale ? staleBrush : normalBrush,
					isStale));
			}
			// —— 已合并分支：GetMergedBranchesGitCommand 结果含基准自身，需排除基准与当前分支 ——
			GitCommandResult<string[]> mergedResult = new GetMergedBranchesGitCommand().Execute(gitModule, mergedBaseRef);
			if (mergedResult.Succeeded)
			{
				HashSet<string> mergedSet = new HashSet<string>(mergedResult.Result, StringComparer.Ordinal);
				string baseFullRef = "refs/heads/" + mergedBaseRef;
				foreach (LocalBranch branch in branchesByDate)
				{
					if (branch.IsActive || branch.FullReference == baseFullRef)
					{
						continue;
					}
					if (mergedSet.Contains(branch.FullReference))
					{
						data.MergedBranches.Add(new BranchHealthItemViewModel(
							branch.FullReference,
							branch.Name,
							"✓ " + string.Format(CultureInfo.CurrentUICulture, Translate("Merged into {0}"), mergedBaseRef),
							mergedBrush));
					}
				}
			}
			// —— 分叉分支：UpstreamStatus（与侧栏/工具栏角标同一数据源）Ahead>0 且 Behind>0 ——
			if (upstreamStatus != null)
			{
				foreach (LocalBranch branch in branchesByDate)
				{
					if (branch.UpstreamFullReference == null)
					{
						continue;
					}
					UpstreamStatus? status = upstreamStatus.GetUpstreamStatus(branch);
					if (status.HasValue && status.Value.IsValid && status.Value.Ahead > 0 && status.Value.Behind > 0)
					{
						data.DivergedBranches.Add(new BranchHealthItemViewModel(
							branch.FullReference,
							branch.Name,
							string.Format(CultureInfo.CurrentUICulture, Translate("ahead {0}, behind {1}"), status.Value.Ahead, status.Value.Behind),
							staleBrush));
					}
				}
			}
		}

		private void ComputeLargestFiles(GitModule gitModule, RepositoryHealthData data)
		{
			GitCommandResult<(string Path, long Size)[]> blobsResult = new GetBiggestBlobsGitCommand().Execute(gitModule, LargestFilesCount);
			if (!blobsResult.Succeeded)
			{
				return;
			}
			foreach ((string blobPath, long blobSize) in blobsResult.Result)
			{
				data.LargestFiles.Add(new BigBlobItemViewModel(blobPath, blobSize, FileHelper.GetReadableFileSize(blobSize, addSizeInBytes: false)));
			}
		}

		private void AddSizeBreakdownRow(RepositoryHealthData data, string name, long size, long totalSize)
		{
			if (size <= 0)
			{
				return;
			}
			string share = totalSize > 0
				? Math.Round(size * 100.0 / totalSize, 1).ToString("0.#", CultureInfo.CurrentUICulture) + "%"
				: "-";
			data.SizeBreakdown.Add(new GitDirSizeItemViewModel(name, size, FileHelper.GetReadableFileSize(size, addSizeInBytes: false), share));
		}

		/// <summary>递归目录体积。跳过符号链接（ReparsePoint）防循环；单条目失败静默跳过。</summary>
		private static long GetDirectorySize(string path)
		{
			long total = 0;
			foreach (string file in SafeEnumerateFiles(path))
			{
				total += SafeFileSize(file);
			}
			foreach (string dir in SafeEnumerateDirectories(path))
			{
				try
				{
					if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
					{
						continue;
					}
				}
				catch
				{
					continue;
				}
				total += GetDirectorySize(dir);
			}
			return total;
		}

		private static IEnumerable<string> SafeEnumerateFiles(string path)
		{
			try
			{
				return Directory.Exists(path) ? Directory.EnumerateFiles(path) : Array.Empty<string>();
			}
			catch
			{
				return Array.Empty<string>();
			}
		}

		private static IEnumerable<string> SafeEnumerateDirectories(string path)
		{
			try
			{
				return Directory.Exists(path) ? Directory.EnumerateDirectories(path) : Array.Empty<string>();
			}
			catch
			{
				return Array.Empty<string>();
			}
		}

		private static long SafeFileSize(string path)
		{
			try
			{
				return new FileInfo(path).Length;
			}
			catch
			{
				return 0;
			}
		}

		// ============================ 渲染（UI 线程） ============================

		private void RenderHealthData(RepositoryHealthData data)
		{
			LoadingTextBlock.Collapse();
			ContentContainer.Show();
			GitSizeValueText.Text = FileHelper.GetReadableFileSize(data.GitSize, addSizeInBytes: false);
			PackedObjectsValueText.Text = data.PackCount.ToString("N0", CultureInfo.CurrentUICulture) + " · " + FileHelper.GetReadableFileSize(data.PackSize, addSizeInBytes: false);
			LooseObjectsValueText.Text = data.LooseCount.ToString("N0", CultureInfo.CurrentUICulture) + " · " + FileHelper.GetReadableFileSize(data.LooseSize, addSizeInBytes: false);
			AttentionItemsValueText.Text = (data.StaleCount + data.MergedBranches.Count + data.DivergedBranches.Count).ToString("N0", CultureInfo.CurrentUICulture);

			StaleEmptyText.Text = "✓ " + Translate("No stale branches");
			StaleEmptyText.IsVisible = data.StaleBranches.Count == 0;
			StaleBranchList.ItemsSource = data.StaleBranches;
			StaleDeleteButton.IsVisible = data.StaleBranches.Count > 0;

			MergedEmptyText.Text = "✓ " + Translate("No merged branches");
			MergedEmptyText.IsVisible = data.MergedBranches.Count == 0;
			MergedBranchList.ItemsSource = data.MergedBranches;
			MergedDeleteButton.IsVisible = data.MergedBranches.Count > 0;

			DivergedEmptyText.Text = "✓ " + Translate("No diverged branches");
			DivergedEmptyText.IsVisible = data.DivergedBranches.Count == 0;
			DivergedBranchList.ItemsSource = data.DivergedBranches;

			LargestFilesList.ItemsSource = data.LargestFiles;
			SizeBreakdownList.ItemsSource = data.SizeBreakdown;
		}

		// ============================ 删除选中分支 ============================

		private void StaleDeleteButton_Click(object sender, RoutedEventArgs e)
		{
			DeleteSelectedBranches(StaleBranchList.ItemsSource as ObservableCollection<BranchHealthItemViewModel>);
		}

		private void MergedDeleteButton_Click(object sender, RoutedEventArgs e)
		{
			DeleteSelectedBranches(MergedBranchList.ItemsSource as ObservableCollection<BranchHealthItemViewModel>);
		}

		/// <summary>批量删除勾选分支。照抄 RemoveLocalBranchWindow.OnSubmit 的安全链路：
		/// AddUndoable（操作前抓快照，Ctrl+Z 可撤销）+ RemoveLocalBranchGitCommand（branch -D）。</summary>
		private void DeleteSelectedBranches([Null] ObservableCollection<BranchHealthItemViewModel> items)
		{
			if (items == null || items.Count == 0)
			{
				return;
			}
			List<BranchHealthItemViewModel> selected = new List<BranchHealthItemViewModel>();
			foreach (BranchHealthItemViewModel item in items)
			{
				if (item.IsChecked)
				{
					selected.Add(item);
				}
			}
			if (selected.Count == 0)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			GitModule gitModule = _gitModule;
			if (repositoryUserControl == null || gitModule == null)
			{
				return;
			}
			string[] branchNames = selected.Select((BranchHealthItemViewModel x) => x.Name).ToArray();
			// 与 RemoveLocalBranchWindow.OnSubmit 同款保护：受保护分支需二次确认才允许删除
			Window ownerWindow = (TopLevel.GetTopLevel(this) as Window) ?? MainWindow.Instance;
			// 二次确认（2026-09-30，"批量删除选中分支无确认太危险"）：删除前弹确认窗列出
			// 将删除的分支名（超 15 个截断省略）。owner 显式挂当前顶层窗口——ForkPlusDialogWindow
			// 构造默认把 owner 登记为 MainWindow，关闭时主窗口会压到健康窗口上面（同 Reflog
			// 跳转确认窗 2026-09-30 修复）。
			const int maxListedBranches = 15;
			string listedNames = string.Join(", ", branchNames.Take(maxListedBranches))
				+ (branchNames.Length > maxListedBranches ? ", …" : "");
			string confirmMessage = string.Format(
				CultureInfo.CurrentUICulture,
				Translate("Delete {0} selected branches?\n\n{1}\n\nYou can undo this afterwards."),
				selected.Count,
				listedNames);
			ForkPlus.UI.Dialogs.MessageBoxWindow confirmDialog = new ForkPlus.UI.Dialogs.MessageBoxWindow(
				Translate("Delete Branches"),
				confirmMessage,
				Translate("Delete"),
				Translate("Cancel"),
				showCancelButton: true,
				620.0);
			confirmDialog.SetOwnerCompat(ownerWindow);
			if (!confirmDialog.ShowDialog().GetValueOrDefault())
			{
				return;
			}
			if (!ForkPlus.UI.Dialogs.ProtectedBranchConfirmWindow.Confirm(ownerWindow, gitModule.Settings.ProtectedBranches, branchNames))
			{
				return;
			}
			string operationName = string.Format(CultureInfo.CurrentUICulture, Translate("Delete {0} branches"), selected.Count);
			repositoryUserControl.AddUndoable(operationName, delegate (JobMonitor monitor)
			{
				GitCommandResult result = new RemoveLocalBranchGitCommand().Execute(gitModule, branchNames, monitor);
				Dispatcher.Post(delegate
				{
					if (result.Succeeded)
					{
						RefreshAfterBranchDeletion(selected);
					}
				});
				return result;
			}, JobFlags.SaveToLog);
		}

		/// <summary>删除成功后三区联动刷新（2026-09-30，"一个分区删掉了另一个还在"）：
		/// 陈旧区含全部本地分支，与已合并/分叉天然重叠——同名分支在多个分区同时存在，只清
		/// 当前分区会"这边删了那边还在"。此处按 FullReference 从三区集合同步移除，并同步
		/// 概览关注项计数、空态文案与删除按钮可见性（与 RenderHealthData 同规则）。
		/// 不走 ShowHealth 整体重算：删除后 RepositoryData 快照由仓库监听异步刷新，
		/// 立即重算会读到旧引用反而把已删分支渲染回来；窗口重开时 ShowHealth 自然全量重算。</summary>
		private void RefreshAfterBranchDeletion(List<BranchHealthItemViewModel> deleted)
		{
			HashSet<string> deletedRefs = new HashSet<string>(
				deleted.Select((BranchHealthItemViewModel x) => x.FullReference), StringComparer.Ordinal);
			ObservableCollection<BranchHealthItemViewModel> stale = StaleBranchList.ItemsSource as ObservableCollection<BranchHealthItemViewModel>;
			ObservableCollection<BranchHealthItemViewModel> merged = MergedBranchList.ItemsSource as ObservableCollection<BranchHealthItemViewModel>;
			ObservableCollection<BranchHealthItemViewModel> diverged = DivergedBranchList.ItemsSource as ObservableCollection<BranchHealthItemViewModel>;
			RemoveDeletedItems(stale, deletedRefs);
			RemoveDeletedItems(merged, deletedRefs);
			RemoveDeletedItems(diverged, deletedRefs);
			// 关注项计数 = 陈旧分支数（IsStale 标记）+ 已合并数 + 分叉数，与 ComputeBranchSections 口径一致
			int staleCount = stale?.Count((BranchHealthItemViewModel x) => x.IsStale) ?? 0;
			AttentionItemsValueText.Text = (staleCount + (merged?.Count ?? 0) + (diverged?.Count ?? 0)).ToString("N0", CultureInfo.CurrentUICulture);
			// 空态文案 + 删除按钮可见性（与 RenderHealthData 同规则）
			StaleEmptyText.IsVisible = stale == null || stale.Count == 0;
			StaleDeleteButton.IsVisible = stale != null && stale.Count > 0;
			MergedEmptyText.IsVisible = merged == null || merged.Count == 0;
			MergedDeleteButton.IsVisible = merged != null && merged.Count > 0;
			DivergedEmptyText.IsVisible = diverged == null || diverged.Count == 0;
		}

		/// <summary>从分区集合移除已删除的引用（倒序遍历防索引位移）。</summary>
		private static void RemoveDeletedItems([Null] ObservableCollection<BranchHealthItemViewModel> items, HashSet<string> deletedRefs)
		{
			if (items == null)
			{
				return;
			}
			for (int i = items.Count - 1; i >= 0; i--)
			{
				if (deletedRefs.Contains(items[i].FullReference))
				{
					items.RemoveAt(i);
				}
			}
		}

		// ============================ 大文件右键菜单 ============================

		private void BigBlobCopyPath_Click(object sender, RoutedEventArgs e)
		{
			BigBlobItemViewModel item = ResolveBigBlobItem(sender);
			if (item != null)
			{
				ForkPlus.Services.ServiceLocator.Clipboard.SetText(item.Path);
			}
		}

		private void BigBlobViewHistory_Click(object sender, RoutedEventArgs e)
		{
			BigBlobItemViewModel item = ResolveBigBlobItem(sender);
			if (item != null && _repositoryUserControl != null)
			{
				// 与 CommitUserControl 右键菜单同款打开方式（Commands 是 RepositoryUserControl 的静态字段）
				RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(
					_repositoryUserControl,
					new ForkPlus.UI.Commands.ShowFileHistoryWindowCommand.Mode.File(item.Path),
					null);
			}
		}

		[Null]
		private static BigBlobItemViewModel ResolveBigBlobItem(object sender)
		{
			if (sender is MenuItem menuItem)
			{
				if (menuItem.DataContext is BigBlobItemViewModel direct)
				{
					return direct;
				}
				if (menuItem.Parent is ContextMenu contextMenu && contextMenu.PlacementTarget?.DataContext is BigBlobItemViewModel viaTarget)
				{
					return viaTarget;
				}
			}
			return null;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
