using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace ForkPlus.Services.Avalonia
{
	/// <summary>
	/// 阶段 6：Linux 文件关联查询。
	///
	/// 通过 freedesktop.org 共享 mime-info 规范查询：
	/// 1. <c>xdg-mime query default &lt;mimetype&gt;</c>：返回默认应用的 .desktop 文件名（如 org.gnome.gedit.desktop）
	/// 2. 在 <c>$XDG_DATA_DIRS/applications/</c> 下查找该 .desktop 文件，解析 <c>Exec=</c> 行得到可执行路径
	/// 3. mimetype 推断：通过扩展名 → mimetype 静态映射表（与 IconTools 的 LinuxIconNames 同源）
	///
	/// 与 Windows AssocQueryString 的差异：
	/// - Linux 默认应用是 .desktop 文件而非直接 exe 路径，<c>GetAssociatedExecutable</c> 返回 Exec= 解析后的路径
	/// - <c>IsEditorAvailable</c> 仅检查 .desktop 文件存在且含 Exec=（不验证 exe 是否实际安装）
	/// - xdg-mime 不存在时降级到 mimeapps.list 文件解析（用户级 ~/.local/share/applications/mimeapps.list）
	/// </summary>
	[SupportedOSPlatform("linux")]
	public class LinuxFileAssociationService : IFileAssociationService
	{
		public string GetAssociatedExecutable(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return null;
			}
			try
			{
				string mimeType = GetMimeType(extension);
				if (mimeType == null)
				{
					return null;
				}
				string desktopFile = QueryDefaultApp(mimeType);
				if (string.IsNullOrEmpty(desktopFile))
				{
					return null;
				}
				return ResolveExecutableFromDesktopFile(desktopFile);
			}
			catch (Exception ex)
			{
				Log.Error("LinuxFileAssociationService.GetAssociatedExecutable failed for '" + extension + "'", ex);
				return null;
			}
		}

		public bool IsEditorAvailable(string extension)
		{
			// 与 Windows 实现语义一致：能否拿到非空 executable 路径。
			// 之前在 WindowsFileAssociationService 中非 Windows 平台保守返回 true，
			// 现在改为按实际查询结果返回（更准确）。
			return !string.IsNullOrEmpty(GetAssociatedExecutable(extension));
		}

		// 扩展名 → mimetype 映射。复用 IconTools.CrossPlatform.cs 中 LinuxIconNames 的设计思路，
		// 但这里需要 mimetype（如 "text/plain"）而非 icon-name（如 "text-x-generic"）。
		// mimetype 用 freedesktop 共享 mime-info 数据库的命名约定。
		private static string GetMimeType(string extension)
		{
			string ext = extension.TrimStart('.').ToLowerInvariant();
			if (ext.Length == 0)
			{
				return null;
			}
			// 优先用 file 命令查询（覆盖最全，但需要写临时文件，开销较大）
			// 这里直接走静态映射表（覆盖常见类型），未匹配返回 null
			switch (ext)
			{
				case "txt": case "md": case "markdown": case "rst": case "log":
				case "ini": case "cfg": case "conf": case "toml": case "yaml": case "yml":
				case "properties": case "env": case "vim":
					return "text/plain";
				case "json": return "application/json";
				case "xml": return "application/xml";
				case "html": case "htm": return "text/html";
				case "css": return "text/css";
				case "csv": return "text/csv";
				case "c": case "h": return "text/x-csrc";
				case "cpp": case "cc": case "cxx": case "hpp": case "hxx": return "text/x-c++src";
				case "cs": return "text/x-csharp";
				case "fs": return "text/x-fsharp";
				case "java": return "text/x-java";
				case "kt": return "text/x-kotlin";
				case "js": case "mjs": case "jsx": return "application/javascript";
				case "ts": case "tsx": return "application/typescript";
				case "py": return "text/x-python";
				case "rb": return "text/x-ruby";
				case "go": return "text/x-go";
				case "rs": return "text/x-rust";
				case "swift": return "text/x-swift";
				case "php": return "text/x-php";
				case "sh": case "bash": case "zsh": return "application/x-shellscript";
				case "lua": return "text/x-lua";
				case "r": return "text/x-r";
				case "pl": return "text/x-perl";
				case "scala": return "text/x-scala";
				case "sql": return "text/x-sql";
				case "patch": case "diff": return "text/x-patch";
				case "pdf": return "application/pdf";
				case "png": case "jpg": case "jpeg": case "gif": case "bmp": case "webp":
				case "tiff": case "tif": case "svg": case "ico": case "psd": case "raw":
				case "heic": case "avif":
					return "image/png"; // 通配用 image/png（实际各有独立 mimetype，但默认编辑器查询用此即可）
				case "mp4": case "mkv": case "avi": case "mov": case "webm": case "flv":
				case "wmv": case "m4v": case "mpg": case "mpeg":
					return "video/mp4";
				case "mp3": case "wav": case "flac": case "aac": case "ogg": case "oga":
				case "m4a": case "wma": case "opus":
					return "audio/mpeg";
				case "zip": case "tar": case "gz": case "tgz": case "bz2": case "xz":
				case "7z": case "rar": case "zst": case "lz":
					return "application/zip";
				case "ttf": case "otf": case "woff": case "woff2":
					return "font/ttf";
				case "exe": case "dll": case "so": case "dylib": case "bin": case "o": case "a":
					return "application/x-executable";
				case "doc": case "docx": return "application/vnd.ms-word";
				case "xls": case "xlsx": return "application/vnd.ms-excel";
				case "ppt": case "pptx": return "application/vnd.ms-powerpoint";
				case "odt": return "application/vnd.oasis.opendocument.text";
				case "ods": return "application/vnd.oasis.opendocument.spreadsheet";
				case "odp": return "application/vnd.oasis.opendocument.presentation";
				case "rtf": return "application/rtf";
				case "epub": return "application/epub+zip";
				case "xd2": return "application/octet-stream"; // ForkPlus 自有，无关联
				default: return "application/octet-stream"; // 通配 mimetype，多数桌面有默认编辑器
			}
		}

		// xdg-mime query default <mimetype> → 输出 .desktop 文件名（如 org.gnome.gedit.desktop）
		// xdg-mime 不存在时降级到 mimeapps.list 解析。
		private static string QueryDefaultApp(string mimeType)
		{
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = "xdg-mime",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				};
				psi.ArgumentList.Add("query");
				psi.ArgumentList.Add("default");
				psi.ArgumentList.Add(mimeType);
				using (var proc = Process.Start(psi))
				{
					if (proc == null)
					{
						return LookupFromMimeappsList(mimeType);
					}
					proc.WaitForExit(3000);
					if (proc.ExitCode != 0)
					{
						return LookupFromMimeappsList(mimeType);
					}
					string result = proc.StandardOutput.ReadToEnd().Trim();
					return string.IsNullOrEmpty(result) ? null : result;
				}
			}
			catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
			{
				// xdg-mime 不存在
				return LookupFromMimeappsList(mimeType);
			}
		}

		// Fallback：解析 ~/.local/share/applications/mimeapps.list 与 /usr/share/applications/mimeapps.list
		// 格式：[Default Applications] 段下 mimetype=app.desktop 行
		private static string LookupFromMimeappsList(string mimeType)
		{
			string[] searchPaths = GetApplicationsDirs();
			foreach (string dir in searchPaths)
			{
				string mimeappsFile = Path.Combine(dir, "mimeapps.list");
				if (!File.Exists(mimeappsFile))
				{
					continue;
				}
				try
				{
					bool inDefaultSection = false;
					foreach (string line in File.ReadAllLines(mimeappsFile))
					{
						string trimmed = line.Trim();
						if (trimmed.StartsWith("["))
						{
							inDefaultSection = trimmed.Equals("[Default Applications]", StringComparison.OrdinalIgnoreCase);
							continue;
						}
						if (!inDefaultSection || string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
						{
							continue;
						}
						int eq = trimmed.IndexOf('=');
						if (eq <= 0)
						{
							continue;
						}
						string mime = trimmed.Substring(0, eq).Trim();
						if (string.Equals(mime, mimeType, StringComparison.OrdinalIgnoreCase))
						{
							return trimmed.Substring(eq + 1).Trim();
						}
					}
				}
				catch (Exception ex)
				{
					Log.Debug("Failed to parse '" + mimeappsFile + "': " + ex.Message);
				}
			}
			return null;
		}

		// 在 $XDG_DATA_HOME/applications 与 $XDG_DATA_DIRS/applications 下查找 .desktop 文件
		private static string[] GetApplicationsDirs()
		{
			var list = new System.Collections.Generic.List<string>(4);
			string xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
			if (!string.IsNullOrEmpty(xdgDataHome))
			{
				list.Add(Path.Combine(xdgDataHome, "applications"));
			}
			else
			{
				string home = Environment.GetEnvironmentVariable("HOME");
				if (!string.IsNullOrEmpty(home))
				{
					list.Add(Path.Combine(home, ".local", "share", "applications"));
				}
			}
			string xdgDataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
			if (string.IsNullOrEmpty(xdgDataDirs))
			{
				xdgDataDirs = "/usr/local/share:/usr/share";
			}
			foreach (string raw in xdgDataDirs.Split(':'))
			{
				if (!string.IsNullOrWhiteSpace(raw))
				{
					list.Add(Path.Combine(raw.Trim(), "applications"));
				}
			}
			return list.ToArray();
		}

		// 解析 .desktop 文件的 Exec= 行，提取可执行路径
		// Exec=gedit %U → "gedit"（依赖 PATH 解析）
		// Exec=/usr/bin/gedit %U → "/usr/bin/gedit"
		// Exec="/opt/app/bin/app" %U → "/opt/app/bin/app"
		private static string ResolveExecutableFromDesktopFile(string desktopFileName)
		{
			// desktopFileName 可能是完整路径，也可能是文件名（需在 applications 目录查找）
			string desktopPath;
			if (File.Exists(desktopFileName))
			{
				desktopPath = desktopFileName;
			}
			else
			{
				desktopPath = null;
				foreach (string dir in GetApplicationsDirs())
				{
					string candidate = Path.Combine(dir, desktopFileName);
					if (File.Exists(candidate))
					{
						desktopPath = candidate;
						break;
					}
				}
				if (desktopPath == null)
				{
					return null;
				}
			}
			try
			{
				foreach (string line in File.ReadAllLines(desktopPath))
				{
					if (!line.StartsWith("Exec=", StringComparison.Ordinal))
					{
						continue;
					}
					string execValue = line.Substring("Exec=".Length).Trim();
					// 处理引号包裹的可执行路径
					if (execValue.StartsWith("\""))
					{
						int endQuote = execValue.IndexOf('"', 1);
						if (endQuote > 0)
						{
							return execValue.Substring(1, endQuote - 1);
						}
					}
					// 取第一个空格前的部分作为可执行路径
					int space = execValue.IndexOf(' ');
					return space < 0 ? execValue : execValue.Substring(0, space);
				}
			}
			catch (Exception ex)
			{
				Log.Debug("Failed to parse .desktop file '" + desktopPath + "': " + ex.Message);
			}
			return null;
		}
	}
}
