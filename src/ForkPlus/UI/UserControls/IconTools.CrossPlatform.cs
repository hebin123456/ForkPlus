// 阶段 6：IconTools 跨平台辅助类。
// - LinuxIconNames：扩展名 → icon-name 静态映射（覆盖常见开发场景）
// - LinuxIconThemeLookup：XDG icon theme 目录查找（~/.local/share/icons、/usr/share/icons、/usr/share/pixmaps）
// - MacosIconInterop：libobjc + AppKit P/Invoke，NSWorkspace.iconForFileType: → TIFF → PNG bytes
//
// 设计取舍：
// 1. Linux 不依赖 GTK# 或 freedesktop.org 的 GIRepository（增加 AOT 复杂度），
//    改走纯文件系统查找 + 内置 mimetype→icon-name 表。覆盖常见开发场景，
//    未覆盖的扩展名返回 null，调用方 UI 兜底显示通用图标（与 Windows 行为一致）。
// 2. macOS 不引入 Xamarin.Mac /objc-sharp 等大型依赖，直接 libobjc P/Invoke。
//    AOT 兼容性更好（无动态反射），且 NSWorkspace API 自 macOS 10.0 起稳定。
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Media;

namespace ForkPlus.UI.UserControls
{
	/// <summary>
	/// 阶段 6：Linux 文件扩展名 → XDG icon-name 静态映射。
	/// 参考共享 mime-info 规范（freedesktop.org）与 Adwaita/Papirus 主题的命名约定。
	/// 未列出的扩展名返回 null，由调用方 UI 兜底显示通用图标。
	/// </summary>
	internal static class LinuxIconNames
	{
		// icon-name 常量，避免拼写错误，便于多处引用。
		private const string TextGeneric = "text-x-generic";
		private const string ApplicationExecutable = "application-x-executable";
		private const string ImageGeneric = "image-x-generic";
		private const string VideoGeneric = "video-x-generic";
		private const string AudioGeneric = "audio-x-generic";
		private const string FontGeneric = "font-x-generic";
		private const string PackageGeneric = "package-x-generic";
		private const string SourceGeneric = "text-x-script"; // 通用源代码（无更具体图标时）
		private const string FolderGeneric = "folder";
		private const string Markdown = "text-x-markdown";

