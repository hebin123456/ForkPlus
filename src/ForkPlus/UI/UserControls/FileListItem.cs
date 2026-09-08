using ForkPlus.UI.WpfCompat;
using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.UserControls
{
	public class FileListItem : MultiselectionTreeViewItem
	{
		public ChangedFile ChangedFile { get; }

		public global::Avalonia.Media.IImage ChangeTypeIcon { get; }

		public global::Avalonia.Media.IImage FileTypeIcon { get; }

		public bool IsDirectory => ChangedFile.IsDirectory;

		public string FileName { get; }

		public string FolderPath { get; }

		public string ToolTip { get; }

		public FileListItem(ChangedFile changedFile, string name, global::Avalonia.Media.IImage fileTypeIcon)
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

		private static global::Avalonia.Media.IImage GetChangeTypeIcon(ChangedFile changedFile)
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

		public override void StartDrag(global::Avalonia.Input.InputElement dragSource, MultiselectionTreeViewItem[] nodes) // Migration note：WPF DependencyObject → InputElement（DoDragDrop 需要）。
		{
			try
			{
				global::ForkPlus.UI.WpfCompat.DragDropLauncher.DoDragDrop(dragSource, GetDataObject(nodes), (global::Avalonia.Input.DragDropEffects)7);
			}
			catch
			{
			}
		}

		protected override global::Avalonia.Input.IDataTransfer GetDataObject(MultiselectionTreeViewItem[] nodes)
		{
			WpfDataObject dataObject = new WpfDataObject();
			dataObject.SetData(FileListTreeView.DragItemsFormat, nodes);
			return dataObject;
		}
	}
}
