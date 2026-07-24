// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - using System.Windows.Media.Imaging → using Avalonia.Media.Imaging
// - 新增 using Avalonia.Platform（AssetLoader）+ using System.IO（Stream）
// - ImageSource → IImage（Avalonia.Media）
// - BitmapImage → Avalonia.Media.Imaging.Bitmap
// - pack://application:,,,/ForkPlus;component/Assets/x.png → avares://ForkPlus/Assets/x.png
// - BitmapImage(uri) + Freeze → AssetLoader.Open(uri) + new Bitmap(stream)（Avalonia Bitmap 不可变，无需 Freeze，参考 AvatarManager/FileListUserControl）
// - Geometry 解析为 Avalonia.Media.Geometry（Theme.FindGeometry 已返回 Avalonia Geometry）
// - Theme.FindImage 已返回 IImage，TryFindResource 调用保持不变
using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ForkPlus.Services;
using ForkPlus.UI.Helpers;

// ⚠ 临时桥接线 ─ 这些扩展方法与原有命名空间相同，因此调用方无需修改 using。
// 在迁移到 Avalonia 时，直接删除此文件并在新 UI 中重写图标解析逻辑。

namespace ForkPlus.Git
{
	/// <summary>
	/// 将 ChangeType/StatusType 图标键解析为 Avalonia IImage。
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

		private static readonly IImage AddIcon = LoadBitmap(AddIconUrl);
		private static readonly IImage EditIcon = LoadBitmap(EditIconUrl);
		private static readonly IImage CopyIcon = LoadBitmap(CopyIconUrl);
		private static readonly IImage DeletedIcon = LoadBitmap(DeletedIconUrl);
		private static readonly IImage RenamedIcon = LoadBitmap(RenamedIconUrl);
		private static readonly IImage TypeChangedIcon = LoadBitmap(TypeChangedIconUrl);
		private static readonly IImage UnmergedIcon = LoadBitmap(UnmergedIconUrl);

		// 阶段 4.5：WPF BitmapImage(uri) + Freeze → Avalonia AssetLoader.Open(uri) + new Bitmap(stream)。
		// Avalonia Bitmap 构造时自动解码并归一化格式，且不可变，无需 Freeze（参考 AvatarManager.LoadBitmapFromAsset）。
		private static IImage LoadBitmap(Uri assetUri)
		{
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
	/// 迁移完成后删除此文件。
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
	/// 迁移完成后删除此文件。
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
