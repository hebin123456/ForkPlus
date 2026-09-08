using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Git;
using Newtonsoft.Json;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 交互式变基的提交消息归档（.git/rebase-merge/fork-message-archive）。
	/// 变基窗口在用户确认变基时把 reword/squash 的目标消息按 sha 归档（见
	/// InteractiveRebaseWindow.SaveMessageArchiveForTodoList）；git 处理到对应指令时
	/// 会调用 core.editor（同为 ForkPlus.RI.exe）编辑 COMMIT_EDITMSG，主进程按
	/// rebase-merge/done 中当前指令的 sha 取回归档消息写回该文件，替代弹出的编辑器。
	/// </summary>
	public static class CommitMessageArchive
	{
		public const string ArchiveFilename = "fork-message-archive";

		private const string TodoShaToken = "#!_";

		private const string RebaseMergeDirectoryName = "rebase-merge";

		/// <summary>
		/// 把归档中当前变基指令对应的提交消息写入 git 正在编辑的消息文件。
		/// 返回 false 表示没有可用归档（调用方应照常放行 git，让其沿用文件中的原消息）。
		/// </summary>
		public static bool TryApplyArchivedMessage(GitModule gitModule, string messageFilePath)
		{
			try
			{
				if (gitModule == null || string.IsNullOrEmpty(messageFilePath) || !File.Exists(messageFilePath))
				{
					return false;
				}
				Sha? currentSha = GetCurrentRebaseSha(gitModule);
				if (!currentSha.HasValue)
				{
					Log.Warn("Cannot apply archived commit message: no executed instruction found in rebase-merge/done.");
					return false;
				}
				Dictionary<string, string> archive = LoadArchive(gitModule);
				string shaKey = currentSha.Value.ToString();
				if (archive == null || !archive.TryGetValue(shaKey, out string message) || string.IsNullOrEmpty(message))
				{
					Log.Info("No archived commit message for " + shaKey + ".");
					return false;
				}
				File.WriteAllText(messageFilePath, message);
				return true;
			}
			catch (Exception ex)
			{
				Log.Warn("Cannot apply archived commit message to '" + messageFilePath + "'", ex);
				return false;
			}
		}

		[Null]
		private static Dictionary<string, string> LoadArchive(GitModule gitModule)
		{
			string archivePath = Path.Combine(gitModule.GitDir(), RebaseMergeDirectoryName, ArchiveFilename);
			if (!File.Exists(archivePath))
			{
				return null;
			}
			return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(archivePath));
		}

		/// <summary>
		/// 取当前正在处理的提交 sha：rebase-merge/done 的最后一条指令行（git 编辑提交消息时，
		/// 对应指令已追加到 done）。兼容三种行格式：
		///   p #!_&lt;sha&gt;              （rebase.instructionFormat 生成，变基窗口首次加载所见）
		///   p &lt;sha&gt; &lt;subject&gt;        （变基窗口确认时覆写 git-rebase-todo 的格式）
		///   pick &lt;sha&gt; # &lt;subject&gt;   （git 默认 todo 格式）
		/// </summary>
		[Null]
		private static Sha? GetCurrentRebaseSha(GitModule gitModule)
		{
			string donePath = Path.Combine(gitModule.GitDir(), RebaseMergeDirectoryName, "done");
			if (!File.Exists(donePath))
			{
				return null;
			}
			string[] lines = File.ReadAllLines(donePath);
			for (int i = lines.Length - 1; i >= 0; i--)
			{
				string line = lines[i].Trim();
				if (line.Length == 0)
				{
					continue;
				}
				string[] parts = line.Split(Consts.Chars.Space, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length < 2)
				{
					continue;
				}
				string shaToken = parts[1];
				if (shaToken.StartsWith(TodoShaToken, StringComparison.Ordinal))
				{
					shaToken = shaToken.Substring(TodoShaToken.Length);
				}
				if (Sha.TryParse(shaToken, out Sha sha))
				{
					return sha;
				}
			}
			return null;
		}
	}
}
