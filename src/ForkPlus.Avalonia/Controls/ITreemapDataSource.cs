namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ITreemapDataSource（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ITreemapDataSource.cs（13 行）：
    //   - WPF ITreemapDataSource 接口（Treemap 数据源抽象）
    //   - GetRootItems() → object（根节点集合）
    //   - GetItemChildren(object array, int index) → object（子节点集合）
    //   - GetItemChildrenCount(object array, int? index) → int?（子节点数，null=叶子）
    //   - GetItemSizeValue(object array, int index) → long（节点大小，用于 treemap 面积分配）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 接口签名与 WPF 完全一致（纯数据抽象，无 UI 依赖）
    //   2. object 类型承载节点集合（spike 不约束泛型，与 WPF 一致）
    //   3. spike 零改动复用（接口无 WPF/Avalonia 差异）
    //
    // spike 简化（task spec：接口）：
    //   - 4 个方法签名与 WPF 一致
    public interface ITreemapDataSource
    {
        // 对照 WPF: object GetRootItems()
        object GetRootItems();

        // 对照 WPF: object GetItemChildren(object array, int index)
        object GetItemChildren(object array, int index);

        // 对照 WPF: int? GetItemChildrenCount(object array, int? index)
        int? GetItemChildrenCount(object array, int? index);

        // 对照 WPF: long GetItemSizeValue(object array, int index)
        long GetItemSizeValue(object array, int index);
    }
}
