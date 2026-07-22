using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;

namespace ForkPlus.Avalonia.Controls.Editor.Merge
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Merge/MergeConflictViewExtensions.cs（34 行）：
    //   - internal static class MergeConflictViewExtensions
    //   - GetConflictedChunkAt(this MergeConflictView, int offset)：遍历 Chunks，
    //     返回 Node is ConflictChunk 且 Range.Contains(offset) 的 Chunk
    //   - AllItemSelected(this MergeConflictView.Chunk, MergeConflictPart mode)：
    //     遍历 Lines，若所有 SelectableLine 都 IsSelected 返回 true
    //
    // Avalonia 版差异：
    //   1. 纯 C# 扩展方法，无 WPF 依赖，零改动迁移
    //   2. MergeConflictView / MergeConflict / MergeConflictPart 来自 ForkPlus.Git.Merge（Core）
    //   3. namespace 改为 ForkPlus.Avalonia.Controls.Editor.Merge
    internal static class MergeConflictViewExtensions
    {
        // 对照 WPF: public static MergeConflictView.Chunk GetConflictedChunkAt(
        //           this MergeConflictView _this, int offset)
        public static MergeConflictView.Chunk GetConflictedChunkAt(this MergeConflictView _this, int offset)
        {
            MergeConflictView.Chunk[] chunks = _this.Chunks;
            foreach (MergeConflictView.Chunk chunk in chunks)
            {
                if (chunk.Node is MergeConflict.ConflictChunk && chunk.Range.Contains(offset))
                {
                    return chunk;
                }
            }
            return null;
        }

        // 对照 WPF: public static bool AllItemSelected(this MergeConflictView.Chunk _this,
        //           MergeConflictPart mode)
        public static bool AllItemSelected(this MergeConflictView.Chunk _this, MergeConflictPart mode)
        {
            MergeConflictView.Line[] lines = _this.Lines;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Node is MergeConflict.SelectableLine { IsSelected: false })
                {
                    return false;
                }
            }
            return true;
        }
    }
}
