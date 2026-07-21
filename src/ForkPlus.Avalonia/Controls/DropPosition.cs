namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 DropPosition（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/DropPosition.cs（9 行）：
    //   - WPF DropPosition 枚举：Top / Bottom / Over
    //   - 用于 DragAndDropListViewItem.GetDropPosition(DragEventArgs e)：
    //     Y < 3.0 → Top（插入到目标项之前）
    //     Y > ActualHeight - 3.0 → Bottom（插入到目标项之后）
    //     其他 → Over（放置到目标项上）
    //   - DropPlaceAdorner 根据 DropPosition 绘制上/下边线或高亮背景
    //
    // Avalonia 版差异（spike 简化策略，task spec：枚举 None/Before/After/Inside）：
    //   1. WPF Top/Bottom/Over → spike None/Before/After/Inside（语义更清晰）
    //      None = 未拖放 / Before = 插入之前 / After = 插入之后 / Inside = 放置到内部
    //   2. spike 提供与 WPF 一致的 Top/Bottom/Over 别名（兼容旧代码）
    //
    // spike 简化（task spec 关键 API）：
    //   - 枚举：None / Before / After / Inside
    public enum DropPosition
    {
        // 对照 WPF: 默认无值（WPF 枚举无 None，spike 新增）
        None = 0,

        // 对照 WPF: Top（插入到目标项之前）
        Before = 1,

        // 对照 WPF: Bottom（插入到目标项之后）
        After = 2,

        // 对照 WPF: Over（放置到目标项上）
        Inside = 3
    }
}