		// 扩展名（小写，无点）→ icon-name。
		// 选择性覆盖 ForkPlus 实际会展示的文件类型（git 仓库内容、源代码、二进制 diff）。
		private static readonly Dictionary<string, string> s_map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			// 文本/代码
			{ "txt", TextGeneric },
			{ "md", Markdown },
			{ "markdown", Markdown },
			{ "rst", TextGeneric },
			{ "log", TextGeneric },
			{ "ini", TextGeneric },
			{ "cfg", TextGeneric },
			{ "conf", TextGeneric },
			{ "toml", TextGeneric },
			{ "yaml", TextGeneric },
			{ "yml", TextGeneric },
			{ "json", "application-json" },
			{ "xml", "text-xml" },
			{ "html", "text-html" },
			{ "htm", "text-html" },
			{ "css", "text-css" },
			{ "csv", "text-csv" },
			{ "tsv", "text-csv" },
			// 源代码（Adwaita 有 text-x-* 系列专用图标）
			{ "c", "text-x-csrc" },
			{ "h", "text-x-chdr" },
			{ "cpp", "text-x-c++src" },
			{ "cc", "text-x-c++src" },
			{ "cxx", "text-x-c++src" },
			{ "hpp", "text-x-c++hdr" },
			{ "hxx", "text-x-c++hdr" },
			{ "cs", "text-x-csharp" },
			{ "fs", "text-x-fsharp" },
			{ "java", "text-x-java" },
			{ "kt", "text-x-kotlin" },
			{ "js", "application-javascript" },
			{ "mjs", "application-javascript" },
			{ "ts", "application-typescript" },
			{ "tsx", "application-typescript" },
			{ "jsx", "application-javascript" },
			{ "py", "text-x-python" },
			{ "rb", "text-x-ruby" },
			{ "go", "text-x-go" },
			{ "rs", "text-x-rust" },
			{ "swift", "text-x-swift" },
			{ "php", "text-x-php" },
			{ "sh", "application-x-shellscript" },
			{ "bash", "application-x-shellscript" },
			{ "zsh", "application-x-shellscript" },
			{ "ps1", SourceGeneric },
			{ "lua", "text-x-lua" },
			{ "r", "text-x-r" },
			{ "pl", "text-x-perl" },
			{ "scala", "text-x-scala" },
			{ "sql", "text-x-sql" },
			{ "vb", SourceGeneric },
			{ "vim", SourceGeneric },
			// 项目/构建
			{ "csproj", SourceGeneric },
			{ "fsproj", SourceGeneric },
			{ "vbproj", SourceGeneric },
			{ "sln", SourceGeneric },
			{ "vcxproj", SourceGeneric },
			{ "vcproj", SourceGeneric },
			{ "makefile", SourceGeneric },
			{ "mk", SourceGeneric },
			{ "cmake", SourceGeneric },
			{ "gradle", SourceGeneric },
			{ "gemspec", SourceGeneric },
			{ "rake", SourceGeneric },
			// Git
			{ "gitattributes", TextGeneric },
			{ "gitignore", TextGeneric },
			{ "gitmodules", TextGeneric },
			{ "patch", "text-x-patch" },
			{ "diff", "text-x-patch" },
			// 配置 / 键值
			{ "properties", TextGeneric },
			{ "env", TextGeneric },
			// 文档
			{ "pdf", "application-pdf" },
			{ "doc", "application-msword" },
			{ "docx", "application-vnd.openxmlformats-officedocument.wordprocessingml.document" },
			{ "xls", "application-vnd.ms-excel" },
			{ "xlsx", "application-vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
			{ "ppt", "application-vnd.ms-powerpoint" },
			{ "pptx", "application-vnd.openxmlformats-officedocument.presentationml.presentation" },
			{ "odt", "application-vnd.oasis.opendocument.text" },
			{ "ods", "application-vnd.oasis.opendocument.spreadsheet" },
			{ "odp", "application-vnd.oasis.opendocument.presentation" },
			{ "rtf", "application-rtf" },
			{ "epub", "application-epub+zip" },
			// 图片
			{ "png", ImageGeneric },
			{ "jpg", ImageGeneric },
			{ "jpeg", ImageGeneric },
			{ "gif", ImageGeneric },
			{ "bmp", ImageGeneric },
			{ "webp", ImageGeneric },
			{ "tiff", ImageGeneric },
			{ "tif", ImageGeneric },
			{ "svg", ImageGeneric },
			{ "ico", ImageGeneric },
			{ "psd", ImageGeneric },
			{ "raw", ImageGeneric },
			{ "heic", ImageGeneric },
			{ "avif", ImageGeneric },
			// 视频
			{ "mp4", VideoGeneric },
			{ "mkv", VideoGeneric },
			{ "avi", VideoGeneric },
			{ "mov", VideoGeneric },
			{ "webm", VideoGeneric },
			{ "flv", VideoGeneric },
			{ "wmv", VideoGeneric },
			{ "m4v", VideoGeneric },
			{ "mpg", VideoGeneric },
			{ "mpeg", VideoGeneric },
			// 音频
			{ "mp3", AudioGeneric },
			{ "wav", AudioGeneric },
			{ "flac", AudioGeneric },
			{ "aac", AudioGeneric },
			{ "ogg", AudioGeneric },
			{ "oga", AudioGeneric },
			{ "m4a", AudioGeneric },
			{ "wma", AudioGeneric },
			{ "opus", AudioGeneric },
			// 压缩包
			{ "zip", PackageGeneric },
			{ "tar", PackageGeneric },
			{ "gz", PackageGeneric },
			{ "tgz", PackageGeneric },
			{ "bz2", PackageGeneric },
			{ "xz", PackageGeneric },
			{ "7z", PackageGeneric },
			{ "rar", PackageGeneric },
			{ "zst", PackageGeneric },
			{ "lz", PackageGeneric },
			// 字体
			{ "ttf", FontGeneric },
			{ "otf", FontGeneric },
			{ "woff", FontGeneric },
			{ "woff2", FontGeneric },
			// 二进制/可执行
			{ "exe", ApplicationExecutable },
			{ "dll", "application-x-ms-dos-executable" },
			{ "so", ApplicationExecutable },
			{ "dylib", ApplicationExecutable },
			{ "bin", ApplicationExecutable },
			{ "o", ApplicationExecutable },
			{ "a", ApplicationExecutable },
			// ForkPlus 自有类型
			{ "xd2", "application-x-git" }, // ForkPlus 二进制 diff，回退到通用 git 图标（如可用）
		};

