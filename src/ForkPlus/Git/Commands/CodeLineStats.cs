using System;
using System.Collections.Generic;
using System.Linq;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 代码行数统计结果。由 tokei（https://github.com/XAMPPRocky/tokei）
	/// 对仓库工作区或历史 commit/分支的快照扫描得到，按语言聚合。
	/// </summary>
	public class CodeLineStats
	{
		/// <summary>统计的目标 ref（null/空表示当前工作区 snapshot）。</summary>
		[Null]
		public string RefSpec { get; }

		/// <summary>按语言聚合的明细，按代码行数降序排序。</summary>
		public LanguageStats[] Languages { get; }

		/// <summary>所有语言汇总的文件数。</summary>
		public long TotalFiles { get; }

		/// <summary>所有语言汇总的代码行数。</summary>
		public long TotalCode { get; }

		/// <summary>所有语言汇总的注释行数。</summary>
		public long TotalComments { get; }

		/// <summary>所有语言汇总的空白行数。</summary>
		public long TotalBlanks { get; }

		public CodeLineStats([Null] string refSpec, LanguageStats[] languages)
		{
			RefSpec = refSpec;
			Languages = languages ?? new LanguageStats[0];
			TotalFiles = Languages.Sum(x => x.Files);
			TotalCode = Languages.Sum(x => x.Code);
			TotalComments = Languages.Sum(x => x.Comments);
			TotalBlanks = Languages.Sum(x => x.Blanks);
		}

		/// <summary>从 tokei JSON 解析。tokei 的 JSON schema：顶层 inner 对象按语言名做 key，
		/// 每个值含 reports（per-file）和 blobs/lines 统计字段（blobs 即 files，
		/// lines 含 code/comments/blanks 三字段）。这里只取汇总。</summary>
		public static CodeLineStats FromTokeiJson([Null] string refSpec, string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				return new CodeLineStats(refSpec, new LanguageStats[0]);
			}
			try
			{
				var root = Newtonsoft.Json.Linq.JObject.Parse(json);
				var list = new List<LanguageStats>(root.Count);
				foreach (var prop in root.Properties())
				{
					// tokei 顶层内部对象按语言做 key（如 "Rust"、"C#"），值为 {blobs, files, lines:{code,comments,blanks}, reports:[...]}
					// "Total" 是 tokei 自带的汇总，跳过。
					string name = prop.Name;
					if (name == "Total" || name == "total")
					{
						continue;
					}
					var lang = prop.Value;
					long files = lang.Value<long?>("blobs") ?? lang.Value<long?>("files") ?? 0;
					var lines = lang["lines"];
					long code = lines?.Value<long?>("code") ?? 0;
					long comments = lines?.Value<long?>("comments") ?? 0;
					long blanks = lines?.Value<long?>("blanks") ?? 0;
					if (code == 0 && files == 0)
					{
						continue;
					}
					list.Add(new LanguageStats(name, files, code, comments, blanks));
				}
				// 按代码行数降序、语言名升序排序，方便 UI 展示
				list.Sort((a, b) =>
				{
					int c = b.Code.CompareTo(a.Code);
					return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
				});
				return new CodeLineStats(refSpec, list.ToArray());
			}
			catch (Exception ex)
			{
				Log.Error("Failed to parse tokei JSON output", ex);
				return new CodeLineStats(refSpec, new LanguageStats[0]);
			}
		}
	}

	/// <summary>单一语言的代码行数统计。</summary>
	public class LanguageStats
	{
		public string Name { get; }
		public long Files { get; }
		public long Code { get; }
		public long Comments { get; }
		public long Blanks { get; }

		/// <summary>总行数 = code + comments + blanks。</summary>
		public long Total => Code + Comments + Blanks;

		public LanguageStats(string name, long files, long code, long comments, long blanks)
		{
			Name = name ?? "";
			Files = files;
			Code = code;
			Comments = comments;
			Blanks = blanks;
		}
	}
}
