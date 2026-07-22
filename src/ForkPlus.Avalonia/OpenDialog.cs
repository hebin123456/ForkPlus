using System;
using System.IO;
using Avalonia.Controls;
// Avalonia spike 版 OpenDialog（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/OpenDialog.cs（140 行）：
//   - WPF: internal static class OpenDialog
//   - 依赖 Microsoft.WindowsAPICodePack.Dialogs（CommonOpenFileDialog / CommonSaveFileDialog）
//   - SelectDirectory(parent, title, initialDir, out path)：IsFolderPicker=true
//   - SelectExecutableFile(parent, title, initialDir, out path)：*.exe 过滤
//   - SelectFile(parent, title, initialDir, fileTypeName, extPattern, out path)
//   - SelectPatchSaveLocation(parent, title, initialDir, defaultFileName, out path)
//   - SelectFileSaveLocation(parent, title, initialDir, defaultFileName, out path)
//   - ShowDialog()：若 parent==MainWindow.Instance → PreventRefreshAfterChildDialogClose
//   - Translate()：PreferencesLocalization.Translate
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF CommonOpenFileDialog → Avalonia.OpenFileDialog（API 不同但功能类似）
//   2. WPF CommonSaveFileDialog → Avalonia.SaveFileDialog
//   3. WPF IsFolderPicker=true → Avalonia StorageProvider OpenFolderDialog
//   4. WPF CommonFileDialogFilter → Avalonia FileDialogFilter
//   5. WPF dialog.ShowDialog(parent) 同步 → Avalonia ShowDialogAsync（异步）
//      spike 版：用 .GetAwaiter().GetResult() 同步包装（spike 简化）
//   6. WPF PreferencesLocalization.Translate → spike 直接返回原字符串
//   7. WPF MainWindow.Instance.PreventRefreshAfterChildDialogClose → spike 跳过
//
// spike 简化（task spec 关键 API）：
//   - SelectDirectory / SelectFile / SelectExecutableFile / SelectPatchSaveLocation / SelectFileSaveLocation
namespace ForkPlus.Avalonia
{
    // spike 版：放在 ForkPlus.Avalonia 命名空间（task spec：Manager 类用此命名空间）。
    internal static class OpenDialog
    {
        // 对照 WPF: public static bool SelectDirectory(Window parent, string title, string initialDirectory, out string directoryPath)
        //   CommonOpenFileDialog { IsFolderPicker = true, ... }
        // Avalonia 版：用 OpenFolderDialog
        public static bool SelectDirectory(Window parent, string title, string initialDirectory, out string directoryPath)
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = Translate(title),
                    Directory = initialDirectory
                };
                var result = dialog.ShowAsync(parent).GetAwaiter().GetResult();
                if (result != null && result.Length > 0)
                {
                    directoryPath = result;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to show open directory dialog", ex);
            }
            directoryPath = null;
            return false;
        }

        // 对照 WPF: public static bool SelectExecutableFile(Window parent, string title, string initialDirectory, out string filePath)
        //   return SelectFile(parent, title, initialDirectory, "Applications", "*.exe", out filePath);
        public static bool SelectExecutableFile(Window parent, string title, string initialDirectory, out string filePath)
        {
            return SelectFile(parent, title, initialDirectory, "Applications", "*.exe", out filePath);
        }

        // 对照 WPF: public static bool SelectFile(Window parent, string title, string initialDirectory, string fileTypeName, string extensionPattern, out string filePath)
        //   CommonOpenFileDialog { IsFolderPicker = false, ... }
        // Avalonia 版：用 OpenFileDialog
        public static bool SelectFile(Window parent, string title, string initialDirectory, string fileTypeName, string extensionPattern, out string filePath)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = Translate(title),
                    Directory = initialDirectory,
                    AllowMultiple = false
                };
                var filter = ParseFilter(extensionPattern, fileTypeName);
                if (filter != null)
                {
                    dialog.Filters.Add(filter);
                }
                var result = dialog.ShowAsync(parent).GetAwaiter().GetResult();
                if (result != null && result.Length > 0)
                {
                    filePath = result[0];
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to show open file dialog", ex);
            }
            filePath = null;
            return false;
        }

        // 对照 WPF: public static bool SelectPatchSaveLocation(Window parent, string title, string initialDirectory, string defaultFileName, out string filePath)
        //   CommonSaveFileDialog { DefaultFileName = defaultFileName, ... }
        // Avalonia 版：用 SaveFileDialog
        public static bool SelectPatchSaveLocation(Window parent, string title, string initialDirectory, string defaultFileName, out string filePath)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = Translate(title),
                    Directory = initialDirectory,
                    DefaultExtension = Consts.Git.PatchFileExtension,
                    InitialFileName = defaultFileName
                };
                dialog.Filters.Add(new FileDialogFilter { Name = Translate("Patches"), Extensions = new System.Collections.Generic.List<string> { Consts.Git.PatchFileExtension.TrimStart('*') } });
                var result = dialog.ShowAsync(parent).GetAwaiter().GetResult();
                if (result != null)
                {
                    filePath = result;
                    if (!filePath.EndsWith(Consts.Git.PatchFileExtension, StringComparison.CurrentCultureIgnoreCase))
                    {
                        filePath += Consts.Git.PatchFileExtension;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to show save dialog", ex);
            }
            filePath = null;
            return false;
        }

        // 对照 WPF: public static bool SelectFileSaveLocation(Window parent, string title, string initialDirectory, string defaultFileName, out string resultFilePath)
        public static bool SelectFileSaveLocation(Window parent, string title, string initialDirectory, string defaultFileName, out string resultFilePath)
        {
            try
            {
                string extension = Path.GetExtension(defaultFileName);
                var dialog = new SaveFileDialog
                {
                    Title = Translate(title),
                    Directory = initialDirectory,
                    InitialFileName = defaultFileName,
                    DefaultExtension = extension
                };
                dialog.Filters.Add(new FileDialogFilter { Name = $"{extension} files", Extensions = new System.Collections.Generic.List<string> { extension.TrimStart('.') } });
                var result = dialog.ShowAsync(parent).GetAwaiter().GetResult();
                if (result != null)
                {
                    resultFilePath = result;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to show save dialog", ex);
            }
            resultFilePath = null;
            return false;
        }

        // spike 版：解析 *.exe 格式为 Avalonia FileDialogFilter
        private static FileDialogFilter ParseFilter(string extensionPattern, string fileTypeName)
        {
            // 对照 WPF: commonOpenFileDialog.Filters.Add(new CommonFileDialogFilter(Translate(fileTypeName), extensionPattern));
            // Avalonia 版：FileDialogFilter.Extensions 是不带 *. 的扩展名列表
            var filter = new FileDialogFilter { Name = Translate(fileTypeName) };
            string ext = extensionPattern.Replace("*", "").TrimStart('.');
            filter.Extensions.Add(ext);
            return filter;
        }

        // spike 版：本地翻译方法（替代 PreferencesLocalization.Translate）
        // 对照 WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        // spike 版：返回原字符串（不实际翻译）
        private static string Translate(string text)
        {
            return text;
        }
    }
}
