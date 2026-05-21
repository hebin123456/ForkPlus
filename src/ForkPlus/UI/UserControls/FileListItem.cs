using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public class FileListItem : MultiselectionTreeViewItem
	{
		public ChangedFile ChangedFile { get; }

		public ImageSource ChangeTypeIcon { get; }

		public ImageSource FileTypeIcon { get; }

		public bool IsDirectory => ChangedFile.IsDirectory;

		public string FileName { get; }

		public string FolderPath { get; }

		public string ToolTip { get; }

		public FileListItem(ChangedFile changedFile, string name, ImageSource fileTypeIcon)
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

		private static ImageSource GetChangeTypeIcon(ChangedFile changedFile)
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

		public override void StartDrag(DependencyObject dragSource, MultiselectionTreeViewItem[] nodes)
		{
			try
			{
				DragDrop.DoDragDrop(dragSource, GetDataObject(nodes), DragDropEffects.All);
			}
			catch
			{
			}
		}

		protected override IDataObject GetDataObject(MultiselectionTreeViewItem[] nodes)
		{
			DataObject dataObject = new DataObject();
			dataObject.SetData(FileListTreeView.DragItemsFormat, nodes);
			return dataObject;
		}
	}
}
