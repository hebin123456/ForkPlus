using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace ForkPlus.Services.Avalonia
{
	/// <summary>
	/// 阶段 6：macOS 文件关联查询，封装 Launch Services 的 <c>lsregister</c> 工具。
	///
	/// macOS 用 Launch Services 管理 URL scheme 与 UTI（Uniform Type Identifier）的默认应用。
	/// 命令行工具 <c>/System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/LaunchServices.framework/Versions/A/Support/lsregister</c>
	/// 查询默认应用。
	///
	/// 简化策略：用 <c>open -a &lt;app&gt; &lt;file&gt;</c> 永远能打开（macOS 行为），
	/// 因此 <c>IsEditorAvailable</c> 始终返回 true（macOS 总有可用的默认编辑器，至少是 TextEdit）。
	/// <c>GetAssociatedExecutable</c> 通过 lsregister 查询，失败返回 null（UI 兜底用 open 命令）。
	/// </summary>
	[SupportedOSPlatform("macos")]
	public class MacOsFileAssociationService : IFileAssociationService
	{
		// lsregister 路径（macOS 10.0+ 稳定）
		private const string LsregisterPath =
			"/System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/" +
			"LaunchServices.framework/Versions/A/Support/lsregister";

		public string GetAssociatedExecutable(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return null;
			}
			try
			{
				string uti = GetUtiForExtension(extension);
				if (uti == null)
				{
					return null;
				}
				string appPath = QueryDefaultAppForUti(uti);
				return appPath;
			}
			catch (Exception ex)
			{
				Log.Error("MacOsFileAssociationService.GetAssociatedExecutable failed for '" + extension + "'", ex);
				return null;
			}
		}

		public bool IsEditorAvailable(string extension)
		{
			// macOS 总能用 `open` 命令打开任意文件（至少回退到 TextEdit），
			// 因此编辑器始终可用，即使没有注册的默认应用。
			// 这与 Linux/Windows 的精确查询语义不同，但与 macOS 用户期望一致。
			return true;
		}

		// 扩展名 → UTI 映射。UTI 是 macOS 的类型标识，如 public.plain-text、public.image。
		// 系统注册的 UTI 列表见 /System/Library/CoreServices/CoreTypes.bundle。
		private static string GetUtiForExtension(string extension)
		{
			string ext = extension.TrimStart('.').ToLowerInvariant();
			if (ext.Length == 0)
			{
				return null;
			}
			switch (ext)
			{
				// 文本
				case "txt": case "log": case "ini": case "cfg": case "conf":
				case "properties": case "env":
					return "public.plain-text";
				case "md": case "markdown":
					return "net.daringfireball.markdown";
				case "json": return "public.json";
				case "xml": return "public.xml";
				case "html": case "htm": return "public.html";
				case "css": return "public.css";
				case "csv": return "public.comma-separated-values-text";
				case "yaml": case "yml": return "public.yaml";
				case "toml": return "org.toml";
				case "rst": return "org.python.restructuredtext";
				case "rtf": return "public.rtf";
				// 源代码（无标准 UTI，统一用 public.source-code）
				case "c": case "h": case "cpp": case "cc": case "cxx":
				case "hpp": case "hxx": case "cs": case "fs": case "java":
				case "kt": case "js": case "mjs": case "ts": case "tsx":
				case "jsx": case "py": case "rb": case "go": case "rs":
				case "swift": case "php": case "sh": case "bash": case "zsh":
				case "lua": case "r": case "pl": case "scala": case "sql":
				case "patch": case "diff": case "vim":
					return "public.source-code";
				// 文档
				case "pdf": return "com.adobe.pdf";
				case "doc": return "com.microsoft.word.doc";
				case "docx": return "org.openxmlformats.wordprocessingml.document";
				case "xls": return "com.microsoft.excel.xls";
				case "xlsx": return "org.openxmlformats.spreadsheetml.sheet";
				case "ppt": return "com.microsoft.powerpoint.ppt";
				case "pptx": return "org.openxmlformats.presentationml.presentation";
				case "odt": return "org.oasis.opendocument.text";
				case "ods": return "org.oasis.opendocument.spreadsheet";
				case "odp": return "org.oasis.opendocument.presentation";
				case "epub": return "org.idpf.epub-container";
				// 图片
				case "png": return "public.png";
				case "jpg": case "jpeg": return "public.jpeg";
				case "gif": return "com.compuserve.gif";
				case "bmp": return "com.microsoft.bmp";
				case "webp": return "org.webmproject.webp";
				case "tiff": case "tif": return "public.tiff";
				case "svg": return "public.svg-image";
				case "ico": return "com.microsoft.ico";
				case "heic": return "public.heic";
				// 视频
				case "mp4": case "m4v": return "public.mpeg-4";
				case "mov": return "com.apple.quicktime-movie";
				case "avi": return "public.avi";
				case "mkv": return "org.matroska.mkv";
				case "webm": return "org.webmproject.webm";
				// 音频
				case "mp3": return "public.mp3";
				case "wav": return "com.microsoft.waveform-audio";
				case "flac": return "org.xiph.flac";
				case "aac": return "public.aac-audio";
				case "ogg": case "oga": return "org.xiph.opus";
				case "m4a": return "public.mpeg-4-audio";
				// 压缩包
				case "zip": return "public.zip-archive";
				case "tar": return "public.tar-archive";
				case "gz": return "org.gnu.gnu-zip-archive";
				case "bz2": return "public.bzip2-archive";
				case "xz": return "org.tukaani.xz-archive";
				case "7z": return "org.7-zip.7-zip-archive";
				case "rar": return "com.rarlab.rar-archive";
				// 字体
				case "ttf": return "public.truetype-ttf-font";
				case "otf": return "public.opentype-font";
				case "woff": return "org.w3c.woff";
				case "woff2": return "org.w3c.woff2";
				// 二进制
				case "exe": case "dll": case "so": case "dylib": case "bin": case "o": case "a":
					return "public.data";
				// ForkPlus 自有
				case "xd2": return "public.data";
				default: return "public.data"; // 通配 UTI，总有默认应用
			}
		}

		// lsregister -find <UTI> 输出包含 "path: <app-path>" 行
		// 例：path: /System/Applications/TextEdit.app
		private static string QueryDefaultAppForUti(string uti)
		{
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = LsregisterPath,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				};
				psi.ArgumentList.Add("-find");
				psi.ArgumentList.Add(uti);
				using (var proc = Process.Start(psi))
				{
					if (proc == null)
					{
						return null;
					}
					proc.WaitForExit(3000);
					if (proc.ExitCode != 0)
					{
						return null;
					}
					string output = proc.StandardOutput.ReadToEnd();
					return ParseAppPath(output);
				}
			}
			catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
			{
				// lsregister 路径变化或不可执行
				Log.Debug("lsregister unavailable: " + ex.Message);
				return null;
			}
		}

		// 解析 lsregister 输出中的 "path: <path>" 行
		// 输出格式：
		//   ------
		//   path: /System/Applications/TextEdit.app
		//   id: com.apple.TextEdit
		//   ...
		private static string ParseAppPath(string output)
		{
			const string marker = "path:";
			foreach (string line in output.Split('\n'))
			{
				string trimmed = line.Trim();
				if (trimmed.StartsWith(marker, StringComparison.Ordinal))
				{
					string path = trimmed.Substring(marker.Length).Trim();
					if (!string.IsNullOrEmpty(path))
					{
						return path;
					}
				}
			}
			return null;
		}
	}
}
