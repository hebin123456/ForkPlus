using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.9a：Avalonia 版 CommitFileDiffControl 骨架（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/CommitFileDiffControl.cs（继承 FileDiffControl）：
    //   - 多了 chunk 级 patch apply 事件（ToggleStage / Stage / UnStage / Discard）
    //   - 依赖 CommitCodeEditor.CreatePatchForSelection（AvalonEdit 衍生 API）
    //   - 用于 CommitUserControl.xaml Row 2 Col 2 Row 0（commit 视图 diff 区）
    //
    // spike 阶段：继承 FileDiffControl.axaml 的 Grid 2 行布局，只增加 chunk stage/unstage 事件占位。
    //
    // 本 spike 版暂不迁移：
    //   - ToggleStage / Stage / UnStage / Discard chunk 级 patch apply 真实逻辑
    //   - CommitCodeEditor.CreatePatchForSelection（AvalonEdit 衍生 API，Phase 3.9b）
    //   - chunk 高亮 / stage 拖拽手势
    //
    // 本 spike 版验证：
    //   - 继承 FileDiffControl 的 Grid 2 行布局正确显示
    //   - 占位 sub-view 文字可切换
    public partial class CommitFileDiffControl : FileDiffControl
    {
        // ===== chunk 级 patch apply 事件占位（对照 WPF）=====
        // spike 版只声明不触发，真实逻辑（CreatePatchForSelection + git apply --cached）留待 Phase 3.9b
        public event EventHandler<EventArgs> ToggleStage;
        public event EventHandler<EventArgs> Stage;
        public event EventHandler<EventArgs> UnStage;
        public event EventHandler<EventArgs> Discard;

        public CommitFileDiffControl()
        {
            InitializeComponent();
        }

        // ===== chunk 级 patch apply 公共方法占位（对照 WPF）=====

        // 对照 WPF: ToggleStageSelection（按 Selection 计算 patch，切换 stage/unstage）
        public void ToggleStageSelection()
        {
            Console.WriteLine("[CommitFileDiffControl] ToggleStageSelection (spike placeholder)");
            ToggleStage?.Invoke(this, EventArgs.Empty);
        }

        // 对照 WPF: StageSelection（按 Selection 计算 patch，git apply --cached）
        public void StageSelection()
        {
            Console.WriteLine("[CommitFileDiffControl] StageSelection (spike placeholder)");
            Stage?.Invoke(this, EventArgs.Empty);
        }

        // 对照 WPF: UnStageSelection（按 Selection 计算 patch，git reset REVERSE）
        public void UnStageSelection()
        {
            Console.WriteLine("[CommitFileDiffControl] UnStageSelection (spike placeholder)");
            UnStage?.Invoke(this, EventArgs.Empty);
        }

        // 对照 WPF: DiscardSelection（按 Selection 计算 patch，git apply --reverse）
        public void DiscardSelection()
        {
            Console.WriteLine("[CommitFileDiffControl] DiscardSelection (spike placeholder)");
            Discard?.Invoke(this, EventArgs.Empty);
        }
    }
}
