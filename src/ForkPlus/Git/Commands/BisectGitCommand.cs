using System;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class BisectGitCommand
	{
		public enum BisectCommand
		{
			Start,
			Skip,
			Reset,
			Good,
			Bad
		}

		public GitCommandResult Execute(GitModule gitModule, BisectCommand bisectCommand, JobMonitor monitor)
		{
			// 修复（2026-09-11，"好和坏可以同时选中"）：git 对"同一提交既标记好又标记坏"的行为是
			// 先落库再报错——bisect start 后首个标记（good 或 bad）HEAD 不动（git 等待另一基准），
			// 此时通知条上点另一个标记 = 同一提交标记好+坏：`git bisect bad` 会先写 refs/bisect/bad、
			// 再以退出码 1 输出 "<sha> was both good and bad"（git 2.34/2.50 实证）；双标记一旦落库，
			// 后续所有 bisect 命令（good/bad/skip）全部卡死在同一个报错上，只有 reset 能救。
			// 通知条的 Good/Bad 按钮先后都可点（"可以同时选中"的入口），这里在执行 good/bad 前
			// 预检：当前 HEAD 已带相反标记时，直接返回 git 同款错误（经 BisectCommand 弹 ErrorWindow
			// 提示"不能同时标记好和坏"），不执行命令——既给出 git 的提示，又不毒化 bisect 会话。
			if (bisectCommand == BisectCommand.Good || bisectCommand == BisectCommand.Bad)
			{
				string oppositeMarkConflict = FindOppositeMarkConflict(gitModule, bisectCommand);
				if (oppositeMarkConflict != null)
				{
					return GitCommandResult.Failure(new GitCommandError.GitError(oppositeMarkConflict, string.Empty));
				}
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("bisect", GetBisectCommandName(bisectCommand)).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			if (gitRequestResult.Stdout.Contains("is the first bad commit"))
			{
				return GitCommandResult.Failure(new GitCommandError.GitError(gitRequestResult.Stdout));
			}
			return GitCommandResult.Success();
		}

		/// <summary>good↔bad 互斥预检：当前 HEAD 是否已带相反的 bisect 标记。
		/// 冲突时返回 git 同款错误文案（"<sha> was both good and bad"，与 git bisect 自身
		/// 报错逐字一致），否则返回 null。git 侧标记形态（builtin bisect）：好 =
		/// refs/bisect/good-&lt;sha&gt;（旧术语 old-），可多个；坏 = refs/bisect/bad（旧术语
		/// new），单个。good/bad 无参调用标记的就是当前检出的提交（= rev-parse HEAD）。
		/// 预检读的是 git 实时状态（refs/bisect/*），不受 UI 刷新滞后影响——UI 侧
		/// RepositoryReferences 是异步刷新的，快速连点时会漏判。</summary>
		private static string FindOppositeMarkConflict(GitModule gitModule, BisectCommand bisectCommand)
		{
			try
			{
				GitRequestResult headResult = new GitRequest(gitModule).Command("rev-parse", "HEAD").Execute(silent: true);
				if (!headResult.Success || !Sha.TryParse(headResult.Stdout.Trim(), out Sha headSha))
				{
					return null;
				}
				GitRequestResult refsResult = new GitRequest(gitModule)
				// 修复（2026-09-11，本预检首版漏判的根因）：GitCommand.Add 拼参数不做引号转义
				//（ArgumentsString 直接空格连接），而预检走 Execute(bool) 重载——该重载用
				// ProcessStartInfo.Arguments 整串，.NET 在 Unix 上按空白（空格/Tab）分词：
				// format 里无论用空格还是裸 tab 分隔字段，都会被拆成 "--format=%(refname)" +
				// 模式 "%(objectname)" 两个参数——git 实际只输出 refname，解析恒取不到第二段、
				// 预检恒漏判（E2E 实证：BISECT_LOG 被写入双标记、refs 输出只有 refname 一列）。
				// 修法对齐库内同路径惯例（GetRecentReferencesGitCommand）：format 整段加引号，
				// 引号内 tab 不参与分词，git 收到单一参数 "%(refname)\t%(objectname)"。
				.Command("for-each-ref", "refs/bisect/", "--format=\"%(refname)\t%(objectname)\"")
				.Execute(silent: true);
			if (!refsResult.Success)
			{
				return null;
			}
			string[] lines = refsResult.Stdout.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < lines.Length; i++)
			{
				string[] array = lines[i].Trim().Split('\t');
					if (array.Length != 2 || !Sha.TryParse(array[1], out Sha refSha) || !(refSha == headSha))
					{
						continue;
					}
					bool isGoodMark = array[0].StartsWith("refs/bisect/good-") || array[0].StartsWith("refs/bisect/old-");
					bool isBadMark = array[0] == "refs/bisect/bad" || array[0] == "refs/bisect/new";
					if ((bisectCommand == BisectCommand.Bad && isGoodMark) || (bisectCommand == BisectCommand.Good && isBadMark))
					{
						return headSha.ToString() + " was both good and bad";
					}
				}
				return null;
			}
			catch (Exception)
			{
				// 预检自身异常不拦截（保守放行，交给 git 原有报错路径）
				return null;
			}
		}

		private static string GetBisectCommandName(BisectCommand bisectCommand)
		{
			return bisectCommand switch
			{
				BisectCommand.Start => "start", 
				BisectCommand.Skip => "skip", 
				BisectCommand.Reset => "reset", 
				BisectCommand.Bad => "bad", 
				BisectCommand.Good => "good", 
				_ => throw new Exception(), 
			};
		}
	}
}
