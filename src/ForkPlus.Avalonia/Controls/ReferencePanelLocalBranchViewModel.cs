using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ReferencePanelLocalBranchViewModel（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ReferencePanelLocalBranchViewModel.cs（16 行）：
    //   - WPF ReferencePanelLocalBranchViewModel : ReferencePanelReferenceViewModel
    //   - 字段：_localBranch（LocalBranch）
    //   - override Name => _localBranch.Name
    //   - 构造函数接收 LocalBranch
    //
    // Avalonia 版差异（spike 简化策略，task spec：POCO 类）：
    //   1. WPF ReferencePanelReferenceViewModel 基类 → Avalonia 同名基类
    //   2. WPF LocalBranch（ForkPlus.Git.Core）→ 同（无 UI 依赖）
    //   3. spike 保持 POCO 形状（无 Avalonia 依赖，纯数据容器）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ReferencePanelReferenceViewModel
    //   - override Name => _localBranch.Name
    //   - 构造函数接收 LocalBranch
    public class ReferencePanelLocalBranchViewModel : ReferencePanelReferenceViewModel
    {
        // 对照 WPF: private LocalBranch _localBranch
        private readonly LocalBranch _localBranch;

        // 对照 WPF: public override string Name => _localBranch.Name
        public override string Name => _localBranch.Name;

        // 对照 WPF: public ReferencePanelLocalBranchViewModel(LocalBranch localBranch)
        public ReferencePanelLocalBranchViewModel(LocalBranch localBranch)
        {
            _localBranch = localBranch;
        }
    }
}
