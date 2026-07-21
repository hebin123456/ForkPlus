using Avalonia;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 BtLayoutTreemapResultExtensions（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/BtLayoutTreemapResultExtensions.cs（15 行）：
    //   - WPF BtLayoutTreemapResultExtensions（internal static 类，BtLayoutTreemapResult ref 扩展）
    //   - Into(this ref BtLayoutTreemapResult) → GitCommandResult<(int, Rect)[]>
    //     将 biturbo native 返回的 BtTreemapItem[] 转换为 (int, Rect)[] 元组数组：
    //     - int = btTreemapItem.index（节点索引）
    //     - Rect = new Rect(x, y, w, h)（布局矩形，来自 BtRect）
    //   - 用 GetStructArray<TSource, TResult> 扩展（BiturboExtensions）遍历 native 内存
    //   - 被 Treemap.CalculateLayout 调用：BtRequest.Run(() => default(BtLayoutTreemapResult), ...)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Rect → Avalonia.Rect（构造函数签名一致：x, y, width, height）
    //   2. WPF internal static class → spike public static class（供外部 TreemapDelegate 使用）
    //   3. ref this 扩展方法保持（C# 7.2+ 支持）
    //   4. spike Treemap 用纯 C# slice-and-dice 算法（跳过 biturbo native），
    //      但保留此扩展供未来接入 biturbo native 布局时使用
    //   5. ValueTuple 命名参数（item1:/item2:）改用普通元组构造（更清晰）
    //
    // spike 简化（task spec：静态类 + 扩展方法）：
    //   - Into(ref BtLayoutTreemapResult) 扩展方法（与 WPF 一致，Rect 改 Avalonia）
    public static class BtLayoutTreemapResultExtensions
    {
        // 对照 WPF: public static GitCommandResult<(int, Rect)[]> Into(this ref BtLayoutTreemapResult btLayoutTreemapResult)
        public static GitCommandResult<(int, Rect)[]> Into(this ref BtLayoutTreemapResult btLayoutTreemapResult)
        {
            // 对照 WPF: btLayoutTreemapResult.items.GetStructArray(btLayoutTreemapResult.items_len, ...)
            //   BtTreemapItem → (int index, Rect rect)
            //   Rect = new Rect(btTreemapItem.rect.x, btTreemapItem.rect.y, btTreemapItem.rect.w, btTreemapItem.rect.h)
            (int, Rect)[] result = btLayoutTreemapResult.items.GetStructArray(
                btLayoutTreemapResult.items_len,
                (BtTreemapItem btTreemapItem) =>
                {
                    // spike: Avalonia.Rect 构造 (x, y, width, height)
                    // 对照 WPF: new Rect(btTreemapItem.rect.x, btTreemapItem.rect.y, btTreemapItem.rect.w, btTreemapItem.rect.h)
                    Rect rect = new Rect(btTreemapItem.rect.x, btTreemapItem.rect.y, btTreemapItem.rect.w, btTreemapItem.rect.h);
                    return ((int)btTreemapItem.index, rect);
                });
            return GitCommandResult<(int, Rect)[]>.Success(result);
        }
    }
}
