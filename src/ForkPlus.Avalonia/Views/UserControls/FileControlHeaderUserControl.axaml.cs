using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.7：Avalonia 版 FileControlHeaderUserControl 骨架（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/FileControlHeaderUserControl.xaml（304 行）：
    //   - 顶层 Grid 3 列：100 / * / 140
    //     Col 0: TextMode 导航按钮（Previous/Next，默认 Collapsed）
    //     Col 1: FilePath 居中（FileTypeImage + FilePathTextBlock 自定义控件）
    //     Col 2: TextMode 按钮（7 个 ToggleButton/Button，默认 Collapsed）+
    //            ImageMode 按钮（1 个 ToggleButton，默认 Collapsed）
    //   - 大量 DataTrigger 切换 ToggleButton Image（IsChecked=true/false 用不同图标）
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/FileControlHeaderUserControl.xaml.cs：
    //   - 2 个 DependencyProperty：FilePath / OldFilePath
    //   - 2 个公共属性：Target (FileDiffControlTarget) / HighlightPixelsToggleButtonEnabled
    //   - 2 个公共方法：ApplyLocalization / Show(filePath, oldFilePath, mode)
    //   - 私有 DiffLayoutMode 属性（按 Target 从 ForkPlusSettings 读写不同设置项）
    //
    // 装入路径（WPF）：作为 DiffUserControl / RevisionFileTreeUserControl 等的子控件
    //
    // 本 spike 版暂不迁移：
    //   - FilePathTextBlock 自定义控件（用 TextBlock 占位）
    //   - DataTrigger 切换 ToggleButton Image（Avalonia 用 Selector，spike 不迁移）
    //   - 6 个 ToggleButton + 4 个 Button 的真实图标绑定（spike 用纯文字标签）
    //   - TextMode/ImageMode 容器 Visibility 切换（spike 默认全部 Collapsed）
    //   - DiffLayoutMode 持久化到 ForkPlusSettings
    //   - ApplyLocalization
    //
    // 本 spike 版验证：
    //   - Grid 3 列布局正确显示
    //   - 中间 FilePath 显示文本占位
    public partial class FileControlHeaderUserControl : UserControl
    {
        // ===== 公共属性（对照 WPF）=====
        // spike 版用 string 占位，真实类型 FileDiffControlTarget 待 Phase 3.7 后续子阶段补
        public string Target { get; set; } = "None";
        public bool HighlightPixelsToggleButtonEnabled { get; set; }

        // 对照 WPF: DependencyProperty FilePath / OldFilePath
        public string FilePath { get; set; }
        public string OldFilePath { get; set; }

        public FileControlHeaderUserControl()
        {
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF 2 个公共方法签名，body stub）=====

        // 对照 WPF: public void Show(string filePath, string oldFilePath, FileControlHeaderMode mode = FileControlHeaderMode.None)
        //   显示文件路径 + 按 mode 切换 TextMode/ImageMode 容器可见性
        public void Show(string filePath, string oldFilePath, string mode = "None")
        {
            Console.WriteLine($"[FileControlHeader] Show (spike placeholder): filePath={filePath}, oldFilePath={oldFilePath}, mode={mode}");
            FilePath = filePath;
            OldFilePath = oldFilePath;
            if (FilePathTextBlock != null)
            {
                FilePathTextBlock.Text = filePath ?? "(no file)";
            }
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            Console.WriteLine("[FileControlHeader] ApplyLocalization (spike placeholder)");
        }

        // ===== ToggleButton/Button 事件占位（对照 WPF 9 个 click handler）=====
        // spike 版只打日志，真实逻辑（更新 ForkPlusSettings + 触发 diff 重渲染）留待 Phase 3.7 后续子阶段

        private void PreviousButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Previous change (spike placeholder)");
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Next change (spike placeholder)");
        }

        private void IgnoreWhitespacesToggleButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Ignore whitespaces toggle (spike placeholder)");
        }

        private void ShowHiddenSymbolsToggleButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Show hidden symbols toggle (spike placeholder)");
        }

        private void WordWrapToggleButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Word wrap toggle (spike placeholder)");
        }

        private void DecreaseVisibleLines_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Decrease visible lines (spike placeholder)");
        }

        private void IncreaseVisibleLines_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Increase visible lines (spike placeholder)");
        }

        private void ShowEntireFileToggleButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Show entire file toggle (spike placeholder)");
        }

        private void DiffLayoutModeToggleButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Diff layout mode toggle (spike placeholder)");
        }

        private void HighlightPixelsToggleButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[FileControlHeader] Highlight pixels toggle (spike placeholder)");
        }
    }
}
