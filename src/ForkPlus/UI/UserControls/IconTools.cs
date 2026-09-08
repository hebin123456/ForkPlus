using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
// Migration note：Imaging / Int32Rect / BitmapSizeOptions 来自 WPF System.Windows.Media.Imaging，
// 兼容层已在 WpfCompat.Batch2.cs 重建同名命名空间，这里显式引入。
using System.Windows.Media.Imaging;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.UserControls
{
	public static class IconTools
	{
		private class NativeMethods
		{
			[DllImport("shell32.dll")]
			public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, ShellIconSize uFlags);

			[DllImport("user32.dll", CharSet = CharSet.Auto)]
			public static extern bool DestroyIcon(IntPtr handle);
		}

		private struct SHFILEINFO
		{
			public IntPtr hIcon;

			public IntPtr iIcon;

			public uint dwAttributes;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szDisplayName;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			public string szTypeName;
		}

		private static readonly object Padlock = new object();

		private static LruCache<string, global::Avalonia.Media.IImage> _defaultFileIconCache = null;

		internal const uint SHGFI_ICON = 256u;

		internal const uint SHGFI_LARGEICON = 0u;

		internal const uint SHGFI_SMALLICON = 1u;

		private const uint SHGFI_USEFILEATTRIBUTES = 16u;

		public static LruCache<string, global::Avalonia.Media.IImage> DefaultFileIconCache
		{
			get
			{
				lock (Padlock)
				{
					if (_defaultFileIconCache == null)
					{
						_defaultFileIconCache = new LruCache<string, global::Avalonia.Media.IImage>(128);
					}
					return _defaultFileIconCache;
				}
			}
		}

		public static Icon GetIconForFile(string filename, ShellIconSize size)
		{
			// Migration note：SHGetFileInfo（shell32.dll）是 Windows 专属。Linux/macOS 抛
			// DllNotFoundException——实证：点击提交行 → RevisionDetails 文件列表建图标缓存
			// → 崩溃整个应用（2026-08-30 fork.log）。Unix 无系统图标 API，返回 null，
			// 由 GetImageSourceForExtension 提供 BinaryFile 占位图标。
			if (!OperatingSystem.IsWindows())
			{
				return null;
			}
			SHFILEINFO psfi = default(SHFILEINFO);
			NativeMethods.SHGetFileInfo(filename, 0u, ref psfi, (uint)Marshal.SizeOf(psfi), size);
			Icon result = null;
			if (psfi.hIcon.ToInt32() != 0)
			{
				result = (Icon)Icon.FromHandle(psfi.hIcon).Clone();
				NativeMethods.DestroyIcon(psfi.hIcon);
			}
			return result;
		}

		public static Icon GetIconForExtension(string extension, ShellIconSize size)
		{
			if (string.IsNullOrEmpty(extension))
			{
				extension = ".xd2";
			}
			size |= (ShellIconSize)16u;
			return GetIconForFile(extension, size);
		}

		public static global::Avalonia.Media.IImage GetImageSourceForPath(string relativeFilePath, ShellIconSize iconsize = ShellIconSize.SmallIcon)
		{
			string extension;
			try
			{
				extension = Path.GetExtension(relativeFilePath);
			}
			catch
			{
				extension = ".xd2";
			}
			return GetImageSourceForExtension(extension, iconsize);
		}

		// Migration note：Unix 无 SHGetFileInfo/ExtractAssociatedIcon（shell32 + System.Drawing 均
		// Windows 专属）。用内置 BinaryFile 图标做统一占位（96x128 原图，列表显示时按目标
		// 尺寸缩放），保证文件列表/历史/blame 等视图有图标可显示。后续可按 freedesktop
		// 图标主题（xdg-icon/resource）实现按扩展名取真实图标。
		[Null]
		private static global::Avalonia.Media.IImage _unixPlaceholderFileIcon;

		[Null]
		private static global::Avalonia.Media.IImage UnixPlaceholderFileIcon
		{
			get
			{
				if (_unixPlaceholderFileIcon == null)
				{
					try
					{
						_unixPlaceholderFileIcon = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://ForkPlus/Assets/BinaryFile.png")));
					}
					catch (Exception ex)
					{
						Log.Error("Failed to load placeholder file icon", ex);
					}
				}
				return _unixPlaceholderFileIcon;
			}
		}

		public static global::Avalonia.Media.IImage GetImageSourceForExtension(string extension, ShellIconSize iconsize = ShellIconSize.SmallIcon)
		{
			LruCache<string, global::Avalonia.Media.IImage> defaultFileIconCache = DefaultFileIconCache;
			if (defaultFileIconCache.TryGet(extension, out var value))
			{
				return value;
			}
			if (!OperatingSystem.IsWindows())
			{
				value = UnixPlaceholderFileIcon;
				defaultFileIconCache.Put(extension, value);
				return value;
			}
			Icon iconForExtension = GetIconForExtension(extension, iconsize);
		if (iconForExtension != null)
		{
			try
			{
				// Migration note：WPF Imaging.CreateBitmapSourceFromHIcon 由兼容层 stub 提供
				//（当前返回 null，GDI HICON → Avalonia Bitmap 转换待补）。
				value = Imaging.CreateBitmapSourceFromHIcon(iconForExtension.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			}
				catch (Exception ex)
				{
					Log.Error("Failed to create bitmap source from icon handle", ex);
				}
			}
			defaultFileIconCache.Put(extension, value);
			return value;
		}

		[Null]
		public static global::Avalonia.Media.IImage GetImageSourceForFile(string filePath, ShellIconSize iconsize = ShellIconSize.SmallIcon)
		{
			global::Avalonia.Media.IImage imageSource = null;
			if (!File.Exists(filePath))
			{
				return imageSource;
			}
			// Migration note：Icon.ExtractAssociatedIcon（System.Drawing.Common）在 .NET 6+ 仅支持
			// Windows，Unix 走 BinaryFile 占位图标（同 GetImageSourceForExtension）。
			if (!OperatingSystem.IsWindows())
			{
				return UnixPlaceholderFileIcon;
			}
			try
		{
			// Migration note：WPF Imaging.CreateBitmapSourceFromHIcon 由兼容层 stub 提供
			//（当前返回 null，GDI HICON → Avalonia Bitmap 转换待补）。
			imageSource = Imaging.CreateBitmapSourceFromHIcon(Icon.ExtractAssociatedIcon(filePath).Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		}
			catch (Exception ex)
			{
				Log.Error("Failed to create bitmap source from icon handle", ex);
			}
			return imageSource;
		}
	}
}
