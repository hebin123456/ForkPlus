using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git;

namespace ForkPlus.Accounts.AiServices
{
	/// <summary>AI Commit Composer (WIP拆分) 生成的单个提交分组。
	/// 包含 commit subject/body、分组文件列表、AI 给出的分组理由。</summary>
	public class WipCommitGroup
	{
		public string Subject { get; set; }

		public string Body { get; set; }

		/// <summary>本分组包含的已暂存文件路径列表（相对仓库根路径）。</summary>
		public List<string> Files { get; } = new List<string>();

		/// <summary>AI 解释为什么这些文件应该归到同一个 commit。</summary>
		public string Reason { get; set; }

		/// <summary>当前分组在原始 staged 文件列表中匹配到的 <see cref="ChangedFile"/> 集合。
		/// 由 <see cref="WipCommitPlan.RebuildMatchedFiles"/> 在解析后填充。</summary>
		public List<ChangedFile> MatchedFiles { get; } = new List<ChangedFile>();

		public WipCommitGroup()
		{
		}

		public WipCommitGroup(string subject, string body = null, string reason = null)
		{
			Subject = subject;
			Body = body;
			Reason = reason;
		}

		/// <summary>合并 subject 和 body 为完整的 commit message（用空行分隔）。</summary>
		public string BuildFullMessage()
		{
			string subject = (Subject ?? "").Trim();
			string body = (Body ?? "").Trim();
			if (string.IsNullOrEmpty(body))
			{
				return subject;
			}
			return subject + "\n\n" + body;
		}

		/// <summary>统计本分组匹配到的 staged 文件数量。</summary>
		public int MatchedFileCount => MatchedFiles.Count;

		/// <summary>本分组是否有未匹配到 staged 文件的路径（AI 给出了不存在的路径）。</summary>
		public bool HasUnmatchedFiles => Files.Count > MatchedFiles.Count;

		/// <summary>未匹配上的文件路径（AI 给出但 staged 区里没有的）。</summary>
		public IEnumerable<string> UnmatchedFiles
		{
			get
			{
				HashSet<string> matched = new HashSet<string>(MatchedFiles.Select(f => f.Path));
				foreach (string file in Files)
				{
					if (!matched.Contains(file))
					{
						yield return file;
					}
				}
			}
		}
	}
}
