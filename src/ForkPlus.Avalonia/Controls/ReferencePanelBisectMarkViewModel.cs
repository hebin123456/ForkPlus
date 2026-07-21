using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ReferencePanelBisectMarkViewModel（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ReferencePanelBisectMarkViewModel.cs（16 行）：
    //   - WPF ReferencePanelBisectMarkViewModel : ReferencePanelReferenceViewModel
    //   - 字段：_bisectMark（BisectMark）
    //   - override Name => "bisect: " + _bisectMark.ShortName
    //   - 构造函数接收 BisectMark
    //
    // Avalonia 版差异（spike 简化策略，task spec：POCO 类）：
    //   1. WPF ReferencePanelReferenceViewModel 基类 → Avalonia 同名基类
    //   2. WPF BisectMark（ForkPlus.Git.Core）→ 同（无 UI 依赖）
    //   3. spike 保持 POCO 形状（无 Avalonia 依赖，纯数据容器）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ReferencePanelReferenceViewModel
    //   - override Name => "bisect: " + _bisectMark.ShortName
    //   - 构造函数接收 BisectMark
    public class ReferencePanelBisectMarkViewModel : ReferencePanelReferenceViewModel
    {
        // 对照 WPF: private readonly BisectMark _bisectMark
        private readonly BisectMark _bisectMark;

        // 对照 WPF: public override string Name => "bisect: " + _bisectMark.ShortName
        public override string Name => "bisect: " + _bisectMark.ShortName;

        // 对照 WPF: public ReferencePanelBisectMarkViewModel(BisectMark bisectMark)
        public ReferencePanelBisectMarkViewModel(BisectMark bisectMark)
        {
            _bisectMark = bisectMark;
        }
    }
}
