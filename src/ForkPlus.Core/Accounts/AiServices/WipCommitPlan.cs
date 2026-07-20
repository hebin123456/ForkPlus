using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git;

namespace ForkPlus.Accounts.AiServices
{
	/// <summary>AI Commit Composer (WIP拆分) 生成的整个拆分方案。包含多个 <see cref="WipCommitGroup"/>，
	/// 按提交顺序排列。同时记录原始 staged 文件列表，便于校验和"未分组文件"统计。</summary>
	public class WipCommitPlan
	{
		public List<WipCommitGroup> Groups { get; } = new List<WipCommitGroup>();

		/// <summary>原始 staged 文件列表（解析 AI 输出时传入，用于匹配 <see cref="WipCommitGroup.Files"/>）。</summary>
		public List<ChangedFile> StagedFiles { get; } = new List<ChangedFile>();

		public WipCommitPlan()
		{
		}

		public WipCommitPlan(IEnumerable<ChangedFile> stagedFiles)
		{
			if (stagedFiles != null)
			{
				StagedFiles.AddRange(stagedFiles);
			}
		}

		/// <summary>把每个 group 的 Files 字符串与 <see cref="StagedFiles"/> 匹配，填充 MatchedFiles。
		/// 路径比较忽略大小写和分隔符差异（\\ vs /）。</summary>
		public void RebuildMatchedFiles()
		{
			Dictionary<string, ChangedFile> lookup = new Dictionary<string, ChangedFile>();
			foreach (ChangedFile changed in StagedFiles)
			{
				if (changed?.Path == null)
				{
					continue;
				}
				lookup[NormalizePath(changed.Path)] = changed;
				// 重命名/复制的旧路径也加入索引，方便 AI 给出的路径命中
				if (!string.IsNullOrEmpty(changed.OldPath))
				{
					lookup[NormalizePath(changed.OldPath)] = changed;
				}
			}

			foreach (WipCommitGroup group in Groups)
			{
				group.MatchedFiles.Clear();
				if (group.Files == null)
				{
					continue;
				}
				HashSet<ChangedFile> seen = new HashSet<ChangedFile>();
				foreach (string file in group.Files)
				{
					if (string.IsNullOrEmpty(file))
					{
						continue;
					}
					if (lookup.TryGetValue(NormalizePath(file), out ChangedFile matched) && seen.Add(matched))
					{
						group.MatchedFiles.Add(matched);
					}
				}
			}
		}

		/// <summary>原始 staged 文件里没有任何分组引用的文件（AI 漏掉的文件）。
		/// 用户在预览窗口里能看到这部分文件未被覆盖。</summary>
		public List<ChangedFile> GetUnassignedFiles()
		{
			HashSet<ChangedFile> assigned = new HashSet<ChangedFile>();
			foreach (WipCommitGroup group in Groups)
			{
				foreach (ChangedFile matched in group.MatchedFiles)
				{
					assigned.Add(matched);
				}
			}
			return StagedFiles.Where(f => !assigned.Contains(f)).ToList();
		}

		/// <summary>所有分组是否覆盖了全部 staged 文件。</summary>
		public bool IsComplete => GetUnassignedFiles().Count == 0 && Groups.Count > 0;

		private static string NormalizePath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return string.Empty;
			}
			return path.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
		}
	}
}
