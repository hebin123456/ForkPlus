using System;
using System.Collections.Generic;
using System.IO;

namespace ForkPlus
{
	/// <summary>
	/// 首次启动"更新内容"弹窗的数据源：解析随安装目录分发的
	/// Docs/RELEASE_NOTE.md（csproj Content，PreserveNewest 复制到输出），
	/// 提取指定版本的章节（"## v{版本}" 标题行到下一个 "## " 版本标题之间）。
	/// 纯文本解析、路径由调用方注入，便于单元测试覆盖（缺失/边界/多版本竞争）。
	/// </summary>
	public static class ReleaseNotesProvider
	{
		/// <summary>安装目录内随包分发的 RELEASE_NOTE.md 相对路径（与 csproj Content Link 一致）。</summary>
		public static readonly string BundledRelativePath = Path.Combine("Docs", "RELEASE_NOTE.md");

		/// <summary>
		/// 从 markdown 全文提取指定版本章节。version 去除可选 "v"/"V" 前缀后与
		/// "## v{版本}" 标题行整行忽略大小写匹配；命中后收集至下一 "## " 标题前，
		/// 返回去首尾空白的章节文本；版本无效或找不到返回 null。
		/// </summary>
		public static string ExtractVersionSection(string markdown, string version)
		{
			string normalized = UpdateChecker.NormalizeVersion(version);
			if (string.IsNullOrEmpty(normalized) || string.IsNullOrEmpty(markdown))
			{
				return null;
			}
			string expectedHeading = "## v" + normalized;
			string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
			bool inSection = false;
			List<string> collected = new List<string>();
			foreach (string line in lines)
			{
				string trimmed = line.Trim();
				if (trimmed.StartsWith("## ", StringComparison.Ordinal))
				{
					if (inSection)
					{
						break; // 下一版本章节开始，当前章节结束
					}
					inSection = string.Equals(trimmed, expectedHeading, StringComparison.OrdinalIgnoreCase);
					continue;
				}
				if (inSection)
				{
					collected.Add(line);
				}
			}
			if (!inSection)
			{
				return null;
			}
			string text = string.Join("\n", collected).Trim();
			return text.Length == 0 ? null : text;
		}

		/// <summary>
		/// 读取安装目录随包分发的 RELEASE_NOTE.md 并提取版本章节。
		/// 文件缺失/读取失败返回 null（调用方按"无内容"处理，不弹窗不重试）。
		/// </summary>
		public static string GetBundledNotesForVersion(string version)
		{
			string path = Path.Combine(AppContext.BaseDirectory, BundledRelativePath);
			if (!File.Exists(path))
			{
				return null;
			}
			try
			{
				return ExtractVersionSection(File.ReadAllText(path), version);
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to read bundled release notes: " + ex.Message);
				return null;
			}
		}
	}
}
