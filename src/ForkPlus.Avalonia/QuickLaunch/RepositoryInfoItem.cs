// RepositoryInfoItem.cs：仓库信息条目（POCO）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/RepositoryInfoItem.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class RepositoryInfoItem : CommandProviderItem
//   - override ImageSource Icon => Application.Current.TryFindResource("RepositoryIcon") as ImageSource
//   - override ImageSource SelectedIcon => Application.Current.TryFindResource("RepositoryEmphasizedIcon") as ImageSource
//   - RepositoryManager.Repository Repository { get; }
//   - 构造函数：base(repository, repository.Name(), repository.Path)
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ImageSource → IImage（Avalonia.Media.IImage）
//   3. Application.Current.TryFindResource(key) as ImageSource → GetIconResource(key)
//   4. RepositoryManager.Repository 从 ForkPlus（WPF 工程）→ spike POCO（同命名空间，见 SpikeTypes.cs）

using Avalonia.Media;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class RepositoryInfoItem : CommandProviderItem
    {
        // 对照 WPF: public override ImageSource Icon => Application.Current.TryFindResource("RepositoryIcon") as ImageSource
        public override IImage Icon => GetIconResource("RepositoryIcon");

        // 对照 WPF: public override ImageSource SelectedIcon => Application.Current.TryFindResource("RepositoryEmphasizedIcon") as ImageSource
        public override IImage SelectedIcon => GetIconResource("RepositoryEmphasizedIcon");

        // 对照 WPF: public RepositoryManager.Repository Repository { get; }
        // spike: RepositoryManager.Repository 为同命名空间 spike POCO
        public RepositoryManager.Repository Repository { get; }

        public RepositoryInfoItem(RepositoryManager.Repository repository)
            : base(repository, repository.GetDisplayName(), repository.Path)
        {
            Repository = repository;
        }
    }
}
