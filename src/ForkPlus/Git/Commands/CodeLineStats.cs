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

		/// <summary>从 tokei JSON 解析。tokei 的 JSON schema（顶层 inner 对象按语言名做 key，
	/// "Total"/"total" 是汇总跳过）有两种版本：
	/// - v14（CodeStats 结构平铺）：{"Rust":{"blanks":N,"code":N,"comments":N,"blobs":{...},"reports":[...]}}
	///   code/comments/blanks 直接在语言对象下；lines() 是方法不序列化；files 数量 = reports 数组长度。
	/// - v13（嵌套 lines 对象）：{"Rust":{"blobs":N,"files":N,"lines":{"code":N,"comments":N,"blanks":N}}}
	///   blobs/files 是数字，lines 是含 code/comments/blanks 的对象。
	/// 这里两种都兼容，优先按 v14 平铺解析。</summary>
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
				// tokei 顶层内部对象按语言做 key（如 "Rust"、"C#"）；"Total"/"total" 是 tokei 自带汇总，跳过
				string name = prop.Name;
				if (name == "Total" || name == "total")
				{
					continue;
				}
				var lang = prop.Value;

				// files：v14 用 reports 数组长度；v13 用 blobs/files 数字字段
				long files;
				var reports = lang["reports"] as Newtonsoft.Json.Linq.JArray;
				if (reports != null)
				{
					files = reports.Count;
				}
				else
				{
					// v13 的 blobs 是数字；v14 的 blobs 是 map（此时 Value<long?> 会返回 null，退化到 files 字段）
					files = lang.Value<long?>("blobs") ?? lang.Value<long?>("files") ?? 0;
				}

				// code/comments/blanks：v14 平铺在语言对象下；v13 嵌套在 lines 对象下
				long code, comments, blanks;
				var linesObj = lang["lines"] as Newtonsoft.Json.Linq.JObject;
				if (linesObj != null)
				{
					// v13 嵌套格式
					code = linesObj.Value<long?>("code") ?? 0;
					comments = linesObj.Value<long?>("comments") ?? 0;
					blanks = linesObj.Value<long?>("blanks") ?? 0;
				}
				else
				{
					// v14 平铺格式
					code = lang.Value<long?>("code") ?? 0;
					comments = lang.Value<long?>("comments") ?? 0;
					blanks = lang.Value<long?>("blanks") ?? 0;
				}

				if (code == 0 && comments == 0 && blanks == 0 && files == 0)
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
