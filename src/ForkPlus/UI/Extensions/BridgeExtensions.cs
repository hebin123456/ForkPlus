using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ForkPlus.UI.Helpers;

// ⚠ 临时桥接线 ─ 这些扩展方法与原有命名空间相同，因此调用方无需修改 using。
// 阶段 4.5：WPF ImageSource/BitmapImage/pack:// URI/Freeze → Avalonia IImage/Bitmap/avares:// URI。
// 迁移完全结束后可删除此文件，改为 Avalonia 原生图标系统。

namespace ForkPlus.Git
{
	/// <summary>
	/// 将 ChangeType/StatusType 图标键解析为 Avalonia IImage。
	/// </summary>
	public static class ChangeTypeBridgeExtensions
	{
		private static readonly Uri AddIconUrl = new Uri("avares://ForkPlus/assets/status_add.png");
		private static readonly Uri EditIconUrl = new Uri("avares://ForkPlus/assets/status_edit.png");
		private static readonly Uri CopyIconUrl = new Uri("avares://ForkPlus/assets/status_copy.png");
		private static readonly Uri DeletedIconUrl = new Uri("avares://ForkPlus/assets/status_remove.png");
		private static readonly Uri RenamedIconUrl = new Uri("avares://ForkPlus/assets/status_rename.png");
		private static readonly Uri TypeChangedIconUrl = new Uri("avares://ForkPlus/assets/status_edit.png");
		private static readonly Uri UnmergedIconUrl = new Uri("avares://ForkPlus/assets/warning.png");

		private static readonly IImage AddIcon = LoadBitmap(AddIconUrl);
		private static readonly IImage EditIcon = LoadBitmap(EditIconUrl);
		private static readonly IImage CopyIcon = LoadBitmap(CopyIconUrl);
		private static readonly IImage DeletedIcon = LoadBitmap(DeletedIconUrl);
		private static readonly IImage RenamedIcon = LoadBitmap(RenamedIconUrl);
		private static readonly IImage TypeChangedIcon = LoadBitmap(TypeChangedIconUrl);
		private static readonly IImage UnmergedIcon = LoadBitmap(UnmergedIconUrl);

		private static IImage LoadBitmap(Uri assetUri)
		{
			// Avalonia Bitmap 构造后即不可变，无需 Freeze。
			using (Stream stream = AssetLoader.Open(assetUri))
			{
				return new Bitmap(stream);
			}
		}

		public static IImage GetImageSource(this ChangeType changeType)
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

		public static IImage GetImageSource(this StatusType statusType)
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

		public static IImage GetConflictImageSource(this StatusType statusType)
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
	/// 将 RemoteType 图标键解析为 Avalonia IImage/Geometry。
	/// </summary>
	public static class RemoteTypeBridgeExtensions
	{
		public static IImage Icon(this RemoteType remoteType)
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
		public static IImage GetIconImage(this Remote remote)
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
	/// 将 GitServiceNotificationTargetType 图标键解析为 Avalonia IImage。
	/// </summary>
	public static class NotificationIconBridgeExtensions
	{
		public static IImage Icon(this GitServiceNotificationTargetType targetType)
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
