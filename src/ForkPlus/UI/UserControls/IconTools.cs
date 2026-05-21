using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

		private static LruCache<string, ImageSource> _defaultFileIconCache = null;

		internal const uint SHGFI_ICON = 256u;

		internal const uint SHGFI_LARGEICON = 0u;

		internal const uint SHGFI_SMALLICON = 1u;

		private const uint SHGFI_USEFILEATTRIBUTES = 16u;

		public static LruCache<string, ImageSource> DefaultFileIconCache
		{
			get
			{
				lock (Padlock)
				{
					if (_defaultFileIconCache == null)
					{
						_defaultFileIconCache = new LruCache<string, ImageSource>(128);
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

		public static ImageSource GetImageSourceForPath(string relativeFilePath, ShellIconSize iconsize = ShellIconSize.SmallIcon)
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

		public static ImageSource GetImageSourceForExtension(string extension, ShellIconSize iconsize = ShellIconSize.SmallIcon)
		{
			LruCache<string, ImageSource> defaultFileIconCache = DefaultFileIconCache;
			if (defaultFileIconCache.TryGet(extension, out var value))
			{
				return value;
			}
			Icon iconForExtension = GetIconForExtension(extension, iconsize);
			if (iconForExtension != null)
			{
				try
				{
					value = Imaging.CreateBitmapSourceFromHIcon(iconForExtension.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
					value.Freeze();
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
		public static ImageSource GetImageSourceForFile(string filePath, ShellIconSize iconsize = ShellIconSize.SmallIcon)
		{
			ImageSource imageSource = null;
			if (!File.Exists(filePath))
			{
				return imageSource;
			}
			try
			{
				imageSource = Imaging.CreateBitmapSourceFromHIcon(Icon.ExtractAssociatedIcon(filePath).Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				imageSource.Freeze();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create bitmap source from icon handle", ex);
			}
			return imageSource;
		}
	}
}
