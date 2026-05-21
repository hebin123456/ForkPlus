using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ForkPlus.Git
{
	public static class ChangeTypeExtensions
	{
		private static readonly Uri AddIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Add.png");

		private static readonly Uri EditIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Edit.png");

		private static readonly Uri CopyIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Copy.png");

		private static readonly Uri DeletedIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Remove.png");

		private static readonly Uri RenamedIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Rename.png");

		private static readonly Uri TypeChangedIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Edit.png");

		private static readonly Uri UnmergedIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Warning.png");

		private static readonly ImageSource AddIcon = new BitmapImage(AddIconUrl);

		private static readonly ImageSource EditIcon = new BitmapImage(EditIconUrl);

		private static readonly ImageSource CopyIcon = new BitmapImage(CopyIconUrl);

		private static readonly ImageSource DeletedIcon = new BitmapImage(DeletedIconUrl);

		private static readonly ImageSource RenamedIcon = new BitmapImage(RenamedIconUrl);

		private static readonly ImageSource TypeChangedIcon = new BitmapImage(TypeChangedIconUrl);

		private static readonly ImageSource UnmergedIcon = new BitmapImage(UnmergedIconUrl);

		static ChangeTypeExtensions()
		{
			Freeze(AddIcon);
			Freeze(EditIcon);
			Freeze(CopyIcon);
			Freeze(DeletedIcon);
			Freeze(RenamedIcon);
			Freeze(TypeChangedIcon);
			Freeze(UnmergedIcon);
		}

		private static void Freeze(ImageSource imageSource)
		{
			if (imageSource?.CanFreeze == true)
			{
				imageSource.Freeze();
			}
		}

		public static ImageSource GetImageSource(this ChangeType changeType)
		{
			return changeType switch
			{
				ChangeType.Added => AddIcon, 
				ChangeType.Untracked => AddIcon, 
				ChangeType.Modified => EditIcon, 
				ChangeType.Copied => CopyIcon, 
				ChangeType.Deleted => DeletedIcon, 
				ChangeType.Renamed => RenamedIcon, 
				ChangeType.Unmerged => UnmergedIcon, 
				ChangeType.TypeChanged => TypeChangedIcon, 
				ChangeType.Unknown => EditIcon, 
				ChangeType.Ignored => AddIcon, 
				_ => null, 
			};
		}

		public static ImageSource GetImageSource(this StatusType statusType)
		{
			return statusType switch
			{
				StatusType.Added => AddIcon, 
				StatusType.Broken => EditIcon, 
				StatusType.Copied => CopyIcon, 
				StatusType.Deleted => DeletedIcon, 
				StatusType.Modified => EditIcon, 
				StatusType.Ignored => AddIcon, 
				StatusType.Renamed => RenamedIcon, 
				StatusType.TypeChanged => TypeChangedIcon, 
				StatusType.Unmerged => EditIcon, 
				StatusType.Untracked => AddIcon, 
				StatusType.Unknown => EditIcon, 
				StatusType.None => EditIcon, 
				_ => null, 
			};
		}

		public static ImageSource GetConflictImageSource(this StatusType statusType)
		{
			return statusType switch
			{
				StatusType.Added => AddIcon, 
				StatusType.Broken => EditIcon, 
				StatusType.Copied => CopyIcon, 
				StatusType.Deleted => DeletedIcon, 
				StatusType.Modified => EditIcon, 
				StatusType.Ignored => AddIcon, 
				StatusType.Renamed => RenamedIcon, 
				StatusType.TypeChanged => TypeChangedIcon, 
				StatusType.Unmerged => EditIcon, 
				StatusType.Untracked => AddIcon, 
				StatusType.Unknown => EditIcon, 
				StatusType.None => EditIcon, 
				_ => null, 
			};
		}

		public static string ToFriendlyName(this StatusType statusType)
		{
			return statusType switch
			{
				StatusType.Added => "added", 
				StatusType.Broken => "broken", 
				StatusType.Copied => "copied", 
				StatusType.Deleted => "deleted", 
				StatusType.Modified => "modified", 
				StatusType.Ignored => "ignored", 
				StatusType.Renamed => "renamed", 
				StatusType.TypeChanged => "typechanged", 
				StatusType.Unmerged => "modified", 
				StatusType.Untracked => "untracked", 
				StatusType.Unknown => "unknown", 
				StatusType.None => "none", 
				_ => null, 
			};
		}
	}
}
