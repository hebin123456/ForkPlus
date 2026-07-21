using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    // Phase 6.3：IDialogService 的 Avalonia 实现。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/OpenDialog.cs（用 Microsoft-WindowsAPICodePack-Shell，
    // Windows-only，仅 Windows 可用）：
    //   - SelectDirectory: CommonOpenFileDialog { IsFolderPicker = true }
    //   - SelectFile: CommonOpenFileDialog { IsFolderPicker = false, Filters.Add(CommonFileDialogFilter) }
    //   - SelectPatchSaveLocation / SelectFileSaveLocation: CommonSaveFileDialog
    //   - WPF MessageBox.Show（49 处调用）替代 ShowMessage
    //   - ForkPlus.UI.Dialogs.ErrorWindow 替代 ShowError
    //
    // 跨平台策略（Avalonia 11，三平台统一，不引入任何 Windows-only 包）：
    //   - ShowOpenFileDialog: TopLevel.StorageProvider.OpenFilePickerAsync(FilePickerOpenOptions)
    //     返回 IReadOnlyList<IStorageFile>，取 .Path.LocalPath 转字符串数组
    //   - ShowOpenFolderDialog: TopLevel.StorageProvider.OpenFolderPickerAsync(FolderPickerOpenOptions)
    //     返回 IReadOnlyList<IStorageFolder>，取第一个 .Path.LocalPath
    //   - ShowMessage / ShowError: 构造简单 Window（TextBlock + Button），用 ShowDialog(owner)
    //     模态显示（替代 WPF MessageBox.Show / ErrorWindow）
    //
    // 接口签名为同步，Avalonia StorageProvider / ShowDialog API 为异步：
    //   - 用 .GetAwaiter().GetResult() 同步阻塞（与 AvaloniaClipboardService 同样模式）
    //   - 调用方需在 UI 线程触发（与 WPF MessageBox.Show / CommonOpenFileDialog.ShowDialog 同样要求）
    //
    // filter 参数格式（兼容 WPF OpenFileDialog.Filter 与 CommonFileDialogFilter 调用风格）：
    //   - "Description|*.ext"           → 1 个 FileType("Description", ["ext"])
    //   - "Desc1|*.ext1|Desc2|*.ext2"   → 2 个 FileType
    //   - "*.exe"                        → 1 个 FileType("*.exe", ["exe"])
    //   - "*.exe;*.dll"                  → 1 个 FileType("*.exe;*.dll", ["exe", "dll"])
    //   - null/空字符串                  → 不设 FileTypeFilter（显示所有文件）
    public class AvaloniaDialogService : IDialogService
    {
        // ===== IDialogService 实现 =====

        public void ShowError(string title, string message, Exception exception = null)
        {
            // 对照 WPF ErrorWindow：显示 title + message + 异常类型/message/stack trace
            var fullMessage = exception == null
                ? (message ?? string.Empty)
                : $"{message}{Environment.NewLine}{Environment.NewLine}"
                  + $"{exception.GetType().Name}: {exception.Message}{Environment.NewLine}"
                  + $"{exception.StackTrace}";
            ShowMessageCore(
                string.IsNullOrEmpty(title) ? "ForkPlus" : title,
                fullMessage,
                DialogMessageBoxButton.OK,
                DialogMessageBoxImage.Error);
        }

        public DialogMessageBoxResult ShowMessage(
            string message,
            string title = "",
            DialogMessageBoxButton buttons = DialogMessageBoxButton.OK,
            DialogMessageBoxImage icon = DialogMessageBoxImage.Information)
        {
            return ShowMessageCore(
                string.IsNullOrEmpty(title) ? "ForkPlus" : title,
                message ?? string.Empty,
                buttons,
                icon);
        }

        public string[] ShowOpenFileDialog(
            string title,
            string filter = null,
            bool multiselect = false,
            string initialDirectory = null)
        {
            var topLevel = GetTopLevel();
            if (topLevel == null)
            {
                Console.Error.WriteLine("[AvaloniaDialogService] No TopLevel available for open file dialog.");
                return Array.Empty<string>();
            }

            var options = new FilePickerOpenOptions
            {
                Title = title ?? string.Empty,
                AllowMultiple = multiselect,
                FileTypeFilter = BuildFileTypeFilter(filter),
            };

            var startFolder = ResolveFolder(topLevel, initialDirectory);
            if (startFolder != null) options.SuggestedStartLocation = startFolder;

            IReadOnlyList<IStorageFile> result;
            try
            {
                result = topLevel.StorageProvider.OpenFilePickerAsync(options).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AvaloniaDialogService] OpenFilePicker failed: {ex.Message}");
                return Array.Empty<string>();
            }

            if (result == null || result.Count == 0) return Array.Empty<string>();
            return result.Select(f => f.Path.LocalPath).ToArray();
        }

        public string ShowOpenFolderDialog(string title, string initialDirectory = null)
        {
            var topLevel = GetTopLevel();
            if (topLevel == null)
            {
                Console.Error.WriteLine("[AvaloniaDialogService] No TopLevel available for open folder dialog.");
                return null;
            }

            var options = new FolderPickerOpenOptions
            {
                Title = title ?? string.Empty,
            };

            var startFolder = ResolveFolder(topLevel, initialDirectory);
            if (startFolder != null) options.SuggestedStartLocation = startFolder;

            IReadOnlyList<IStorageFolder> result;
            try
            {
                result = topLevel.StorageProvider.OpenFolderPickerAsync(options).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AvaloniaDialogService] OpenFolderPicker failed: {ex.Message}");
                return null;
            }

            if (result == null || result.Count == 0) return null;
            return result[0].Path.LocalPath;
        }

        // ===== 内部辅助 =====

        // 从 IClassicDesktopStyleApplicationLifetime 取主窗口的 TopLevel
        // （与 AvaloniaClipboardService.GetTopLevel 同样模式）
        private static TopLevel GetTopLevel()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return TopLevel.GetTopLevel(desktop.MainWindow);
            }
            return null;
        }

        // 解析 filter 字符串为 Avalonia FilePickerFileType 列表
        // 兼容两种格式：
        //   1. WPF OpenFileDialog.Filter 格式（成对 "Desc|*.ext"），parts.Length 为偶数
        //   2. 简单扩展名 pattern（"*.exe" 或 "*.exe;*.dll"），parts.Length 为奇数或 1
        // Avalonia 11 用 FilePickerFileType.Patterns（如 ["*.exe"]），不是扩展名数组。
        private static List<FilePickerFileType> BuildFileTypeFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return null;

            var fileTypes = new List<FilePickerFileType>();
            var parts = filter.Split('|');

            // 偶数个 part 且 >= 2：成对 (desc, pattern)
            if (parts.Length >= 2 && parts.Length % 2 == 0)
            {
                for (int i = 0; i < parts.Length; i += 2)
                {
                    var desc = parts[i]?.Trim();
                    var pattern = parts[i + 1]?.Trim();
                    if (string.IsNullOrEmpty(pattern)) continue;
                    var label = string.IsNullOrEmpty(desc) ? pattern : desc;
                    fileTypes.Add(new FilePickerFileType(label)
                    {
                        Patterns = ParsePatterns(pattern),
                    });
                }
            }
            else
            {
                // 每个 part 当作独立 pattern（"*.exe" / "*.exe;*.dll" / "*.patch"）
                foreach (var part in parts)
                {
                    var pattern = part?.Trim();
                    if (string.IsNullOrEmpty(pattern)) continue;
                    fileTypes.Add(new FilePickerFileType(pattern)
                    {
                        Patterns = ParsePatterns(pattern),
                    });
                }
            }

            return fileTypes.Count == 0 ? null : fileTypes;
        }

        // 把 "*.exe;*.dll" 拆分为 ["*.exe", "*.dll"]（Avalonia Patterns 期望带通配符的字符串）
        // 对照 CommonFileDialogFilter 构造：传入 "*.ext" 模式
        private static List<string> ParsePatterns(string pattern)
        {
            var patterns = new List<string>();
            foreach (var p in pattern.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = p.Trim();
                if (!string.IsNullOrEmpty(trimmed)) patterns.Add(trimmed);
            }
            // 兜底：返回 ["*"] 表示通配（Avalonia FilePickerFileType.Patterns 支持 "*"）
            return patterns.Count == 0 ? new List<string> { "*" } : patterns;
        }

        // 把 initialDirectory 字符串解析为 IStorageFolder（用于 SuggestedStartLocation）
        // 路径不存在或解析失败时返回 null（picker 回退到默认位置）
        private static IStorageFolder ResolveFolder(TopLevel topLevel, string initialDirectory)
        {
            if (string.IsNullOrWhiteSpace(initialDirectory)) return null;
            try
            {
                var fullPath = Path.GetFullPath(initialDirectory);
                if (!Directory.Exists(fullPath)) return null;
                // new Uri(string) 在 Windows/Linux/macOS 上对绝对路径都会构造 file:// URI
                var uri = new Uri(fullPath, UriKind.Absolute);
                return topLevel.StorageProvider.TryGetFolderFromPathAsync(uri).GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        // 构造简单模态消息窗口（替代 WPF MessageBox.Show / ErrorWindow）
        // 接口同步签名，用 ShowDialog(owner).GetAwaiter().GetResult() 阻塞
        // （ShowDialog 内部 pump 消息，不会死锁 UI 线程）
        private static DialogMessageBoxResult ShowMessageCore(
            string title,
            string message,
            DialogMessageBoxButton buttons,
            DialogMessageBoxImage icon)
        {
            var owner = GetOwnerWindow();
            if (owner == null)
            {
                Console.Error.WriteLine(
                    $"[AvaloniaDialogService] No main window, message dropped: {title} — {message}");
                return DialogMessageBoxResult.OK;
            }

            var dialog = new Window
            {
                Title = title ?? string.Empty,
                Width = 440,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
            };

            // 对照 WPF MessageBoxImage：用 unicode 字符替代 PNG 图标
            // （与 ForkPlusDialogWindow.SetStatus 用 emoji 同样策略，无额外资源依赖）
            var iconText = icon switch
            {
                DialogMessageBoxImage.Error => "✗",
                DialogMessageBoxImage.Warning => "⚠",
                DialogMessageBoxImage.Question => "?",
                DialogMessageBoxImage.Information => "ℹ",
                _ => "",
            };

            var iconTextBlock = new TextBlock
            {
                Text = iconText,
                FontSize = 32,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
            };

            var messageTextBlock = new TextBlock
            {
                Text = message ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var contentPanel = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Margin = new Thickness(18),
            };
            Grid.SetColumn(iconTextBlock, 0);
            Grid.SetColumn(messageTextBlock, 1);
            contentPanel.Children.Add(iconTextBlock);
            contentPanel.Children.Add(messageTextBlock);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 14),
                Spacing = 10,
            };

            // 默认返回值（用户直接关闭窗口时使用，对照 WPF MessageBox 默认行为）
            DialogMessageBoxResult defaultResult;
            var buttonList = BuildButtons(buttons, out defaultResult);
            foreach (var btn in buttonList)
            {
                buttonPanel.Children.Add(btn);
            }

            var root = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
            };
            Grid.SetRow(contentPanel, 0);
            Grid.SetRow(buttonPanel, 1);
            root.Children.Add(contentPanel);
            root.Children.Add(buttonPanel);

            dialog.Content = root;

            // 捕获结果变量（按钮 Click 设值后 Close，ShowDialog 返回后读取）
            DialogMessageBoxResult result = defaultResult;
            foreach (var btn in buttonPanel.Children.OfType<Button>())
            {
                btn.Click += (_, _) =>
                {
                    if (btn.Tag is DialogMessageBoxResult r) result = r;
                    dialog.Close();
                };
            }

            try
            {
                dialog.ShowDialog(owner).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AvaloniaDialogService] ShowDialog failed: {ex.Message}");
                return defaultResult;
            }

            return result;
        }

        // 根据 DialogMessageBoxButton 构造按钮列表，并通过 out 返回默认结果
        // （对照 WPF MessageBox 默认行为：OKCancel 默认 Cancel，YesNoCancel 默认 Cancel）
        private static List<Button> BuildButtons(
            DialogMessageBoxButton buttons,
            out DialogMessageBoxResult defaultResult)
        {
            var list = new List<Button>();
            switch (buttons)
            {
                case DialogMessageBoxButton.OK:
                    list.Add(MakeButton("OK", DialogMessageBoxResult.OK));
                    defaultResult = DialogMessageBoxResult.OK;
                    break;
                case DialogMessageBoxButton.OKCancel:
                    list.Add(MakeButton("OK", DialogMessageBoxResult.OK));
                    list.Add(MakeButton("Cancel", DialogMessageBoxResult.Cancel));
                    defaultResult = DialogMessageBoxResult.Cancel;
                    break;
                case DialogMessageBoxButton.YesNo:
                    list.Add(MakeButton("Yes", DialogMessageBoxResult.Yes));
                    list.Add(MakeButton("No", DialogMessageBoxResult.No));
                    defaultResult = DialogMessageBoxResult.No;
                    break;
                case DialogMessageBoxButton.YesNoCancel:
                    list.Add(MakeButton("Yes", DialogMessageBoxResult.Yes));
                    list.Add(MakeButton("No", DialogMessageBoxResult.No));
                    list.Add(MakeButton("Cancel", DialogMessageBoxResult.Cancel));
                    defaultResult = DialogMessageBoxResult.Cancel;
                    break;
                default:
                    list.Add(MakeButton("OK", DialogMessageBoxResult.OK));
                    defaultResult = DialogMessageBoxResult.OK;
                    break;
            }
            return list;
        }

        private static Button MakeButton(string text, DialogMessageBoxResult result)
        {
            return new Button
            {
                Content = text,
                Tag = result,
                MinWidth = 84,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
        }

        private static Window GetOwnerWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }
    }
}
