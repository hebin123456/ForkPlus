using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	// WS4 仓库健康仪表盘：仓库历史中最大的 N 个 blob（体积降序）。
	// 两条 git 命令组合，均不走 stdin（GitRequest 不写不关 stdin 时 git 会等 EOF 挂起）：
	//   1) cat-file --batch-all-objects [--unordered] --batch-check=%(objectname),%(objecttype),%(objectsize)
	//      枚举仓库全部对象（pack + loose，含不可达对象）。格式用逗号分隔——GitRequest 经
	//      ProcessStartInfo.Arguments 原样拼接参数串，含空格的参数会被拆成多个 argv。
	//      只保留 type=blob 的行，逐行解析时在内存里维护 Top-N 降序列表（插入排序，N≤10）。
	//   2) rev-list --objects --all 输出 "<sha> <path>" 行，只为 Top-N 的 sha 记录首个出现的
	//      路径（未被任何引用可达的 blob 拿不到路径，回退 abbreviated sha）。
	// 大仓性能考量：cat-file 的 stdout 随对象数线性增长（百万对象 ≈ 数十 MB 字符串），由
	// GitRequestResult 整体缓冲——解析阶段用 StringReader 流式逐行 + Top-N 小列表，额外内存
	// O(N)；--unordered 跳过 git 端的输出排序（结果本来就按体积重排）。
	public class GetBiggestBlobsGitCommand
	{
		public const int DefaultMaxCount = 10;

		public GitCommandResult<(string Path, long Size)[]> Execute(GitModule gitModule, int maxCount = DefaultMaxCount)
		{
			if (maxCount <= 0)
			{
				maxCount = DefaultMaxCount;
			}
			GitRequestResult batchResult = new GitRequest(gitModule)
				.Command("cat-file", "--batch-all-objects", "--unordered", "--batch-check=%(objectname),%(objecttype),%(objectsize)")
				.Execute();
			if (!batchResult.Success)
			{
				return GitCommandResult<(string Path, long Size)[]>.Failure(batchResult.ToGitCommandError());
			}
			List<(string Sha, long Size)> biggest = ReadBiggestBlobs(batchResult.Stdout, maxCount);
			if (biggest.Count == 0)
			{
				return GitCommandResult<(string Path, long Size)[]>.Success(new (string, long)[0]);
			}
			Dictionary<string, string> pathsBySha = ReadPathsForShas(gitModule, biggest);
			(string, long)[] result = new (string, long)[biggest.Count];
			for (int i = 0; i < biggest.Count; i++)
			{
				string path = pathsBySha.TryGetValue(biggest[i].Sha, out string value) && !string.IsNullOrEmpty(value)
					? value
					: biggest[i].Sha.Substring(0, Math.Min(10, biggest[i].Sha.Length));
				result[i] = (path, biggest[i].Size);
			}
			return GitCommandResult<(string Path, long Size)[]>.Success(result);
		}

		/// <summary>流式解析 cat-file --batch-check 输出，维护 Top-N blob 降序列表。</summary>
		private static List<(string Sha, long Size)> ReadBiggestBlobs(string stdout, int maxCount)
		{
			List<(string Sha, long Size)> biggest = new List<(string, long)>(maxCount + 1);
			using (System.IO.StringReader reader = new System.IO.StringReader(stdout ?? ""))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					if (line.Length == 0)
					{
						continue;
					}
					string[] parts = line.Split(',');
					if (parts.Length != 3 || parts[1] != "blob" || !long.TryParse(parts[2], out long size))
					{
						continue;
					}
					InsertSorted(biggest, (parts[0], size), maxCount);
				}
			}
			return biggest;
		}

		/// <summary>按体积降序插入，超出 maxCount 时丢弃尾部。N≤10，插入排序足够。</summary>
		private static void InsertSorted(List<(string Sha, long Size)> list, (string Sha, long Size) item, int maxCount)
		{
			int index = list.Count;
			for (int i = 0; i < list.Count; i++)
			{
				if (item.Size > list[i].Size)
				{
					index = i;
					break;
				}
			}
			if (index < maxCount)
			{
				list.Insert(index, item);
				if (list.Count > maxCount)
				{
					list.RemoveAt(list.Count - 1);
				}
			}
		}

		/// <summary>rev-list --objects --all 建立 sha→路径映射，只保留 Top-N 关心的 sha。</summary>
		private static Dictionary<string, string> ReadPathsForShas(GitModule gitModule, List<(string Sha, long Size)> biggest)
		{
			Dictionary<string, string> pathsBySha = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var (sha, _) in biggest)
			{
				pathsBySha[sha] = null;
			}
			GitRequestResult revListResult = new GitRequest(gitModule).Command("rev-list", "--objects", "--all").Execute();
			if (!revListResult.Success)
			{
				return pathsBySha;
			}
			using (System.IO.StringReader reader = new System.IO.StringReader(revListResult.Stdout ?? ""))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					if (line.Length == 0)
					{
						continue;
					}
					int spaceIndex = line.IndexOf(' ');
					string sha = spaceIndex < 0 ? line : line.Substring(0, spaceIndex);
					if (pathsBySha.TryGetValue(sha, out string existing) && existing == null)
					{
						// 提交行只有 sha（无路径段）；blob/tree 行为 "sha path"，path 可能为空（根 tree）
						pathsBySha[sha] = spaceIndex < 0 ? null : line.Substring(spaceIndex + 1);
					}
				}
			}
			return pathsBySha;
		}
	}
}
