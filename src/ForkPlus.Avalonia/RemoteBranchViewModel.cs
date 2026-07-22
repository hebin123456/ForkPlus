using ForkPlus.Git;

// Avalonia spike 版 RemoteBranchViewModel（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/RemoteBranchViewModel.cs（24 行）：
//   - WPF: public class RemoteBranchViewModel : BranchViewModel
//   - Reference Reference => RemoteBranch
//   - string Name => RemoteBranch.Name
//   - RemoteBranch RemoteBranch { get; }
//   - ImageSource RemoteIcon { get; }（WPF System.Windows.Media.ImageSource）
//   - bool HasDownstream { get; set; }
//   - 构造 RemoteBranchViewModel(int graphColumn, RemoteBranch remoteBranch, ImageSource remoteIcon)
//   - 依赖：ForkPlus.Git.RemoteBranch（Core 可用）/ System.Windows.Media.ImageSource（WPF-only）
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ImageSource → spike 用 object 替代（避免 Avalonia.Media.IImage 依赖）
//   2. POCO 其余无 WPF 依赖，零改动复用
//   3. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//
// spike 简化：
//   - RemoteIcon 用 object 替代 ImageSource
namespace ForkPlus.Avalonia
{
    public class RemoteBranchViewModel : BranchViewModel
    {
        public override Reference Reference => RemoteBranch;
        public string Name => RemoteBranch.Name;
        public RemoteBranch RemoteBranch { get; }
        public object RemoteIcon { get; }
        public bool HasDownstream { get; set; }

        public RemoteBranchViewModel(int graphColumn, RemoteBranch remoteBranch, object remoteIcon)
            : base(graphColumn)
        {
            RemoteBranch = remoteBranch;
            RemoteIcon = remoteIcon;
        }
    }
}
