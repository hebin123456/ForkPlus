// 跨平台文件/目录选择对话框：Avalonia StorageProvider 实现。
// Windows 由 FileDialogInterop（Win32 COM IFileDialog，与 WPF 原版行为一致）处理；
// Linux/macOS 走这里（XDG portal / NSOpenPanel 由 Avalonia 自动对接）。
// ForkPlus 的调用链全部是同步 API，这里用 DispatcherFrame PushFrame 同步桥接
// （同 WpfCompat Clipboard / WindowDialogCompat 的既有模式）。

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace ForkPlus.UI
{
	internal static class StorageProviderDialogs
	{
		public static bool ShowOpenDialog([Null] Window parent, string title, string initialDirectory, bool folderPicker,
			(string name, string spec)[] filters, out string path)
		{
			path = null;
			Window window = parent ?? GetActiveWindow();
			if (window == null)
			{
				Log.Error("Failed to show open dialog: no visible window for StorageProvider", null);
				return false;
			}
			try
			{
				Task<string> task = ShowOpenAsync(window, title, initialDirectory, folderPicker, filters);
				path = Wait(task);
				return !string.IsNullOrEmpty(path);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show open dialog (StorageProvider)", ex);
				return false;
			}
		}

		public static bool ShowSaveDialog([Null] Window parent, string title, string initialDirectory, string defaultFileName,
			(string name, string spec)[] filters, out string path)
		{
			path = null;
			Window window = parent ?? GetActiveWindow();
			if (window == null)
			{
				Log.Error("Failed to show save dialog: no visible window for StorageProvider", null);
				return false;
			}
			try
			{
				Task<string> task = ShowSaveAsync(window, title, initialDirectory, defaultFileName, filters);
				path = Wait(task);
				return !string.IsNullOrEmpty(path);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show save dialog (StorageProvider)", ex);
				return false;
			}
		}

		private static async Task<string> ShowOpenAsync(Window window, string title, string initialDirectory, bool folderPicker,
			(string name, string spec)[] filters)
		{
			IStorageProvider provider = window.StorageProvider;
			IStorageFolder startLocation = await TryGetStartLocation(provider, initialDirectory);
			if (folderPicker)
			{
				var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
				{
					Title = title,
					AllowMultiple = false,
					SuggestedStartLocation = startLocation
				});
				return (folders.Count > 0) ? folders[0].TryGetLocalPath() : null;
			}
			var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = title,
				AllowMultiple = false,
				SuggestedStartLocation = startLocation,
				FileTypeFilter = BuildFileTypes(filters)
			});
			return (files.Count > 0) ? files[0].TryGetLocalPath() : null;
		}

		private static async Task<string> ShowSaveAsync(Window window, string title, string initialDirectory, string defaultFileName,
			(string name, string spec)[] filters)
		{
			IStorageProvider provider = window.StorageProvider;
			IStorageFolder startLocation = await TryGetStartLocation(provider, initialDirectory);
			IStorageFile file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = title,
				SuggestedFileName = defaultFileName,
				SuggestedStartLocation = startLocation,
				ShowOverwritePrompt = true,
				FileTypeChoices = BuildFileTypes(filters)
			});
			return file?.TryGetLocalPath();
		}

		// internal（InternalsVisibleTo）：回归测试直接断言过滤串解析（多模式拆分 / 空模式无过滤器）。
		internal static System.Collections.Generic.List<FilePickerFileType> BuildFileTypes((string name, string spec)[] filters)
		{
			// Migration note：ForkPlus 的过滤串全部是单模式（如 *.exe / *.patch），逐一映射成 Patterns。
			// Bug 修复（2026-09-07）：WPF 语法允许 "*.a; *.b" 多模式串——整串塞进单个 Pattern
			// 是非法 glob（portal/NSOpenPanel 上恒不匹配，选择器里选不到任何文件），
			// 按 ';' 拆成独立模式。
			if (filters == null || filters.Length == 0)
			{
				return null;
			}
			var result = filters
				.Where(f => !string.IsNullOrEmpty(f.spec))
				.Select(f => new FilePickerFileType(f.name) { Patterns = SplitPatterns(f.spec) })
				.ToList();
			// 全部条目被空模式筛掉 → 返回 null（无过滤器）而非空列表——
			// 部分 picker 实现把空 FileTypeFilter 与 null 区别对待。
			return result.Count > 0 ? result : null;
		}

		private static string[] SplitPatterns(string spec)
		{
			return spec.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
		}

		private static async Task<IStorageFolder> TryGetStartLocation(IStorageProvider provider, string initialDirectory)
		{
			try
			{
				if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
				{
					return await provider.TryGetFolderFromPathAsync(initialDirectory);
				}
			}
			catch
			{
			}
			return null;
		}

		private static Window GetActiveWindow()
		{
			// WpfApp 全局 using（csproj GlobalUsings），与 Clipboard shim 的取窗逻辑一致。
			return WpfApp.Windows.LastOrDefault(w => w.IsVisible) ?? WpfApp.Windows.LastOrDefault();
		}

		private static T Wait<T>(Task<T> task)
		{
			if (task == null)
			{
				return default;
			}
			if (task.IsCompleted)
			{
				return task.Result;
			}
			// Migration note：嵌套消息循环同步等待 async API（同 Clipboard shim）。
			// 对话框关闭后 task 完成 → frame.Continue=false 退出循环。
			var frame = new DispatcherFrame();
			task.ContinueWith(_ => Dispatcher.UIThread.Post(delegate
			{
				frame.Continue = false;
			}), TaskScheduler.Default);
			Dispatcher.UIThread.PushFrame(frame);
			return task.Status == TaskStatus.RanToCompletion ? task.Result : default;
		}
	}
}
