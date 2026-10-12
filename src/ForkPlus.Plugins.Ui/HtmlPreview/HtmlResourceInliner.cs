// HTML 内嵌资源内联器（HTML 预览共享组件的一部分）。
//
// 容器类文档（CHM / EPUB）里的 HTML 引用的是容器内部路径（如 /images/logo.png、
// images/cover.jpg），WebView 直接渲染时无法解析。此内联器把这些引用替换为 data URI，
// 让文档成为自包含 HTML——无需注册自定义协议，WebView NavigateToString 即可正确显示。
using System;
using System.Text.RegularExpressions;

namespace ForkPlus.Plugins.Ui.HtmlPreview
{
	/// <summary>把 HTML 中外部引用的资源替换为 data URI 的内联器。</summary>
	public static class HtmlResourceInliner
	{
		private static readonly Regex AttrRegex = new Regex(
			"(?<attr>\\b(?:src|href)\\s*=\\s*)(?<quote>[\"'])(?<url>[^\"']*)\\k<quote>",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>内联资源：<paramref name="data"/> 为原始字节；<paramref name="mimeType"/>
		/// 可为空（按扩展名推断）。</summary>
		public sealed class InlinedResource
		{
			public InlinedResource(byte[] data, string mimeType = null)
			{
				Data = data;
				MimeType = mimeType;
			}

			public byte[] Data { get; }

			public string MimeType { get; }
		}

		/// <summary>把 HTML 里外部引用的资源替换为 data URI。<paramref name="resolve"/> 返回
		/// null 表示该引用内联不了（保持原样）；相对路径由调用方在 resolve 内按容器规则解析。</summary>
		public static string Inline(string html, Func<string, InlinedResource> resolve)
		{
			if (string.IsNullOrEmpty(html) || resolve == null)
			{
				return html;
			}
			return AttrRegex.Replace(html, match =>
			{
				string url = match.Groups["url"].Value;
				if (IsExternal(url))
				{
					return match.Value;
				}
				InlinedResource resource;
				try
				{
					resource = resolve(url);
				}
				catch
				{
					return match.Value;
				}
				if (resource?.Data == null)
				{
					return match.Value;
				}
				string mime = string.IsNullOrEmpty(resource.MimeType) ? GuessMimeType(url) : resource.MimeType;
				string dataUri = "data:" + mime + ";base64," + Convert.ToBase64String(resource.Data);
				return match.Groups["attr"].Value + match.Groups["quote"].Value + dataUri + match.Groups["quote"].Value;
			});
		}

		/// <summary>是否为无需内联的外部/特殊引用：带 scheme（data: / http: / https: / mailto: /
		/// javascript: …）、协议相对（//）或纯锚点（#）。以 / 开头的容器内绝对路径**不**算外部，
		/// 交给 resolve 处理。</summary>
		private static bool IsExternal(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
			{
				return true;
			}
			char first = url[0];
			if (first == '#' || first == '?')
			{
				return true;
			}
			if (url.StartsWith("//", StringComparison.Ordinal))
			{
				return true;
			}
			int colon = url.IndexOf(':');
			int slash = url.IndexOf('/');
			return colon > 0 && (slash < 0 || colon < slash);
		}

		private static string GuessMimeType(string url)
		{
			string ext;
			try
			{
				ext = System.IO.Path.GetExtension(url ?? string.Empty).ToLowerInvariant();
			}
			catch
			{
				ext = string.Empty;
			}
			switch (ext)
			{
				case ".png": return "image/png";
				case ".jpg":
				case ".jpeg": return "image/jpeg";
				case ".gif": return "image/gif";
				case ".webp": return "image/webp";
				case ".bmp": return "image/bmp";
				case ".svg": return "image/svg+xml";
				case ".ico": return "image/x-icon";
				case ".css": return "text/css";
				case ".js": return "application/javascript";
				case ".woff": return "font/woff";
				case ".woff2": return "font/woff2";
				case ".ttf": return "font/ttf";
				default: return "application/octet-stream";
			}
		}
	}
}
