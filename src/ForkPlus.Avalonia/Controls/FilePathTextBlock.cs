using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 FilePathTextBlock（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/FilePathTextBlock.cs（146 行）：
    //   - WPF FilePathTextBlock : SelectableTextBlock（自定义 WPF 基类，可选中文本）
    //   - FilePathProperty / OldFilePathProperty（RegisterAttached）
    //   - Refresh() 拆分路径：folder part（SecondaryLabelBrush）+ filename part（LabelBrush）
    //     重命名文件显示 "OldPath → NewPath"（带箭头）
    //   - RefreshBrushes() 从 Theme.LabelBrush / Theme.SecondaryLabelBrush 取画刷
    //   - ApplicationThemeChanged 事件订阅 → 刷新画刷 + Refresh
    //   - TextIsTrimmed() 判断文本是否被截断（Measure + DesiredSize 对比 ActualWidth）
    //   - GetToolTipText() 返回 Old/New 路径多行 tooltip
    //   - MouseEnter 事件：文本截断时显示 ToolTip
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 SelectableTextBlock → Avalonia.Controls.TextBlock
    //      （Avalonia 11 TextBlock 已支持 SelectingText 属性，spike 不继承自定义可选基类）
    //   2. DependencyProperty.RegisterAttached → StyledProperty<T>.Register（含 coercion 回调）
    //   3. WPF Inlines.Add(new Run { Foreground = ... }) →
    //      Avalonia Inlines.Add(new Run { Foreground = ... })（API 一致，Run 在 Avalonia.Controls.Documents）
    //   4. spike 跳过 Theme.LabelBrush / Theme.SecondaryLabelBrush 引用（Theme 类未迁移）
    //      用硬编码颜色 spike 兜底（浅色主题：LabelBrush=#333333, SecondaryLabelBrush=#888888）
    //   5. spike 跳过 NotificationCenter.ApplicationThemeChanged 订阅（NotificationCenter 在 WPF 工程）
    //   6. spike 跳过 TextIsTrimmed() Measure 检测（Avalonia TextBlock 自动处理 TextTrimming）
    //      用 ToolTip.Tip 始终显示完整路径（spike 简化）
    //   7. spike 跳过 MouseEnter + ToolTip 动态显示逻辑（Avalonia ToolTip 自动管理）
    //
    // spike 简化：
    //   - 继承 TextBlock + FilePath / OldFilePath StyledProperty
    //   - SetText(path) 拆分路径分段着色（folder=灰，filename=黑）
    //   - 重命名时显示 "oldPath → newPath" 带箭头
    public class FilePathTextBlock : TextBlock
    {
        // 对照 WPF: FilePathProperty（RegisterAttached，变化时 Refresh）
        // Avalonia 11：AvaloniaProperty.Register 无 notifying 参数，改用 OnPropertyChanged 触发 Refresh。
        public static readonly StyledProperty<string> FilePathProperty =
            AvaloniaProperty.Register<FilePathTextBlock, string>(nameof(FilePath));

        // 对照 WPF: OldFilePathProperty（RegisterAttached）
        public static readonly StyledProperty<string> OldFilePathProperty =
            AvaloniaProperty.Register<FilePathTextBlock, string>(nameof(OldFilePath));

        // spike 版硬编码画刷（替代 WPF Theme.LabelBrush / Theme.SecondaryLabelBrush）
        // 浅色主题兜底色：LabelBrush=#333333（深灰，主文本色）, SecondaryLabelBrush=#888888（中灰，次要色）
        private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        private static readonly IBrush SecondaryLabelBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        public string FilePath
        {
            get => GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        public string OldFilePath
        {
            get => GetValue(OldFilePathProperty);
            set => SetValue(OldFilePathProperty, value);
        }

        public FilePathTextBlock()
        {
            // spike 版：始终显示 ToolTip（Avalonia 自动管理 ToolTip 显示）
            // 对照 WPF: MouseEnter 时检查 TextIsTrimmed() 决定是否显示 ToolTip
        }

        // Avalonia 11：替代 WPF notifying 回调，FilePath/OldFilePath 变化时触发 Refresh。
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == FilePathProperty || change.Property == OldFilePathProperty)
            {
                Refresh();
            }
        }

        // 对照 WPF: private void Refresh()
        //   清空 Inlines + 按 OldFilePath + FilePath 拆分路径分段着色
        public void Refresh()
        {
            Inlines.Clear();
            string oldFilePath = OldFilePath;
            if (oldFilePath != null)
            {
                string readableFileName = PathHelper.GetReadableFileName(oldFilePath);
                int folderLen = oldFilePath.Length - readableFileName.Length;
                if (folderLen != 0)
                {
                    Inlines.Add(new Run(oldFilePath.Substring(0, folderLen))
                    {
                        Foreground = SecondaryLabelBrush
                    });
                }
                Inlines.Add(new Run(readableFileName)
                {
                    Foreground = LabelBrush
                });
                Inlines.Add(new Run(" → ")
                {
                    Foreground = LabelBrush
                });
            }
            string filePath = FilePath;
            if (filePath != null)
            {
                string readableFileName = PathHelper.GetReadableFileName(filePath);
                int folderLen = filePath.Length - readableFileName.Length;
                if (folderLen != 0)
                {
                    Inlines.Add(new Run(filePath.Substring(0, folderLen))
                    {
                        Foreground = SecondaryLabelBrush
                    });
                }
                Inlines.Add(new Run(readableFileName)
                {
                    Foreground = LabelBrush
                });
            }
            // spike 版：始终设置 ToolTip 显示完整路径（简化 WPF TextIsTrimmed 检测）
            ToolTip.SetTip(this, GetToolTipText());
        }

        // 对照 WPF: public void SetText(string path)（task spec 关键 API）
        // spike 版：设置 FilePath 属性并触发 Refresh
        public void SetText(string path)
        {
            FilePath = path;
            // Refresh 由 OnPropertyChanged(FilePathProperty) 自动触发
        }

        // 对照 WPF: private string GetToolTipText()
        //   Old/New 路径多行 tooltip，单路径时仅显示 FilePath
        private string GetToolTipText()
        {
            string filePath = FilePath;
            if (filePath != null)
            {
                string oldFilePath = OldFilePath;
                if (oldFilePath != null)
                {
                    return "Old:\t" + oldFilePath + Environment.NewLine + "New:\t" + filePath;
                }
                return filePath;
            }
            return null;
        }
    }
}
