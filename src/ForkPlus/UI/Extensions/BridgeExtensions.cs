using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ForkPlus.Services;
using ForkPlus.UI.Helpers;

// ⚠ 临时桥接线 ─ 这些扩展方法与原有命名空间相同，因此调用方无需修改 using。
// 在迁移到 Avalonia 时，直接删除此文件并在新 UI 中重写图标解析逻辑。

namespace ForkPlus.Git
{
	/// <summary>
	/// 将 ChangeType/StatusType 图标键解析为 WPF ImageSource。
	/// 迁移完成后删除此文件，改为 Avalonia 原生图标系统。
	/// </summary>
	public static class ChangeTypeBridgeExtensions
	{
		private static readonly Uri AddIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Add.png");
		private static readonly Uri EditIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Edit.png");
		private static readonly Uri CopyIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Copy.png");
		private static readonly Uri DeletedIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Remove.png");
		private static readonly Uri RenamedIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Rename.png");
		private static readonly Uri TypeChangedIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Status_Edit.png");
		private static readonly Uri UnmergedIconUrl = new Uri("pack://application:,,,/ForkPlus;component/Assets/Warning.png");

		private static readonly ImageSource AddIcon = Freeze(new BitmapImage(AddIconUrl));
		private static readonly ImageSource EditIcon = Freeze(new BitmapImage(EditIconUrl));
		private static readonly ImageSource CopyIcon = Freeze(new BitmapImage(CopyIconUrl));
		private static readonly ImageSource DeletedIcon = Freeze(new BitmapImage(DeletedIconUrl));
		private static readonly ImageSource RenamedIcon = Freeze(new BitmapImage(RenamedIconUrl));
		private static readonly ImageSource TypeChangedIcon = Freeze(new BitmapImage(TypeChangedIconUrl));
		private static readonly ImageSource UnmergedIcon = Freeze(new BitmapImage(UnmergedIconUrl));

		private static ImageSource Freeze(ImageSource source)
		{
			if (source?.CanFreeze == true) source.Freeze();
			return source;
		}

		public static ImageSource GetImageSource(this ChangeType changeType)
		{
			return changeType.GetIconKey() switch
			{
				IconKeys.StatusAdd => AddIcon,
				IconKeys.StatusCopy => CopyIcon,
				IconKeys.StatusDelete => DeletedIcon,
				IconKeys.StatusRename => RenamedIcon,
				IconKeys.StatusTypeChanged => TypeChangedIcon,
				IconKeys.StatusUnmerged => UnmergedIcon,
				_ => EditIcon,
			};
		}

		public static ImageSource GetImageSource(this StatusType statusType)
		{
			return statusType.GetIconKey() switch
			{
				IconKeys.StatusAdd => AddIcon,
				IconKeys.StatusCopy => CopyIcon,
				IconKeys.StatusDelete => DeletedIcon,
				IconKeys.StatusRename => RenamedIcon,
				IconKeys.StatusTypeChanged => TypeChangedIcon,
				IconKeys.StatusUnmerged => UnmergedIcon,
				_ => EditIcon,
			};
		}

		public static ImageSource GetConflictImageSource(this StatusType statusType)
		{
			return statusType.GetIconKey() switch
			{
				IconKeys.StatusAdd => AddIcon,
				IconKeys.StatusUnmerged => UnmergedIcon,
				_ => EditIcon,
			};
		}
	}

	/// <summary>
	/// 将 RemoteType 图标键解析为 WPF ImageSource/Geometry。
	/// 迁移完成后删除此文件。
	/// </summary>
	public static class RemoteTypeBridgeExtensions
	{
		public static ImageSource Icon(this RemoteType remoteType)
		{
			string key = remoteType.GetIconKey();
			return UI.Theme.FindImage(key) ?? UI.Theme.RemoteIcon;
		}

		public static Geometry IconGeometry(this RemoteType remoteType)
		{
			string key = remoteType.GetIconGeometryKey();
			return UI.Theme.FindGeometry(key) ?? UI.Theme.RemoteGeometry;
		}
	}

	/// <summary>
	/// 为 Remote 对象提供向后兼容的 GetIcon() / GetIconGeometry() 方法。
	/// 在 UI 层调用时替代已删除的 .Icon / .IconGeometry 实例属性。
	/// </summary>
	public static class RemoteBridgeExtensions
	{
		public static ImageSource GetIconImage(this Remote remote)
		{
			return UI.Theme.FindImage(remote.IconKey) ?? UI.Theme.RemoteIcon;
		}

		public static Geometry GetIconGeometryShape(this Remote remote)
		{
			return UI.Theme.FindGeometry(remote.IconGeometryKey) ?? UI.Theme.RemoteGeometry;
		}
	}
}

namespace ForkPlus.Accounts
{
	/// <summary>
	/// 将 GitServiceNotificationTargetType 图标键解析为 WPF ImageSource。
	/// 迁移完成后删除此文件。
	/// </summary>
	public static class NotificationIconBridgeExtensions
	{
		public static ImageSource Icon(this GitServiceNotificationTargetType targetType)
		{
			string key = targetType.GetIconKey();
			return key switch
			{
				IconKeys.NotificationCommit => UI.Theme.RevisionIcon,
				IconKeys.NotificationPullRequest => UI.Theme.PullRequestIcon,
				_ => UI.Theme.IssueIcon,
			};
		}
	}
}
