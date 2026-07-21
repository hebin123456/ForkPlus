using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ReferencePanelTagViewModel（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ReferencePanelTagViewModel.cs（16 行）：
    //   - WPF ReferencePanelTagViewModel : ReferencePanelReferenceViewModel
    //   - 字段：_tag（Tag）
    //   - override Name => _tag.Name
    //   - 构造函数接收 Tag
    //
    // Avalonia 版差异（spike 简化策略，task spec：POCO 类）：
    //   1. WPF ReferencePanelReferenceViewModel 基类 → Avalonia 同名基类
    //   2. WPF Tag（ForkPlus.Git.Core）→ 同（无 UI 依赖）
    //   3. spike 保持 POCO 形状（无 Avalonia 依赖，纯数据容器）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ReferencePanelReferenceViewModel
    //   - override Name => _tag.Name
    //   - 构造函数接收 Tag
    public class ReferencePanelTagViewModel : ReferencePanelReferenceViewModel
    {
        // 对照 WPF: private Tag _tag
        private readonly Tag _tag;

        // 对照 WPF: public override string Name => _tag.Name
        public override string Name => _tag.Name;

        // 对照 WPF: public ReferencePanelTagViewModel(Tag tag)
        public ReferencePanelTagViewModel(Tag tag)
        {
            _tag = tag;
        }
    }
}
