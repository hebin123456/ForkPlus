// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → 移除（Int32Rect 无 Avalonia 等价物，不再使用）
// - 移除 using System.Windows.Interop（Imaging.CreateBitmapSourceFromHIcon 无 Avalonia 直接等价物）
// - using System.Windows.Media → using Avalonia.Media（IImage 替代 ImageSource）
// - using System.Windows.Media.Imaging → using Bitmap = Avalonia.Media.Imaging.Bitmap
//   （别名替代，避免与 System.Drawing.Bitmap 二义性；本文件 GDI+ Bitmap 与 Avalonia Bitmap 同时使用）
// - 新增 using System.Drawing.Imaging（ImageFormat.Png，用于 GDI+ Bitmap → PNG 流转换）
// - ImageSource → IImage（Avalonia.Media）
// - Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
//   → CreateBitmapFromIcon(icon)：System.Drawing.Icon.ToBitmap() 得 GDI+ Bitmap，
//     PNG 编码入 MemoryStream，再由 Avalonia.Media.Imaging.Bitmap(stream) 加载
// - BitmapSource.Freeze() → 移除（Avalonia Bitmap 构造后即不可变）
// 注：Icon / shell32 / user32 P/Invoke 仍为 Windows-only，跨平台替代见阶段 5（phase5-platform-crossplatform.md）。
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

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

		public static Icon GetIconForFile(string filename, ShellIconSize size)
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
			Icon iconForExtension = GetIconForExtension(extension, iconsize);
			if (iconForExtension != null)
			{
				try
				{
					// 阶段 4.5：WPF Imaging.CreateBitmapSourceFromHIcon + Freeze → CreateBitmapFromIcon（Avalonia Bitmap 构造后即不可变）。
					value = CreateBitmapFromIcon(iconForExtension);
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
		public static IImage GetImageSourceForFile(string filePath, ShellIconSize iconsize = ShellIconSize.SmallIcon)
		{
			IImage imageSource = null;
			if (!File.Exists(filePath))
			{
				return imageSource;
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
	}
}
