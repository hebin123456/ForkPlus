using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ForkPlus.Git.Commands
{
	public class UpdateUserColorGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string email, byte colorId)
		{
			Dictionary<string, byte> dictionary = new GetUserColorsGitCommand().Execute(gitModule);
			// 0 = 无色（UserColorsUserControl 取消选择时传 0）；原代码 `colorId == -1` 对 byte 参数是
			// 死代码（恒 false），取消颜色会写入 "email 0" 而非删除条目。改为 0 时移除。
			if (colorId == 0)
			{
				dictionary.Remove(email);
			}
			else
			{
				dictionary[email] = colorId;
			}
			SaveOrRemoveFile(Path.Combine(gitModule.GitDir(), "fork", "user-colors"), Serialize(dictionary));
			return GitCommandResult.Success();
		}

		private static void SaveOrRemoveFile(string filePath, [Null] string content)
		{
			if (content != null)
			{
				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(filePath));
					File.WriteAllText(filePath, content);
					return;
				}
				catch (Exception ex)
				{
					Log.Error("Failed to save '" + filePath + "'", ex);
					return;
				}
			}
			if (File.Exists(filePath))
			{
				try
				{
					File.Delete(filePath);
				}
				catch (Exception ex2)
				{
					Log.Error("Failed to delete '" + filePath + "'", ex2);
				}
			}
		}

		private static string Serialize(Dictionary<string, byte> userColors)
		{
			if (userColors.Count == 0)
			{
				return null;
			}
			StringBuilder stringBuilder = new StringBuilder(1024);
			foreach (string key in userColors.Keys)
			{
				stringBuilder.AppendLine($"{key} {userColors[key]}");
			}
			return stringBuilder.ToString();
		}
	}
}