		/// <summary>查询扩展名对应的 XDG icon-name。未匹配返回 null。</summary>
		[Null]
		public static string GetIconNameForExtension(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return TextGeneric;
			}
			// 去掉前导点，统一小写
			string key = extension.TrimStart('.').ToLowerInvariant();
			if (key.Length == 0)
			{
				return TextGeneric;
			}
			return s_map.TryGetValue(key, out string name) ? name : null;
		}
	}

	/// <summary>
	/// 阶段 6：XDG Icon Theme Specification 目录查找。
	/// 遍历 $XDG_DATA_HOME 与 $XDG_DATA_DIRS 下的 icons/&lt;theme&gt;/&lt;size&gt;x&lt;size&gt;/&lt;icon&gt;.png
	/// 与 icons/&lt;theme&gt;/&lt;size&gt;/&lt;icon&gt;.png 两种布局（Adwaita 用 NxN，Papirus 用 N）。
	/// 主题优先级：用户配置 → Adwaita → Papirus → Breeze → hicolor（fallback）。
	/// SVG 不支持（Avalonia 无内置光栅化），仅查找 .png。
	/// </summary>
	internal static class LinuxIconThemeLookup
	{
		// 主流主题优先级。hicolor 是 freedesktop 规定的 fallback 主题，必须最后。
		private static readonly string[] s_themes =
		{
			"Adwaita", "Papirus", "Papirus-Dark", "Papirus-Light",
			"Breeze", "Breeze-Dark", "Yaru", "Yaru-dark",
			"ubuntu-mono-dark", "ubuntu-mono-light",
			"Mint-X", "Mint-Y",
			"hicolor" // 必须最后
		};

		// 候选尺寸（按视觉权重降序）。查找时按此顺序尝试，第一个找到即用。
		private static readonly int[] s_sizes = { 16, 22, 24, 32, 48, 64, 128 };

		// icon-name 可能含连字符（如 "text-x-generic"），但有些主题用下划线。
		// 先按原名查找；找不到再尝试下划线变体，提升兼容性。
		[Null]
		public static string FindIcon(string iconName, int size)
		{
			if (string.IsNullOrEmpty(iconName))
			{
				return null;
			}
			// 缓存搜索根目录列表（环境变量通常不变）
			string[] roots = GetSearchRoots();
			string[] nameVariants = GetNameVariants(iconName);
			foreach (string theme in s_themes)
			{
				foreach (int sz in s_sizes)
				{
					foreach (string root in roots)
					{
						string path = TryFindInTheme(root, theme, sz, nameVariants);
						if (path != null)
						{
							return path;
						}
					}
				}
			}
			// /usr/share/pixmaps 直接放图标（无主题层），仅查 <icon>.png 与 <icon>.xpm（xpm 不支持，跳过）
			foreach (string root in roots)
			{
				string pixmapsDir = Path.Combine(root, "pixmaps");
				if (!Directory.Exists(pixmapsDir))
				{
					continue;
				}
				foreach (string name in nameVariants)
				{
					string candidate = Path.Combine(pixmapsDir, name + ".png");
					if (File.Exists(candidate))
					{
						return candidate;
					}
				}
			}
			return null;
		}

		[Null]
		private static string TryFindInTheme(string root, string theme, int size, string[] nameVariants)
		{
			string themeDir = Path.Combine(root, "icons", theme);
			if (!Directory.Exists(themeDir))
			{
				return null;
			}
			// 1) <theme>/<size>x<size>/<context>/<name>.png（Adwaita 布局，含 context 子目录）
			//    也在 <theme>/<size>x<size>/<name>.png 直接查找（无 context 子目录）。
			string sizedDir = Path.Combine(themeDir, size + "x" + size);
			if (Directory.Exists(sizedDir))
			{
				string hit = FindInDirRecursive(sizedDir, nameVariants, maxDepth: 2);
				if (hit != null)
				{
					return hit;
				}
			}
			// 2) <theme>/<size>/<name>.png（Papirus 布局，扁平无 context 子目录）
			string flatDir = Path.Combine(themeDir, size.ToString());
			if (Directory.Exists(flatDir))
			{
				foreach (string name in nameVariants)
				{
					string candidate = Path.Combine(flatDir, name + ".png");
					if (File.Exists(candidate))
					{
						return candidate;
					}
				}
			}
			return null;
		}

		// 在 sizedDir 下递归查找 nameVariants 之一（含 context 子目录，如 apps/mimetypes/actions）。
		// maxDepth=2 容许 <16x16>/<context>/<name>.png。
		[Null]
		private static string FindInDirRecursive(string dir, string[] nameVariants, int maxDepth)
		{
			if (maxDepth < 0 || !Directory.Exists(dir))
			{
				return null;
			}
			foreach (string name in nameVariants)
			{
				string candidate = Path.Combine(dir, name + ".png");
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}
			try
			{
				foreach (string sub in Directory.EnumerateDirectories(dir))
				{
					string hit = FindInDirRecursive(sub, nameVariants, maxDepth - 1);
					if (hit != null)
					{
						return hit;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Debug("LinuxIconThemeLookup: enumerate '" + dir + "' failed: " + ex.Message);
			}
			return null;
		}

		private static string[] GetNameVariants(string iconName)
		{
			// 优先原名，下划线变体作为 fallback（部分老主题用下划线）。
			if (iconName.IndexOf('-') >= 0)
			{
				return new[] { iconName, iconName.Replace('-', '_') };
			}
			if (iconName.IndexOf('_') >= 0)
			{
				return new[] { iconName, iconName.Replace('_', '-') };
			}
			return new[] { iconName };
		}

		private static string[] GetSearchRoots()
		{
			var list = new List<string>(4);
			// $XDG_DATA_HOME（默认 ~/.local/share）
			string xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
			if (!string.IsNullOrEmpty(xdgDataHome))
			{
				list.Add(xdgDataHome);
			}
			else
			{
				string home = Environment.GetEnvironmentVariable("HOME");
				if (!string.IsNullOrEmpty(home))
				{
					list.Add(Path.Combine(home, ".local", "share"));
				}
			}
			// $XDG_DATA_DIRS（默认 /usr/local/share:/usr/share）
			string xdgDataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
			if (string.IsNullOrEmpty(xdgDataDirs))
			{
				xdgDataDirs = "/usr/local/share:/usr/share";
			}
			foreach (string raw in xdgDataDirs.Split(':'))
			{
				if (!string.IsNullOrWhiteSpace(raw))
				{
					list.Add(raw.Trim());
				}
			}
			return list.ToArray();
		}
	}

	/// <summary>
	/// 阶段 6：macOS NSWorkspace.iconForFileType: P/Invoke。
	/// 直接走 libobjc + AppKit 框架，避免引入 Xamarin.Mac / objc-sharp 等大型依赖（AOT 友好）。
	/// 调用链：
	///   NSWorkspace *ws = [NSWorkspace sharedWorkspace];
	///   NSImage *img = [ws iconForFileType:@".txt"];   // 返回系统注册的文件类型图标
	///   NSData *tiff = [img TIFFRepresentation];        // 转 TIFF
	///   NSBitmapImageRep *rep = [[NSBitmapImageRep alloc] initWithData:tiff];
	///   NSData *png = [rep representationUsingType:NSPNGFileType(4) properties:nil];
	///   bytes = [png bytes]; length = [png length];
	///
	/// 内存管理（Objective-C 所有权规则）：
	///   - alloc/init 返回 retained 对象 → 必须 release
	///   - sharedWorkspace / iconForFileType: / TIFFRepresentation / stringWithUTF8String:
	///     / representationUsingType:properties: 都是便利构造方法，返回 autoreleased 对象
	///     → 不主动 release（由当前 autorelease pool 管理）
	/// </summary>
	[SupportedOSPlatform("macos")]
	internal static class MacosIconInterop
	{
		private const string ObjcLib = "/usr/lib/libobjc.dylib";

		// objc_msgSend 在 arm64 上必须用正确签名的变体。这里所有调用都返回 id 或 void，
		// 用 IntPtr 签名（返回对象/指针）和 void 签名（release）足够。
		[DllImport(ObjcLib)]
		private static extern IntPtr objc_getClass(string name);

		[DllImport(ObjcLib)]
		private static extern IntPtr sel_registerName(string name);

		[DllImport(ObjcLib, EntryPoint = "objc_msgSend")]
		private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

		[DllImport(ObjcLib, EntryPoint = "objc_msgSend")]
		private static extern IntPtr objc_msgSend_obj_obj(IntPtr receiver, IntPtr selector, IntPtr arg1);

		[DllImport(ObjcLib, EntryPoint = "objc_msgSend")]
		private static extern IntPtr objc_msgSend_obj_int_obj(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

		[DllImport(ObjcLib, EntryPoint = "objc_msgSend")]
		private static extern void objc_msgSend_release(IntPtr receiver, IntPtr selector, IntPtr arg1);

		// === 选择子缓存（避免每次 sel_registerName）===
		private static readonly IntPtr s_sel_sharedWorkspace = sel_registerName("sharedWorkspace");
		private static readonly IntPtr s_sel_iconForFileType = sel_registerName("iconForFileType:");
		private static readonly IntPtr s_sel_TIFFRepresentation = sel_registerName("TIFFRepresentation");
		private static readonly IntPtr s_sel_alloc = sel_registerName("alloc");
		private static readonly IntPtr s_sel_initWithData = sel_registerName("initWithData:");
		private static readonly IntPtr s_sel_representationUsingType = sel_registerName("representationUsingType:properties:");
		private static readonly IntPtr s_sel_bytes = sel_registerName("bytes");
		private static readonly IntPtr s_sel_length = sel_registerName("length");
		private static readonly IntPtr s_sel_release = sel_registerName("release");
		private static readonly IntPtr s_sel_stringWithUTF8String = sel_registerName("stringWithUTF8String:");

		private static readonly IntPtr s_cls_NSWorkspace = objc_getClass("NSWorkspace");
		private static readonly IntPtr s_cls_NSBitmapImageRep = objc_getClass("NSBitmapImageRep");
		private static readonly IntPtr s_cls_NSMutableString = objc_getClass("NSString");

		// NSPNGFileType = 4（AppKit NSBitmapImageRep.FileType enum）
		private const int NSPNGFileType = 4;

		/// <summary>
		/// 调用 NSWorkspace.iconForFileType: 获取系统文件类型图标，转换为 PNG bytes。
		/// 失败返回 null。调用方负责用 bytes 构造 Avalonia Bitmap。
		/// </summary>
		[Null]
		public static byte[] GetIconPngForExtension(string extension)
		{
			if (!OperatingSystem.IsMacOS())
			{
				return null;
			}
			if (s_cls_NSWorkspace == IntPtr.Zero || s_cls_NSBitmapImageRep == IntPtr.Zero)
			{
				return null;
			}
			string fileType = NormalizeFileType(extension);
			IntPtr fileTypeNs = IntPtr.Zero;
			IntPtr rep = IntPtr.Zero; // 仅 rep（alloc/init 返回）需手动 release
			byte[] result = null;
			try
			{
				IntPtr ws = objc_msgSend(s_cls_NSWorkspace, s_sel_sharedWorkspace);
				if (ws == IntPtr.Zero)
				{
					return null;
				}
				fileTypeNs = CreateNSString(fileType);
				if (fileTypeNs == IntPtr.Zero)
				{
					return null;
				}
				IntPtr img = objc_msgSend_obj_obj(ws, s_sel_iconForFileType, fileTypeNs);
				if (img == IntPtr.Zero)
				{
					return null;
				}
				IntPtr tiff = objc_msgSend(img, s_sel_TIFFRepresentation);
				if (tiff == IntPtr.Zero)
				{
					return null;
				}
				IntPtr allocRep = objc_msgSend(s_cls_NSBitmapImageRep, s_sel_alloc);
				if (allocRep == IntPtr.Zero)
				{
					return null;
				}
				rep = objc_msgSend_obj_obj(allocRep, s_sel_initWithData, tiff);
				if (rep == IntPtr.Zero)
				{
					// initWithData: 失败时 alloc 对象仍需 release（rep 为零则释放原 alloc）
					objc_msgSend_release(allocRep, s_sel_release, IntPtr.Zero);
					rep = allocRep; // 标记给 finally 释放
					return null;
				}
				// representationUsingType:properties: 第二参传 nil（无附加属性）
				IntPtr png = objc_msgSend_obj_int_obj(rep, s_sel_representationUsingType, (IntPtr)NSPNGFileType, IntPtr.Zero);
				if (png == IntPtr.Zero)
				{
					return null;
				}
				IntPtr bytesPtr = objc_msgSend(png, s_sel_bytes);
				IntPtr lengthPtr = objc_msgSend(png, s_sel_length);
				int length = lengthPtr.ToInt32();
				if (bytesPtr == IntPtr.Zero || length <= 0)
				{
					return null;
				}
				result = new byte[length];
				Marshal.Copy(bytesPtr, result, 0, length);
			}
			catch (Exception ex)
			{
				Log.Debug("MacosIconInterop.GetIconPngForExtension failed: " + ex.Message);
				return null;
			}
			finally
			{
				// 仅 release alloc/init 返回的 retained 对象。
				// sharedWorkspace / iconForFileType: / TIFFRepresentation / representationUsingType:
				// / stringWithUTF8String: 都是 autoreleased，由当前 autorelease pool 管理。
				if (rep != IntPtr.Zero)
				{
					objc_msgSend_release(rep, s_sel_release, IntPtr.Zero);
				}
			}
			return result;
		}

		private static string NormalizeFileType(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return ".";
			}
			string s = extension.TrimStart('.');
			return "." + s;
		}

		// NSString 通过 stringWithUTF8String: 构造（autoreleased，无需手动 release）。
		private static IntPtr CreateNSString(string s)
		{
			if (s_cls_NSMutableString == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			// 用 UTF8 编码，零终止字节序列。
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s + "\0");
			IntPtr utf8Ptr = Marshal.AllocHGlobal(bytes.Length);
			try
			{
				Marshal.Copy(bytes, 0, utf8Ptr, bytes.Length);
				return objc_msgSend_obj_obj(s_cls_NSMutableString, s_sel_stringWithUTF8String, utf8Ptr);
			}
			finally
			{
				Marshal.FreeHGlobal(utf8Ptr);
			}
		}
	}
}
