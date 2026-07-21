namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 TreemapDataSourceExtensions（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/TreemapDataSourceExtensions.cs（73 行）：
    //   - WPF TreemapDataSourceExtensions（静态类，ITreemapDataSource 扩展方法）
    //   - GetItemValue(IndexPath indexPath) → long?
    //     按 indexPath 路径遍历 dataSource：每层 GetItemSizeValue + GetItemChildren
    //   - FirstVisualItem() → IndexPath
    //     递归找最大 size 的子项路径（MaxItemIndex → GetItemChildren → 递归）
    //   - MaxItemIndex(object folder) → int?
    //     遍历所有子项，找 GetItemSizeValue 最大的索引
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 扩展方法签名与 WPF 完全一致（纯数据逻辑，无 UI 依赖）
    //   2. spike 零改动复用（扩展方法无 WPF/Avalonia 差异）
    //   3. Treemap.IndexPath 引用同命名空间 spike 版（Controls/Treemap.cs）
    //
    // spike 简化（task spec：静态类 + 扩展方法）：
    //   - 3 个扩展方法与 WPF 一致
    public static class TreemapDataSourceExtensions
    {
        // 对照 WPF: public static long? GetItemValue(this ITreemapDataSource dataSource, Treemap.IndexPath indexPath)
        public static long? GetItemValue(this ITreemapDataSource dataSource, Treemap.IndexPath indexPath)
        {
            long? result = null;
            object array = dataSource.GetRootItems();
            for (int i = 0; i < indexPath.Count; i++)
            {
                int index = indexPath[i];
                if (i == 0)
                {
                    result = dataSource.GetItemSizeValue(array, index);
                    array = dataSource.GetItemChildren(array, index);
                    continue;
                }
                result = dataSource.GetItemSizeValue(array, index);
                if (i != indexPath.Count - 1)
                {
                    array = dataSource.GetItemChildren(array, index);
                }
            }
            return result;
        }

        // 对照 WPF: public static Treemap.IndexPath FirstVisualItem(this ITreemapDataSource dataSource)
        public static Treemap.IndexPath FirstVisualItem(this ITreemapDataSource dataSource)
        {
            Treemap.IndexPath indexPath = new Treemap.IndexPath();
            object obj = dataSource.GetRootItems();
            while (true)
            {
                int? num = dataSource.MaxItemIndex(obj);
                if (!num.HasValue)
                {
                    break;
                }
                int valueOrDefault = num.GetValueOrDefault();
                indexPath.Add(valueOrDefault);
                if (!(dataSource.GetItemChildrenCount(obj, valueOrDefault) is int))
                {
                    break;
                }
                obj = dataSource.GetItemChildren(obj, valueOrDefault);
            }
            return indexPath;
        }

        // 对照 WPF: public static int? MaxItemIndex(this ITreemapDataSource dataSource, object folder)
        public static int? MaxItemIndex(this ITreemapDataSource dataSource, object folder)
        {
            int? itemChildrenCount = dataSource.GetItemChildrenCount(folder, null);
            if (!itemChildrenCount.HasValue || itemChildrenCount == 0)
            {
                return null;
            }
            int num = 0;
            long num2 = dataSource.GetItemSizeValue(folder, num);
            for (int i = 1; i < itemChildrenCount; i++)
            {
                long itemSizeValue = dataSource.GetItemSizeValue(folder, i);
                if (itemSizeValue > num2)
                {
                    num2 = itemSizeValue;
                    num = i;
                }
            }
            return num;
        }
    }
}
