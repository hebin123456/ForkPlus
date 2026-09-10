// WPF → Avalonia 迁移兼容层：Microsoft.Win32.OpenFileDialog / SaveFileDialog shim。
// WPF ShowDialog() 同步返回 bool?；Avalonia StorageProvider 是异步 API。
// 这里用阻塞等待包装，保持迁移期调用形状不变。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage; // TryGetLocalPath 扩展方法所在命名空间

namespace Microsoft.Win32
{
    /// <summary>WPF Microsoft.Win32.FileDialog 基类形状。</summary>
    public abstract class FileDialog
    {
        public string Title { get; set; }
        public string Filter { get; set; } = "All files (*.*)|*.*";
        public bool CheckFileExists { get; set; }
        public bool Multiselect { get; set; }
        public string FileName { get; set; } = "";
        public string InitialDirectory { get; set; }

        /// <summary>解析 WPF "Name (*.ext)|*.ext" 过滤串为 Avalonia FileType 列表。</summary>
        internal IReadOnlyList<global::Avalonia.Platform.Storage.FilePickerFileType> ParseFilter()
        {
            var types = new List<global::Avalonia.Platform.Storage.FilePickerFileType>();
            if (string.IsNullOrEmpty(Filter)) return types;
            foreach (string part in Filter.Split('|'))
            {
                int idx = part.LastIndexOf('(');
                if (idx <= 0 || idx >= part.Length - 1) continue;
                string name = part.Substring(0, idx).Trim();
                string pats = part.Substring(idx + 1).TrimEnd(')').Trim();
                var patterns = pats.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0 && p != "*.*").ToArray();
                if (patterns.Length == 0) continue;
                types.Add(new global::Avalonia.Platform.Storage.FilePickerFileType(name) { Patterns = patterns });
            }
            return types;
        }

        internal static Window ActiveWindow
            => global::Avalonia.Application.Current?.ApplicationLifetime
                is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

        internal static T BlockingWait<T>(Task<T> task)
        {
            // v4.0.6：原实现 task.GetAwaiter().GetResult() 在 UI 线程上裸同步阻塞——
            // 任务完成依赖 UI 线程派发（StorageProvider 内部回调）时即死锁；即使不死锁，
            // 等待期间（慢目录枚举/网络盘）主窗口也全程无响应，是"选个文件卡死界面"的
            // 直接来源。改为 PushFrame 嵌套消息循环等待（与 ShowDialog/Clipboard 兼容层
            // 既有模式一致）：UI 线程持续泵消息，任务完成后由后台续体退出帧。
            // 非 UI 线程调用保持原语义（直接阻塞，本就安全）。
            if (task.IsCompleted) return task.GetAwaiter().GetResult();
            if (!global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                return task.GetAwaiter().GetResult();
            }
            var frame = new global::Avalonia.Threading.DispatcherFrame();
            task.ContinueWith(_ =>
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => frame.Continue = false);
                },
                TaskScheduler.Default);
            global::Avalonia.Threading.Dispatcher.UIThread.PushFrame(frame);
            // 帧退出即任务已完成；异常语义与原 GetResult() 一致（faulted 抛原异常）。
            return task.GetAwaiter().GetResult();
        }

        internal static global::Avalonia.Platform.Storage.IStorageFolder TryGetStartLocation(Window owner, string initialDirectory, string fileName)
        {
            string startPath = null;
            if (!string.IsNullOrWhiteSpace(initialDirectory) && System.IO.Directory.Exists(initialDirectory))
            {
                startPath = initialDirectory;
            }
            else if (!string.IsNullOrWhiteSpace(fileName))
            {
                string directory = System.IO.Path.GetDirectoryName(fileName);
                if (!string.IsNullOrWhiteSpace(directory) && System.IO.Directory.Exists(directory))
                {
                    startPath = directory;
                }
            }

            if (string.IsNullOrWhiteSpace(startPath))
            {
                return null;
            }

            try
            {
                return BlockingWait(owner.StorageProvider.TryGetFolderFromPathAsync(startPath));
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>WPF Microsoft.Win32.OpenFileDialog shim（Avalonia StorageProvider）。</summary>
    public sealed class OpenFileDialog : FileDialog
    {
        public string[] FileNames { get; private set; } = Array.Empty<string>();

        public bool? ShowDialog()
        {
            var owner = ActiveWindow;
            if (owner == null) return null;
            var pickerOptions = new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = Title,
                AllowMultiple = Multiselect,
                FileTypeFilter = ParseFilter(),
                SuggestedStartLocation = TryGetStartLocation(owner, InitialDirectory, FileName)
            };
            var files = BlockingWait(owner.StorageProvider.OpenFilePickerAsync(pickerOptions));
            var list = files?.ToList();
            if (list == null || list.Count == 0) return false;
            FileNames = list.Select(f => f.TryGetLocalPath() ?? f.Name).ToArray();
            FileName = FileNames[0];
            return true;
        }
    }

    /// <summary>WPF Microsoft.Win32.SaveFileDialog shim（Avalonia StorageProvider）。</summary>
    public sealed class SaveFileDialog : FileDialog
    {
        public bool OverwritePrompt { get; set; } = true;

        public bool? ShowDialog()
        {
            var owner = ActiveWindow;
            if (owner == null) return null;
            var suggestName = string.IsNullOrEmpty(FileName) ? null : System.IO.Path.GetFileName(FileName);
            var options = new global::Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = Title,
                SuggestedFileName = suggestName,
                FileTypeChoices = ParseFilter(),
                ShowOverwritePrompt = OverwritePrompt,
                SuggestedStartLocation = TryGetStartLocation(owner, InitialDirectory, FileName)
            };
            var file = BlockingWait(owner.StorageProvider.SaveFilePickerAsync(options));
            if (file == null) return false;
            FileName = file.TryGetLocalPath() ?? file.Name;
            return true;
        }
    }
}
