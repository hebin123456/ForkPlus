using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.19b：Avalonia 版 KeyboardShortcutsWindow（真实迁移版，对照 WPF KeyboardShortcutsWindow.cs 270 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/KeyboardShortcutsWindow.cs（纯代码构造 UI，无 .xaml）：
    //   - public class KeyboardShortcutsWindow : ForkPlusDialogWindow
    //   - 静态数据：5 个 ShortcutSection，约 50 个 ShortcutRow（keys + description）
    //   - 构造函数:
    //     * Title = "Keyboard Shortcuts"
    //     * Width=720, Height=620, ShowLogo=false
    //     * DialogTitle = PreferencesLocalization.Current("Keyboard Shortcuts")
    //     * DialogDescription = PreferencesLocalization.Current("Available keyboard shortcuts")
    //     * SubmitButtonTitle = PreferencesLocalization.Current("Close")
    //     * ShowCancelButton = false
    //   - CreateContent: ScrollViewer + StackPanel
    //   - CreateShortcutRow: Grid 2 列 (230, *)，左 WrapPanel 按键 badge，右 TextBlock 描述
    //   - CreateKeyBadge: Border + TextBlock(Monospace)
    //   - keys 字符串解析: ',' 拆替代 → " / " 拆 chord → '+' 拆按键
    //
    // Avalonia 版差异：
    //   1. spike 模式：axaml 提供 Header/ScrollViewer/Footer，cs 动态填充 ContentStackPanel
    //   2. PreferencesLocalization.Current/Translate → ServiceLocator.Localization.Translate
    //   3. FontConstants.MonospaceFontFamily → FontFamily="Consolas"
    //   4. SetResourceReference → 直接设置 Foreground/BorderBrush/Background（用 DynamicResource 在 axaml 中
    //      无法对动态生成的控件应用，所以 cs 端用 Brushes.Gray 等 fallback 或绑定 DynamicResource）
    //   5. 保留 WPF 原版的代码动态构造 UI 风格
    public partial class KeyboardShortcutsWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private sealed class ShortcutSection
        {
            public string Title { get; }
            public ShortcutRow[] Rows { get; }

            public ShortcutSection(string title, params ShortcutRow[] rows)
            {
                Title = title;
                Rows = rows;
            }
        }

        private sealed class ShortcutRow
        {
            public string Keys { get; }
            public string Description { get; }

            public ShortcutRow(string keys, string description)
            {
                Keys = keys;
                Description = description;
            }
        }

        // 对照 WPF: private static readonly ShortcutSection[] Sections
        // 5 个分组，约 50 个快捷键。文案与 WPF 版完全一致。
        private static readonly ShortcutSection[] Sections = new ShortcutSection[]
        {
            new ShortcutSection("General Navigation",
                new ShortcutRow("Ctrl+1", "Show Changes view (second press will focus commit field)"),
                new ShortcutRow("Ctrl+2", "Show All Commits view (second press will jump to HEAD)"),
                new ShortcutRow("Ctrl+0", "Reveal HEAD"),
                new ShortcutRow("Ctrl+P", "Show Quick Launch window"),
                new ShortcutRow("Ctrl+Tab", "Select next tab"),
                new ShortcutRow("Ctrl+Shift+Tab", "Select previous tab"),
                new ShortcutRow("Ctrl+T", "Open new tab"),
                new ShortcutRow("Ctrl+W", "Close current tab"),
                new ShortcutRow("Ctrl+= / Ctrl+-", "Zoom in / Zoom out"),
                new ShortcutRow("Ctrl+,", "Open ForkPlus preferences")),
            new ShortcutSection("All Commits View",
                new ShortcutRow("Ctrl+0", "Jump to HEAD"),
                new ShortcutRow("Ctrl+F", "Commit search"),
                new ShortcutRow("Enter, F3", "Jump to next search result"),
                new ShortcutRow("Shift+Enter, Shift+F3", "Jump to previous search result"),
                new ShortcutRow("Ctrl+C", "Copy commit info"),
                new ShortcutRow("Delete", "Remove branch/stash"),
                new ShortcutRow("Ctrl+Shift+A", "Filter by active branch")),
            new ShortcutSection("Changes View",
                new ShortcutRow("Ctrl+Enter", "Commit"),
                new ShortcutRow("Ctrl+Shift+Enter", "Commit and push"),
                new ShortcutRow("Ctrl+1", "Focus commit message field"),
                new ShortcutRow("Ctrl+F", "Filter"),
                new ShortcutRow("Enter, Ctrl+Shift+S", "Stage/unstage selected file (or lines)"),
                new ShortcutRow("Ctrl+Alt+Shift+S", "Stage/unstage all files"),
                new ShortcutRow("Backspace, Ctrl+Shift+D", "Discard selected file (or lines)"),
                new ShortcutRow("Ctrl+O", "Open selected file"),
                new ShortcutRow("Ctrl+D", "Open selected file in external diff tool"),
                new ShortcutRow("Ctrl+C", "Copy selected file full path")),
            new ShortcutSection("Repository",
                new ShortcutRow("F5", "Refresh"),
                new ShortcutRow("Ctrl+Shift+N", "Init new repository"),
                new ShortcutRow("Ctrl+N", "Clone new repository"),
                new ShortcutRow("Ctrl+G", "Initialize git mm Repository"),
                new ShortcutRow("Ctrl+O", "Open repository"),
                new ShortcutRow("Ctrl+Shift+F", "Fetch"),
                new ShortcutRow("Ctrl+Alt+Shift+F, Ctrl+Click", "Quick Fetch"),
                new ShortcutRow("Ctrl+Shift+L", "Pull"),
                new ShortcutRow("Ctrl+Alt+Shift+L, Ctrl+Click", "Quick Pull"),
                new ShortcutRow("Ctrl+Shift+P", "Push"),
                new ShortcutRow("Ctrl+Alt+Shift+P, Ctrl+Click", "Quick Push"),
                new ShortcutRow("Ctrl+Shift+B", "New branch"),
                new ShortcutRow("Ctrl+Shift+T", "New tag"),
                new ShortcutRow("Ctrl+Shift+H", "Create stash"),
                new ShortcutRow("Ctrl+Alt+O", "Open in File Explorer"),
                new ShortcutRow("Ctrl+Alt+T", "Open in Terminal")),
            new ShortcutSection("Repository Manager",
                new ShortcutRow("F2", "Rename Repository"),
                new ShortcutRow("Delete", "Remove Repository"),
                new ShortcutRow("Enter", "Open Repository"))
        };

        public KeyboardShortcutsWindow()
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.Title / Width / Height / ShowLogo
            string title = Translate("Keyboard Shortcuts");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Available keyboard shortcuts");
            SubmitButtonTitle = Translate("Close");
            ShowCancelButton = false;

            // 对照 WPF: CreateContent() — 动态填充 ContentStackPanel
            PopulateContent();
        }

        // 对照 WPF: CreateContent / CreateSectionHeader / CreateShortcutRow / CreateKeysPanel / AddChord
        private void PopulateContent()
        {
            foreach (ShortcutSection section in Sections)
            {
                ContentStackPanel.Children.Add(CreateSectionHeader(section.Title));
                foreach (ShortcutRow row in section.Rows)
                {
                    ContentStackPanel.Children.Add(CreateShortcutRow(row));
                }
            }
        }

        private static TextBlock CreateSectionHeader(string title)
        {
            return new TextBlock
            {
                Text = Translate(title),
                FontSize = 14,
                FontWeight = FontWeight.Medium,
                Margin = new Thickness(0, 12, 0, 5)
            };
        }

        private static Grid CreateShortcutRow(ShortcutRow row)
        {
            Grid grid = new Grid
            {
                Margin = new Thickness(0, 2, 0, 2),
                ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(230, GridUnitType.Pixel),
                    new ColumnDefinition(1, GridUnitType.Star)
                }
            };
            WrapPanel keysPanel = CreateKeysPanel(row.Keys);
            Grid.SetColumn(keysPanel, 0);
            grid.Children.Add(keysPanel);

            TextBlock descriptionTextBlock = new TextBlock
            {
                Text = Translate(row.Description),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 0, 0, 0)
            };
            // 对照 WPF: descriptionTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
            // Avalonia spike 版：spike 版不接入主题 brush 跟随，使用默认前景色（继承窗口）
            // Phase 4.0c 升级到 ControlTemplate 后改回 DynamicResource
            Grid.SetColumn(descriptionTextBlock, 1);
            grid.Children.Add(descriptionTextBlock);
            return grid;
        }

        private static WrapPanel CreateKeysPanel(string keys)
        {
            WrapPanel panel = new WrapPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            string[] alternatives = keys.Split(',');
            for (int i = 0; i < alternatives.Length; i++)
            {
                if (i > 0)
                {
                    panel.Children.Add(CreateSeparatorText(","));
                }
                string[] chords = alternatives[i].Trim().Split(new string[] { " / " }, StringSplitOptions.None);
                for (int j = 0; j < chords.Length; j++)
                {
                    if (j > 0)
                    {
                        panel.Children.Add(CreateSeparatorText("/"));
                    }
                    AddChord(panel, chords[j].Trim());
                }
            }
            return panel;
        }

        private static void AddChord(WrapPanel panel, string chord)
        {
            string[] keys = chord.Split('+');
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0)
                {
                    panel.Children.Add(CreateSeparatorText("+"));
                }
                panel.Children.Add(CreateKeyBadge(keys[i].Trim()));
            }
        }

        private static Border CreateKeyBadge(string key)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = key,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border border = new Border
            {
                Child = textBlock,
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 1, 5, 2),
                Margin = new Thickness(1),
                // 对照 WPF: border.SetResourceResource(Border.BorderBrushProperty, "BorderBrush");
                //          border.SetResourceResource(Border.BackgroundProperty, "TextBox.Static.Background");
                // Avalonia spike 版：用固定灰色 fallback，spike 不接入主题 brush 跟随
                // Phase 4.0c 升级到 ControlTemplate 后改回 DynamicResource
                BorderBrush = Brushes.Gray,
                Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0))
            };
            return border;
        }

        private static TextBlock CreateSeparatorText(string text)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Margin = new Thickness(3, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                // 对照 WPF: textBlock.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryLabelBrush");
                // Avalonia spike 版：用固定灰色 fallback
                Foreground = Brushes.Gray
            };
            return textBlock;
        }

        // 对照 WPF: PreferencesLocalization.Current(text) / PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        // Avalonia 版统一用 ServiceLocator.Localization.Translate(text, userSettings.UiLanguage)
        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }
    }
}
