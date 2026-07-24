// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
using System;
using System.ComponentModel;
using System.IO;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.UserControls
{
	public class DiffEntry : INotifyPropertyChanged
	{
		private GitCommandResult<DiffContent> _content;

		private bool _isExpanded;

		public RepositoryUserControl RepositoryUserControl { get; }

		public string FilePath => ChangedFile.Path;

		[Null]
		public IImage FileTypeIcon { get; }

		public ChangedFile ChangedFile { get; }

		[Null]
		public IImage ChangeTypeIcon { get; }

		public string ToolTip { get; }

		public GitCommandResult<DiffContent> Content
		{
			get
			{
				return _content;
			}
			set
			{
				if (_content != value)
				{
					_content = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Content"));
				}
			}
		}

		public bool IsExpanded
		{
			get
			{
				return _isExpanded;
			}
			set
			{
				if (_isExpanded != value)
				{
					_isExpanded = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpanded"));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public DiffEntry(RepositoryUserControl repositoryUserControl, ChangedFile changedFile)
		{
			RepositoryUserControl = repositoryUserControl;
			FileTypeIcon = GetIconFileType(changedFile.Path);
			ChangedFile = changedFile;
			ChangeTypeIcon = GetChangeTypeIcon(changedFile);
		}

		[Null]
		private IImage GetIconFileType(string filePath)
		{
			try
			{
				return IconTools.GetImageSourceForExtension(Path.GetExtension(filePath));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get icon type for '" + filePath + "'", ex);
				return null;
			}
		}

		[Null]
		private static IImage GetChangeTypeIcon(ChangedFile changedFile)
		{
			if (changedFile.IsDirectory)
			{
				return null;
			}
			return changedFile.Status.GetImageSource();
		}
	}
}
