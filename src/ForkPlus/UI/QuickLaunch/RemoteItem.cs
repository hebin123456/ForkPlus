// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage（Avalonia.Media）
// 依赖：Remote.Icon 须由 Remote.partial.cs 同步迁移为 IImage（同批次依赖，否则类型不匹配）
using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.QuickLaunch
{
	public class RemoteItem : CommandProviderItem
	{
		public override IImage Icon => Remote.Icon;

		public override IImage SelectedIcon => Remote.Icon;

		public Remote Remote { get; }

		public RemoteItem(Remote remote)
			: base(remote, remote.Name, "")
		{
			Remote = remote;
		}
	}
}
