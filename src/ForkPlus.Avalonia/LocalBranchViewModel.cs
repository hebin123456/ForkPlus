using ForkPlus.Git;

// Avalonia spike 版 LocalBranchViewModel（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/LocalBranchViewModel.cs（25 行）：
//   - WPF: public class LocalBranchViewModel : BranchViewModel
//   - Reference Reference => _localBranch
//   - string Name => _localBranch.Name
//   - bool IsActive => _localBranch.IsActive
//   - bool IsWorktree => _isWorktree
//   - 构造 LocalBranchViewModel(int graphColumn, LocalBranch localBranch, bool isWorktree)
//   - 依赖：ForkPlus.Git.LocalBranch（Core 可用）
//
// Avalonia 版差异：
//   1. POCO 无 WPF 依赖，零改动复用
//   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//
// spike 简化：
//   - 与 WPF 完全一致的 POCO 类
namespace ForkPlus.Avalonia
{
    public class LocalBranchViewModel : BranchViewModel
    {
        private readonly LocalBranch _localBranch;
        private readonly bool _isWorktree;

        public override Reference Reference => _localBranch;
        public string Name => _localBranch.Name;
        public bool IsActive => _localBranch.IsActive;
        public bool IsWorktree => _isWorktree;

        public LocalBranchViewModel(int graphColumn, LocalBranch localBranch, bool isWorktree)
            : base(graphColumn)
        {
            _localBranch = localBranch;
            _isWorktree = isWorktree;
        }
    }
}
