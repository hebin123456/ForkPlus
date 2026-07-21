using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 DiffEntryRowUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/DiffEntryRowUserControl.xaml.cs（132 行）：
    //   - 构造函数 DiffEntryRowUserControl(DiffEntry entry)
    //   - Entry 属性 + IsExpanded 属性 + SelectionChanged 事件
    //   - SetDiffContent(FrameworkElement) / ClearDiffContent / BringDiffContentIntoView
    //   - HeaderToggleButton_CheckedChanged → Entry.IsExpanded = IsChecked
    //   - OnPreviewMouseLeftButtonUp → 点击行切换展开
    //   - Entry_PropertyChanged → UpdateExpansionVisualState（箭头方向 + SeparatorBorder 显隐）
    //   - CollapsedArrowGeometry / ExpandedArrowGeometry（Geometry.Parse）
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF ToggleButton Checked/Unchecked → IsCheckedChanged（Avalonia API）
    //   - WPF Geometry.Parse Path → emoji TextBlock（▶ 折叠 / ▼ 展开）
    //   - WPF Image ChangeTypeIcon/FileTypeIcon → emoji TextBlock
    //   - WPF OnPreviewMouseLeftButtonUp → PointerReleased（Avalonia API）
    //   - WPF FrameworkElement content → Control content（Avalonia API 规则）
    //   - WPF Visibility.Collapsed/Visible → IsVisible=false/true
    //   - WPF VisualTreeAttachmentHelper.TrySetChild → 直接 ContentControl.Content
    //   - WPF BringIntoView() → Avalonia VisualExtensions.BringIntoView()
    //   - task spec 关键 API：SetDiffEntry(DiffEntry entry)
    //
    // spike 简化：
    //   - SetDiffEntry(DiffEntry entry) 公共方法（task spec 关键 API）
    //   - 展开箭头用 emoji（▶/▼）替代 Path Geometry
    //   - 增删行数显示（+N 绿色 / -M 红色）
    //   - diff content host 用 ContentControl 占位
    public partial class DiffEntryRowUserControl : UserControl
    {
        // ===== 公共事件（对照 WPF）=====
        public event EventHandler SelectionChanged;

        // ===== 公共属性（对照 WPF）=====
        // 当前 DiffEntry（对照 WPF: Entry 属性）
        // spike 版用 Avalonia 工程内的 DiffEntry POCO（DiffEntrySpike.cs）
        public DiffEntry Entry { get; private set; }

        // 是否展开（对照 WPF: IsExpanded 属性，委托到 Entry.IsExpanded）
        public bool IsExpanded
        {
            get => Entry?.IsExpanded ?? false;
            set
            {
                if (Entry != null)
                {
                    if (Entry.IsExpanded != value)
                    {
                        Entry.IsExpanded = value;
                    }
                    else
                    {
                        UpdateExpansionVisualState(value);
                    }
                }
            }
        }

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        private bool _updatingToggleButton;

        // ===== 构造函数（对照 WPF: DiffEntryRowUserControl(DiffEntry entry)，spike 用 IServiceProvider）=====
        public DiffEntryRowUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
        }

        // ===== SetDiffEntry(DiffEntry)（task spec 关键 API）=====
        // 对照 WPF: 构造函数注入 entry + DataContext = entry + PropertyChanged 订阅
        // spike 版：SetDiffEntry 方法注入（配合 DI 无参构造）
        public void SetDiffEntry(DiffEntry entry)
        {
            // 解除旧 entry 的 PropertyChanged 订阅
            if (Entry != null)
            {
                Entry.PropertyChanged -= Entry_PropertyChanged;
            }

            Entry = entry;
            if (entry == null)
            {
                ClearDisplay();
                return;
            }

            // 订阅 PropertyChanged（对照 WPF: Entry.PropertyChanged += Entry_PropertyChanged）
            entry.PropertyChanged += Entry_PropertyChanged;

            // 更新显示
            if (FilePathTextBlock != null)
            {
                FilePathTextBlock.Text = entry.FilePath ?? string.Empty;
            }
            if (ChangeTypeIconTextBlock != null)
            {
                ChangeTypeIconTextBlock.Text = string.IsNullOrEmpty(entry.ChangeTypeEmoji) ? "📝" : entry.ChangeTypeEmoji;
            }
            if (FileTypeIconTextBlock != null)
            {
                FileTypeIconTextBlock.Text = string.IsNullOrEmpty(entry.FileTypeEmoji) ? "📄" : entry.FileTypeEmoji;
            }
            if (AddedLinesTextBlock != null)
            {
                AddedLinesTextBlock.Text = entry.AddedLines > 0 ? $"+{entry.AddedLines}" : "";
            }
            if (DeletedLinesTextBlock != null)
            {
                DeletedLinesTextBlock.Text = entry.DeletedLines > 0 ? $"-{entry.DeletedLines}" : "";
            }

            UpdateExpansionVisualState(entry.IsExpanded);
        }

        // ===== ClearDiffContent()（对照 WPF）=====
        public void ClearDiffContent()
        {
            if (Entry != null)
            {
                Entry.PropertyChanged -= Entry_PropertyChanged;
            }
            SetDiffContent(null);
        }

        // ===== SetDiffContent(Control)（对照 WPF: SetDiffContent(FrameworkElement)）=====
        // WPF 用 VisualTreeAttachmentHelper.TrySetChild，spike 直接设置 ContentControl.Content
        public void SetDiffContent(Control content)
        {
            if (DiffContentContainer == null) return;

            if (content == null)
            {
                DiffContentContainer.Content = null;
                if (DiffContentHost != null) DiffContentHost.IsVisible = false;
                return;
            }
            DiffContentContainer.Content = content;
            DiffContentContainer.IsVisible = true;
            if (DiffContentHost != null) DiffContentHost.IsVisible = true;
        }

        // ===== BringDiffContentIntoView()（对照 WPF）=====
        // WPF: DiffContentHost.BringIntoView() / BringIntoView()
        // Avalonia: VisualExtensions.BringIntoView()（需 using Avalonia.VisualTree）
        public void BringDiffContentIntoView()
        {
            if (DiffContentHost != null && DiffContentHost.IsVisible)
            {
                DiffContentHost.BringIntoView();
            }
            else
            {
                this.BringIntoView();
            }
        }

        // ===== HeaderToggleButton 事件（对照 WPF: HeaderToggleButton_CheckedChanged）=====
        // WPF: Checked/Unchecked → IsCheckedChanged（Avalonia API）
        private void HeaderToggleButton_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_updatingToggleButton || Entry == null) return;
            if (HeaderToggleButton != null)
            {
                Entry.IsExpanded = HeaderToggleButton.IsChecked ?? false;
            }
            e.Handled = true;
        }

        // ===== Entry_PropertyChanged（对照 WPF，完整迁移）=====
        private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DiffEntry.IsExpanded))
            {
                UpdateExpansionVisualState(Entry.IsExpanded);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // ===== UpdateExpansionVisualState（对照 WPF，spike 用 emoji 替代 Geometry）=====
        // WPF: HeaderToggleButton.IsChecked + ArrowPath.Data + SeparatorBorder.Visibility
        // spike: HeaderToggleButton.IsChecked + ArrowTextBlock.Text + SeparatorBorder.IsVisible
        private void UpdateExpansionVisualState(bool isExpanded)
        {
            _updatingToggleButton = true;
            if (HeaderToggleButton != null)
            {
                HeaderToggleButton.IsChecked = isExpanded;
            }
            _updatingToggleButton = false;

            if (ArrowTextBlock != null)
            {
                // 对照 WPF: ExpandedArrowGeometry "M0,0L3.5,3.5 7,0" → ▼
                // 对照 WPF: CollapsedArrowGeometry "M0,0L3.5,3.5 0,7" → ▶
                ArrowTextBlock.Text = isExpanded ? "▼" : "▶";
            }
            if (SeparatorBorder != null)
            {
                SeparatorBorder.IsVisible = isExpanded;
            }
            if (!isExpanded)
            {
                SetDiffContent(null);
            }
        }

        // ===== ClearDisplay（spike 新增，清空显示）=====
        private void ClearDisplay()
        {
            if (FilePathTextBlock != null) FilePathTextBlock.Text = "(no file)";
            if (ChangeTypeIconTextBlock != null) ChangeTypeIconTextBlock.Text = "📝";
            if (FileTypeIconTextBlock != null) FileTypeIconTextBlock.Text = "📄";
            if (AddedLinesTextBlock != null) AddedLinesTextBlock.Text = "";
            if (DeletedLinesTextBlock != null) DeletedLinesTextBlock.Text = "";
            if (ArrowTextBlock != null) ArrowTextBlock.Text = "▶";
            if (SeparatorBorder != null) SeparatorBorder.IsVisible = false;
            if (DiffContentHost != null) DiffContentHost.IsVisible = false;
        }
    }
}
