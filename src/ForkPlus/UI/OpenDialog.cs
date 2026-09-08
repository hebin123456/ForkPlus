using System;
using System.IO;
using Avalonia;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Win32;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
        internal static class OpenDialog
        {
                public static bool SelectDirectory([Null] Window parent, string title, string initialDirectory, out string directoryPath)
                {
                        try
                        {
                                if (ShowOpen(parent, Translate(title), initialDirectory, folderPicker: true, filters: null, out directoryPath))
                                {
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

                public static bool SelectExecutableFile([Null] Window parent, string title, string initialDirectory, out string filePath)
                {
                        // Migration note（2026-09-07 用户反馈）：Linux/macOS 可执行文件没有 .exe 扩展名
                        //（git / git-ai / git-mm / 终端模拟器...），*.exe 过滤会把所有可选目标挡在列表
                        // 外——"添加自定义实例"永远选不到文件。非 Windows 改为不过滤（任意文件可选），
                        // Windows 保持原版 *.exe 语义。
                        if (OperatingSystem.IsWindows())
                        {
                                return SelectFile(parent, title, initialDirectory, "Applications", "*.exe", out filePath);
                        }
                        return SelectAnyFile(parent, title, initialDirectory, out filePath);
                }

                /// <summary>无过滤选择任意文件：Linux/macOS 可执行文件无扩展名，按扩展名过滤会挡住目标。</summary>
                public static bool SelectAnyFile([Null] Window parent, string title, string initialDirectory, out string filePath)
                {
                        try
                        {
                                if (ShowOpen(parent, Translate(title), initialDirectory, folderPicker: false, filters: null, out filePath))
                                {
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

                /// <summary>
                /// Windows 专用的可执行文件过滤器（*.exe / *.cmd / *.bat）在非 Windows 平台退化为 null：
                /// Unix 可执行文件无扩展名，任何 exe 类 glob 都会把它们滤掉（选择器里看不见目标文件）。
                /// 调用方把 Windows 过滤串传入 SelectFile 即可，Unix 端经空模式（SelectFile 的
                /// extensionPattern 空值分支）变成"无过滤器"；SelectExecutableFile 走上面的
                /// SelectAnyFile 显式分支，语义相同。
                /// </summary>
                public static string ExecutableFilterOrNullOnUnix(string windowsPattern)
                {
                        return OperatingSystem.IsWindows() ? windowsPattern : null;
                }

                public static bool SelectFile([Null] Window parent, string title, string initialDirectory, string fileTypeName, string extensionPattern, out string filePath)
                {
                        try
                        {
                                // extensionPattern 为 null/空 → 不带过滤器（选择器显示全部文件）；
                                // 用于非 Windows 平台选择无扩展名的可执行文件（git-mm 等）。
                                var filters = string.IsNullOrEmpty(extensionPattern)
                                        ? null
                                        : new[] { (Translate(fileTypeName), extensionPattern) };
                                if (ShowOpen(parent, Translate(title), initialDirectory, folderPicker: false, filters, out filePath))
                                {
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

                public static bool SelectPatchSaveLocation([Null] Window parent, string title, string initialDirectory, string defaultFileName, out string filePath)
                {
                        try
                        {
                                var filters = new[] { (Translate("Patches"), "*" + Consts.Git.PatchFileExtension) };
                                if (ShowSave(parent, Translate(title), initialDirectory, defaultFileName, filters, out filePath))
                                {
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

                public static bool SelectFileSaveLocation([Null] Window parent, string title, string initialDirectory, string defaultFileName, out string resultFilePath)
                {
                        try
                        {
                                string extension = Path.GetExtension(defaultFileName);
                                var filters = new[] { (string.Format(Translate("*{0} files"), extension), extension) };
                                if (ShowSave(parent, Translate(title), initialDirectory, defaultFileName, filters, out resultFilePath))
                                {
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

                private static bool ShowOpen(Window parent, string title, string initialDirectory, bool folderPicker,
                        (string name, string spec)[] filters, out string path)
                {
                        // Migration note：跨平台化——Windows 保留 Win32 COM IFileDialog（与 WPF 原版行为一致），
                        // Linux/macOS 走 Avalonia StorageProvider（此前非 Windows 静默返回 false，
                        // 导致 Kali/macOS 上"初始化新仓库/克隆/打开仓库"等所有文件选择功能无反应）。
                        bool result;
                        if (OperatingSystem.IsWindows())
                        {
                                IntPtr owner = GetOwnerHandle(parent);
                                result = FileDialogInterop.ShowOpenDialog(owner, title, initialDirectory, folderPicker, filters, out path);
                        }
                        else
                        {
                                result = StorageProviderDialogs.ShowOpenDialog(parent, title, initialDirectory, folderPicker, filters, out path);
                        }
                        NotifyDialogClosed(parent);
                        return result;
                }

                private static bool ShowSave(Window parent, string title, string initialDirectory, string defaultFileName,
                        (string name, string spec)[] filters, out string path)
                {
                        bool result;
                        if (OperatingSystem.IsWindows())
                        {
                                IntPtr owner = GetOwnerHandle(parent);
                                result = FileDialogInterop.ShowSaveDialog(owner, title, initialDirectory, defaultFileName, filters, out path);
                        }
                        else
                        {
                                result = StorageProviderDialogs.ShowSaveDialog(parent, title, initialDirectory, defaultFileName, filters, out path);
                        }
                        NotifyDialogClosed(parent);
                        return result;
                }

                private static void NotifyDialogClosed([Null] Window parent)
                {
                        if (parent == MainWindow.Instance)
                        {
                                MainWindow.Instance.PreventRefreshAfterChildDialogClose("Open File Dialog");
                        }
                }

                private static IntPtr GetOwnerHandle([Null] Window parent)
                {
                        try
                        {
                                if (parent?.TryGetPlatformHandle() is { } handle)
                                {
                                        return handle.Handle;
                                }
                        }
                        catch
                        {
                        }
                        return IntPtr.Zero;
                }

                private static string Translate(string text)
                {
                        return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
                }
        }
}
