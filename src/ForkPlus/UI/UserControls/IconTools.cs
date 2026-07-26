// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → 移除（Int32Rect 无 Avalonia 等价物，不再使用）
// - 移除 using System.Windows.Interop（Imaging.CreateBitmapSourceFromHIcon 无 Avalonia 直接等价物）
// - using System.Windows.Media → using Avalonia.Media（IImage 替代 IImage）
// - using System.Windows.Media.Imaging → using Bitmap = Avalonia.Media.Imaging.Bitmap
//   （别名替代，避免与 System.Drawing.Bitmap 二义性；本文件 GDI+ Bitmap 与 Avalonia Bitmap 同时使用）
// - 新增 using System.Drawing.Imaging（ImageFormat.Png，用于 GDI+ Bitmap → PNG 流转换）
// - IImage → IImage（Avalonia.Media）
// - Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
//   → CreateBitmapFromIcon(icon)：System.Drawing.Icon.ToBitmap() 得 GDI+ Bitmap，
//     PNG 编码入 MemoryStream，再由 Avalonia.Media.Imaging.Bitmap(stream) 加载
// - BitmapSource.Freeze() → 移除（Avalonia Bitmap 构造后即不可变）
// 阶段 5→6 跨平台化：
// - Windows：shell32 SHGetFileInfo（系统文件类型图标）
// - Linux：XDG Icon Theme Specification 查找（mimetype → icon-name → /usr/share/icons/<theme>/<size>/<icon>.png）
//   覆盖 Adwaita / Papirus / Breeze / hicolor 等主流主题；缺失时返回 null（调用方兜底）
// - macOS：NSWorkspace.iconForFileType: via libobjc + AppKit（返回系统文件类型图标）
//   TIFF → PNG 转换后加载为 Avalonia Bitmap
// System.Drawing.Common 在非 Windows 平台靠 OxyPlot.Avalonia 等包传递引入，但官方仅 Windows 受支持，
// GDI+ Icon/Bitmap 在 Unix 上可能抛 PlatformNotSupportedException，已加 try/catch 兜底。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Media;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace ForkPlus.UI.UserControls
{
	public static class IconTools
	{
		private class NativeMethods
		{
			[DllImport("shell32.dll")]
			[SupportedOSPlatform("windows")]
			public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, ShellIconSize uFlags);

			[DllImport("user32.dll", CharSet = CharSet.Auto)]
			[SupportedOSPlatform("windows")]
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

		private static LruCache<string, IImage> _defaultFileIconCache = null;

		internal const uint SHGFI_ICON = 256u;

		internal const uint SHGFI_LARGEICON = 0u;

		internal const uint SHGFI_SMALLICON = 1u;

		private const uint SHGFI_USEFILEATTRIBUTES = 16u;

		public static LruCache<string, IImage> DefaultFileIconCache
		{
			get
			{
				lock (Padlock)
				{
					if (_defaultFileIconCache == null)
					{
						_defaultFileIconCache = new LruCache<string, IImage>(128);
					}
					return _defaultFileIconCache;
				}
			}
		}

		// 阶段 6：跨平台文件图标策略。
		// Windows：SHGetFileInfo 提取系统注册的文件类型图标（与原 WPF 实现一致）。
		// Linux：XDG Icon Theme Specification 查找 —— extension → mimetype → icon-name →
		//        ~/.local/share/icons 与 /usr/share/icons 下的 <theme>/<size>/<icon>.png
		// macOS：NSWorkspace.iconForFileType: via libobjc + AppKit，TIFF → PNG 转 Avalonia Bitmap
		// 非 Windows 平台 GDI+ 不可用，已 try/catch 兜底；返回 null 时调用方 UI 显示通用图标。
		[Null]
		public static Icon GetIconForFile(string filename, ShellIconSize size)
		{
			if (!OperatingSystem.IsWindows())
			{
				return null;
			}
			try
			{
				return GetIconForFileWindows(filename, size);
			}
			catch (Exception ex)
			{
				Log.Debug("GetIconForFile failed on non-Windows or GDI+ unavailable: " + ex.Message);
				return null;
			}
		}

		[SupportedOSPlatform("windows")]
		private static Icon GetIconForFileWindows(string filename, ShellIconSize size)
		{
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

		public static IImage GetImageSourceForPath(string relativeFilePath, ShellIconSize iconsize = ShellIconSize.SmallIcon)
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

		public static IImage GetImageSourceForExtension(string extension, ShellIconSize iconsize = ShellIconSize.SmallIcon)
		{
			LruCache<string, IImage> defaultFileIconCache = DefaultFileIconCache;
			if (defaultFileIconCache.TryGet(extension, out var value))
			{
				return value;
			}
			// 阶段 6：按平台分派 —— Windows 走 SHGetFileInfo；Linux 走 XDG icon theme；macOS 走 NSWorkspace。
			try
			{
				if (OperatingSystem.IsWindows())
				{
					Icon iconForExtension = GetIconForExtension(extension, iconsize);
					if (iconForExtension != null)
					{
						value = CreateBitmapFromIcon(iconForExtension);
					}
				}
				else if (OperatingSystem.IsLinux())
				{
					value = GetImageSourceForExtensionLinux(extension);
				}
				else if (OperatingSystem.IsMacOS())
				{
					value = GetImageSourceForExtensionMacOS(extension);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create bitmap source for extension '" + extension + "'", ex);
			}
			defaultFileIconCache.Put(extension, value);
			return value;
		}

		[Null]
		public static IImage GetImageSourceForFile(string filePath, ShellIconSize iconsize = ShellIconSize.SmallIcon)
		{
			IImage imageSource = null;
			if (!File.Exists(filePath))
			{
				return imageSource;
			}
			// 阶段 6：按平台分派 —— Windows 走 Icon.ExtractAssociatedIcon；Linux/macOS 走扩展名→图标缓存。
			// 文件本身的图标（含自定义缩略图）非 Windows 平台不直接提取，但同一扩展名的系统图标
			// 已通过 GetImageSourceForExtension 跨平台获取，对 UI 列表场景等价。
			if (!OperatingSystem.IsWindows())
			{
				return GetImageSourceForPath(filePath, iconsize);
			}
			try
			{
				// 阶段 4.5：WPF Imaging.CreateBitmapSourceFromHIcon + Freeze → CreateBitmapFromIcon。
				imageSource = CreateBitmapFromIcon(Icon.ExtractAssociatedIcon(filePath));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create bitmap source from icon handle", ex);
			}
			return imageSource;
		}

		// 阶段 4.5：WPF Imaging.CreateBitmapSourceFromHIcon(IntPtr, Int32Rect, BitmapSizeOptions) → Avalonia Bitmap。
		// Avalonia 无 HIcon → Bitmap 直接转换 API；改走 System.Drawing.Icon.ToBitmap() 得到 GDI+ Bitmap，
		// 再以 PNG 编码写入 MemoryStream，由 Avalonia.Media.Imaging.Bitmap(stream) 加载（不可变，无需 Freeze）。
		private static Bitmap CreateBitmapFromIcon(Icon icon)
		{
			using (System.Drawing.Bitmap bitmap = icon.ToBitmap())
			using (MemoryStream ms = new MemoryStream())
			{
				bitmap.Save(ms, ImageFormat.Png);
				ms.Position = 0;
				return new Bitmap(ms);
			}
		}

		// ===== 阶段 6：Linux XDG Icon Theme 查找 =====
		// 扩展名 → mimetype → icon-name → /usr/share/icons/<theme>/<size>/<icon>.png
		// 覆盖 Adwaita / Papirus / Breeze / hicolor 等主流主题；SVG 暂不支持（需光栅化，Avalonia 无内置）。
		[Null]
		private static IImage GetImageSourceForExtensionLinux(string extension)
		{
			string iconName = LinuxIconNames.GetIconNameForExtension(extension);
			if (iconName == null)
			{
				return null;
			}
			string path = LinuxIconThemeLookup.FindIcon(iconName, size: 16);
			if (path == null)
			{
				return null;
			}
			try
			{
				using (FileStream fs = File.OpenRead(path))
				{
					return new Bitmap(fs);
				}
			}
			catch (Exception ex)
			{
				Log.Debug("Failed to load Linux icon '" + path + "': " + ex.Message);
				return null;
			}
		}

		// ===== 阶段 6：macOS NSWorkspace.iconForFileType: =====
		// P/Invoke libobjc + AppKit：
		//   NSImage *img = [NSWorkspace sharedWorkspace].iconForFileType:@"txt"];
		//   NSData *tiff = [img TIFFRepresentation];
		//   NSBitmapImageRep *rep = [[NSBitmapImageRep alloc] initWithData:tiff];
		//   NSData *png = [rep representationUsingType:NSPNGFileType properties:@{}];
		// 然后从 PNG bytes 加载为 Avalonia Bitmap。
		[Null]
		private static IImage GetImageSourceForExtensionMacOS(string extension)
		{
			try
			{
				byte[] pngBytes = MacosIconInterop.GetIconPngForExtension(extension);
				if (pngBytes == null || pngBytes.Length == 0)
				{
					return null;
				}
				using (MemoryStream ms = new MemoryStream(pngBytes))
				{
					return new Bitmap(ms);
				}
			}
			catch (Exception ex)
			{
				Log.Debug("GetImageSourceForExtensionMacOS failed for '" + extension + "': " + ex.Message);
				return null;
			}
		}
	}
}
