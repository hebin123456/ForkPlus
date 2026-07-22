using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;
using ForkPlus.UI;
using AvaloniaEdit.Rendering;

namespace ForkPlus.Avalonia.Controls.Editor.Merge
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Merge/MergeCodeEditor.cs（224 行）：
    //   - public class MergeCodeEditor : CodeEditor
    //   - 嵌套 struct Block（Start / Length / Kind: Resolved|Unresolved）
    //   - 常量：SrcBlockPathName / DstBlockPathName（WPF TemplatePart Path 名称）
    //   - 字段：_backgroundColorizer / _lineNumberMargin / _mergeChunkSelectionLayer /
    //     _showScrollbarMap / _refreshUI / _blocks / _mergeConflictView
    //   - 属性：Theme / ViewMode / MergeConflictView
    //   - 事件：MergeLineAdded / MergeLineRemoved / MergeChunkAdded / MergeChunkRemoved
    //   - 构造函数：SetResourceReference(StyleProperty) + 创建 3 个辅助对象 +
    //     TextView.InsertLayer(_mergeChunkSelectionLayer) + BackgroundRenderers.Add +
    //     LeftMargins.Add
    //   - SetMergeConflictView：更新 line numbers / Text / InvalidateVisual / RefreshScrollbarMap
    //   - InvalidateMargin / OnMergeLineAdded / OnMergeLineRemoved / OnMergeChunkAdded /
    //     OnMergeChunkRemoved（事件触发器）
    //   - OnRenderSizeChanged → RefreshScrollbarMap
    //   - RefreshScrollbarMap：用 StreamGeometry 画 src/dst diff map（WPF-only Path TemplatePart）
    //   - CreateBlocks：根据 ConflictChunk 的 SelectedLine 数量创建 Block 数组
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 继承本工程 CodeEditor（src/ForkPlus.Avalonia/Controls/Editor/CodeEditor.cs）
    //   2. WPF SetResourceReference(StyleProperty) → spike 移除（Avalonia 用 Style/ControlTheme，
    //      spike 阶段用默认样式）
    //   3. WPF TextView.InsertLayer(_mergeChunkSelectionLayer, KnownLayer.Selection, ...) →
    //      spike 移除（AvaloniaEdit InsertLayer 需 ILayer 接口，ChunkSelectionLayer 是 Control
    //      不是 ILayer，phase 3.9b 需改为 IBackgroundRenderer 或自定义 ILayer 实现）
    //   4. WPF RefreshScrollbarMap（StreamGeometry + Path TemplatePart）→ spike 移除
    //      （Avalonia StreamGeometry API 不同，phase 3.9b 用 Avalonia StreamGeometry 重写）
    //   5. WPF OnRenderSizeChanged(SizeChangedInfo) → Avalonia SizeChanged 事件
    //   6. WPF EventArgs<T> → System.EventArgs（spike 用 EventHandler<T> 标准委托）
    //   7. WPF Template.TryFindName<Path> → spike 移除（无 WPF Template）
    //   8. namespace 改为 ForkPlus.Avalonia.Controls.Editor.Merge
    //
    // spike 简化（task spec：复杂渲染逻辑可简化为空实现 + 注释）：
    //   - 保留 3 个辅助对象创建（MergeChunkSelectionLayer / MergeCodeEditorBackgroundColorizer /
    //     MergeLineNumberMargin），但不插入 TextView 层（phase 3.9b 接入）
    //   - 保留 SetMergeConflictView / InvalidateMargin / OnMergeXxx 事件触发器
    //   - RefreshScrollbarMap / CreateBlocks / OnRenderSizeChanged → 空实现 + 注释
    public class MergeCodeEditor : CodeEditor
    {
        // 对照 WPF: private struct Block { enum BlockKind { Resolved, Unresolved } ... }
        // spike 版：保留 struct 定义（CreateBlocks 用到，spike 阶段 CreateBlocks 空实现）
        private struct Block
        {
            public enum BlockKind
            {
                Resolved,
                Unresolved
            }

            public double Start { get; }
            public double Length { get; }
            public BlockKind Kind { get; }

            public Block(double start, double length, BlockKind kind)
            {
                Start = start;
                Length = length;
                Kind = kind;
            }
        }

        // 对照 WPF: private MergeCodeEditorBackgroundColorizer _backgroundColorizer;
        private readonly MergeCodeEditorBackgroundColorizer _backgroundColorizer;

        // 对照 WPF: private MergeLineNumberMargin _lineNumberMargin;
        private readonly MergeLineNumberMargin _lineNumberMargin;

        // 对照 WPF: private MergeChunkSelectionLayer _mergeChunkSelectionLayer;
        private readonly MergeChunkSelectionLayer _mergeChunkSelectionLayer;

        // 对照 WPF: private bool _showScrollbarMap;
        private bool _showScrollbarMap;

        // 对照 WPF: private bool _refreshUI;
        private bool _refreshUI;

        // 对照 WPF: private Block[] _blocks;
        private Block[] _blocks;

        // 对照 WPF: private MergeConflictView _mergeConflictView;
        private MergeConflictView _mergeConflictView;

        // 对照 WPF: public ThemeType Theme { get; }
        public ThemeType Theme { get; }

        // 对照 WPF: public MergeConflictPart ViewMode { get; set; }
        public MergeConflictPart ViewMode { get; set; }

        // 对照 WPF: public MergeConflictView MergeConflictView { get; private set; }
        public MergeConflictView MergeConflictView
        {
            get => _mergeConflictView;
            private set
            {
                if (_mergeConflictView != value)
                {
                    _mergeConflictView = value;
                }
            }
        }

        // 对照 WPF: public event EventHandler<EventArgs<int>> MergeLineAdded;
        public event EventHandler<int> MergeLineAdded;

        // 对照 WPF: public event EventHandler<EventArgs<int>> MergeLineRemoved;
        public event EventHandler<int> MergeLineRemoved;

        // 对照 WPF: public event EventHandler<EventArgs<MergeConflictView.Chunk>> MergeChunkAdded;
        public event EventHandler<MergeConflictView.Chunk> MergeChunkAdded;

        // 对照 WPF: public event EventHandler<EventArgs<MergeConflictView.Chunk>> MergeChunkRemoved;
        public event EventHandler<MergeConflictView.Chunk> MergeChunkRemoved;

        // 对照 WPF: public MergeCodeEditor()
        public MergeCodeEditor()
        {
            // 对照 WPF: SetResourceReference(FrameworkElement.StyleProperty, typeof(CodeEditor));
            // spike 移除（Avalonia 用 ControlTheme，spike 阶段用默认样式）

            Theme = ForkPlusSettings.Default.Theme;

            // 对照 WPF: _mergeChunkSelectionLayer = new MergeChunkSelectionLayer(this);
            //           base.TextArea.TextView.InsertLayer(_mergeChunkSelectionLayer,
            //           KnownLayer.Selection, LayerInsertionPosition.Above);
            // spike 版：创建对象但不插入 TextView 层（phase 3.9b 需改为 IBackgroundRenderer）
            _mergeChunkSelectionLayer = new MergeChunkSelectionLayer(this);

            // 对照 WPF: _backgroundColorizer = new MergeCodeEditorBackgroundColorizer(this);
            //           base.TextArea.TextView.BackgroundRenderers.Add(_backgroundColorizer);
            _backgroundColorizer = new MergeCodeEditorBackgroundColorizer(this);
            TextArea.TextView.BackgroundRenderers.Add(_backgroundColorizer);

            // 对照 WPF: _lineNumberMargin = new MergeLineNumberMargin(this);
            //           base.TextArea.LeftMargins.Add(_lineNumberMargin);
            _lineNumberMargin = new MergeLineNumberMargin(this);
            TextArea.LeftMargins.Add(_lineNumberMargin);
        }

        // 对照 WPF: public void SetMergeConflictView(MergeConflictView mergeConflictView,
        //           bool refreshUI, bool showScrollbarMap = false)
        public void SetMergeConflictView(MergeConflictView mergeConflictView, bool refreshUI, bool showScrollbarMap = false)
        {
            _refreshUI = refreshUI;
            _showScrollbarMap = showScrollbarMap;
            MergeConflictView = mergeConflictView;
            if (refreshUI)
            {
                _lineNumberMargin.UpdateLineNumbersData(_mergeConflictView);
                Text = _mergeConflictView?.StringValue ?? string.Empty;
                InvalidateVisual();
                if (showScrollbarMap)
                {
                    _blocks = CreateBlocks(mergeConflictView.Chunks);
                    RefreshScrollbarMap();
                }
            }
        }

        // 对照 WPF: public void InvalidateMargin()
        public void InvalidateMargin()
        {
            _lineNumberMargin.InvalidateVisual();
        }

        // 对照 WPF: public void OnMergeLineAdded(int lineNumber)
        public void OnMergeLineAdded(int lineNumber)
        {
            MergeLineAdded?.Invoke(this, lineNumber);
        }

        // 对照 WPF: public void OnMergeLineRemoved(int lineNumber)
        public void OnMergeLineRemoved(int lineNumber)
        {
            MergeLineRemoved?.Invoke(this, lineNumber);
        }

        // 对照 WPF: public void OnMergeChunkAdded(MergeConflictView.Chunk chunk)
        public void OnMergeChunkAdded(MergeConflictView.Chunk chunk)
        {
            MergeChunkAdded?.Invoke(this, chunk);
        }

        // 对照 WPF: public void OnMergeChunkRemoved(MergeConflictView.Chunk chunk)
        public void OnMergeChunkRemoved(MergeConflictView.Chunk chunk)
        {
            MergeChunkRemoved?.Invoke(this, chunk);
        }

        // 对照 WPF: protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        // spike 版：用 Avalonia SizeChanged 事件替代（spike 阶段不订阅，RefreshScrollbarMap 空实现）
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            if (_refreshUI && _showScrollbarMap)
            {
                RefreshScrollbarMap();
            }
        }

        // 对照 WPF: private void RefreshScrollbarMap()
        // spike 版：空实现（WPF StreamGeometry + Path TemplatePart，phase 3.9b 用 Avalonia 重写）
        private void RefreshScrollbarMap()
        {
            // Phase 3.9b 在此补：
            //   - 用 Avalonia StreamGeometry + StreamGeometryContext 画 src/dst diff map
            //   - 用 Avalonia Controls.Path 替代 WPF TemplatePart Path
            //   - 遍历 _blocks 画 Resolved / Unresolved 矩形
        }

        // 对照 WPF: private static Block[] CreateBlocks(MergeConflictView.Chunk[] chunks)
        // spike 版：返回空数组（依赖 ConflictChunk.RemoteLines/LocalLines.SelectableLine.IsSelected，
        // phase 3.9b 补完整逻辑）
        private static Block[] CreateBlocks(MergeConflictView.Chunk[] chunks)
        {
            // Phase 3.9b 在此补：
            //   - 遍历 chunks，对 ConflictChunk 统计 SelectedLine 数量
            //   - 按 Resolved/Unresolved 创建 Block
            return Array.Empty<Block>();
        }
    }
}
