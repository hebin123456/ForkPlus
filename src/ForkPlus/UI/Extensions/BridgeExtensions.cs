using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
		private static readonly Uri AddIconUrl = new Uri("avares://ForkPlus/Assets/Status_Add.png");
		private static readonly Uri EditIconUrl = new Uri("avares://ForkPlus/Assets/Status_Edit.png");
		private static readonly Uri CopyIconUrl = new Uri("avares://ForkPlus/Assets/Status_Copy.png");
		private static readonly Uri DeletedIconUrl = new Uri("avares://ForkPlus/Assets/Status_Remove.png");
		private static readonly Uri RenamedIconUrl = new Uri("avares://ForkPlus/Assets/Status_Rename.png");
		private static readonly Uri TypeChangedIconUrl = new Uri("avares://ForkPlus/Assets/Status_Edit.png");
		private static readonly Uri UnmergedIconUrl = new Uri("avares://ForkPlus/Assets/Warning.png");

		private static readonly global::Avalonia.Media.IImage AddIcon = Freeze(new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(AddIconUrl)));
		private static readonly global::Avalonia.Media.IImage EditIcon = Freeze(new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(EditIconUrl)));
		private static readonly global::Avalonia.Media.IImage CopyIcon = Freeze(new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(CopyIconUrl)));
		private static readonly global::Avalonia.Media.IImage DeletedIcon = Freeze(new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(DeletedIconUrl)));
		private static readonly global::Avalonia.Media.IImage RenamedIcon = Freeze(new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(RenamedIconUrl)));
		private static readonly global::Avalonia.Media.IImage TypeChangedIcon = Freeze(new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(TypeChangedIconUrl)));
		private static readonly global::Avalonia.Media.IImage UnmergedIcon = Freeze(new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(UnmergedIconUrl)));

		private static global::Avalonia.Media.IImage Freeze(global::Avalonia.Media.IImage source)
		{
			// Migration note：WPF Freezable.CanFreeze/Freeze 在 Avalonia 无对应（Bitmap 天然不可变），直接返回。
			return source;
		}

		public static global::Avalonia.Media.IImage GetImageSource(this ChangeType changeType)
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

		public static global::Avalonia.Media.IImage GetImageSource(this StatusType statusType)
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

		public static global::Avalonia.Media.IImage GetConflictImageSource(this StatusType statusType)
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
		public static global::Avalonia.Media.IImage Icon(this RemoteType remoteType)
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
		public static global::Avalonia.Media.IImage GetIconImage(this Remote remote)
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
		public static global::Avalonia.Media.IImage Icon(this GitServiceNotificationTargetType targetType)
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
