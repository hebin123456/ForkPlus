// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Input
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage（Avalonia.Media）
// - DependencyObject → AvaloniaObject（StartDrag 参数，与已迁移基类 MultiselectionTreeViewItem 一致）
// - DragDrop.DoDragDrop → _ = DragDrop.DoDragDrop（异步返回 Task<DragDropEffects>，丢弃；参考 LocalBranchSidebarItem）
// - DataObject.SetData → DataObject.Set（Avalonia 11.3 方法名）
// TODO(4.5): ChangeType.GetImageSource()（定义于 BridgeExtensions.cs ChangeTypeBridgeExtensions，尚未迁移）
//            仍返回 WPF System.Windows.Media.ImageSource。待 BridgeExtensions 迁移为返回 Avalonia.Media.IImage 后，
//            此赋值类型与 FileListItem.ChangeTypeIcon (IImage) 一致（参考 ReferencePanel 对 Remote.Icon 的处理）。
using System;
using System.IO;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public class FileListItem : MultiselectionTreeViewItem
	{
		public ChangedFile ChangedFile { get; }

		public IImage ChangeTypeIcon { get; }

		public IImage FileTypeIcon { get; }

		public bool IsDirectory => ChangedFile.IsDirectory;

		public string FileName { get; }

		public string FolderPath { get; }

		public string ToolTip { get; }

		public FileListItem(ChangedFile changedFile, string name, IImage fileTypeIcon)
		{
			ChangedFile = changedFile;
			ChangeTypeIcon = GetChangeTypeIcon(changedFile);
			base.Title = name;
			FileTypeIcon = fileTypeIcon;
			if (!string.IsNullOrEmpty(name))
			{
				FileName = Path.GetFileName(name);
				FolderPath = Path.GetDirectoryName(name);
			}
			if (changedFile.ChangeType == ChangeType.Renamed)
			{
				ToolTip = PreferencesLocalization.FormatCurrent("Old:\t{0}\nNew:\t{1}", changedFile.OldPath, changedFile.Path);
			}
		}

		protected override bool MatchFilter(string filterString)
		{
			if (string.IsNullOrEmpty(filterString))
			{
				return true;
			}
			if (ChangedFile.Path.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) != -1)
			{
				return true;
			}
			return false;
		}

		private static IImage GetChangeTypeIcon(ChangedFile changedFile)
		{
			if (changedFile.IsDirectory)
			{
				return null;
			}
			return changedFile.ChangeType.GetImageSource();
		}

		internal static bool ByTypeThenByTitlePredicate(FileListItem l, FileListItem r)
		{
			if (l.IsDirectory == r.IsDirectory)
			{
				switch (l.Title.CompareTo(r.Title))
				{
				case 0:
					return (int)l.ChangedFile.ChangeType <= (int)r.ChangedFile.ChangeType;
				case -1:
					return true;
				case 1:
					return false;
				}
			}
			return l.IsDirectory;
		}

		public override void StartDrag(AvaloniaObject dragSource, MultiselectionTreeViewItem[] nodes)
		{
			try
			{
				_ = DragDrop.DoDragDrop(dragSource, GetDataObject(nodes), DragDropEffects.All);
			}
			catch
			{
			}
		}

		protected override IDataObject GetDataObject(MultiselectionTreeViewItem[] nodes)
		{
			DataObject dataObject = new DataObject();
			dataObject.Set(FileListTreeView.DragItemsFormat, nodes);
			return dataObject;
		}
	}
}
