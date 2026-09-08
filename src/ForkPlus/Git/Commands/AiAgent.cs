using System;
using System.Collections.Generic;
using System.IO;

namespace ForkPlus.Git.Commands
{
	public abstract class AiAgent
	{
		public class Claude : AiAgent
		{
			public override string Name => "Claude";

			public override string Path { get; }

			[Null]
			public static string TryFindInstance()
			{
				// Migration note：原为硬编码 Windows 路径 "%userprofile%\.local\bin\claude.exe"（%VAR%
				// 与反斜杠均不适用于 Unix）。改为跨平台：Unix 的 ~/.local/bin/claude 无 .exe，
				// 同时保留 Windows %userprofile% 展开路径与常见全局安装位置作回退。
				return FindExistingInstance(new string[4]
				{
					global::ForkPlus.SystemEnvironment.UserProfileDirectory + global::System.IO.Path.DirectorySeparatorChar + ".local" + global::System.IO.Path.DirectorySeparatorChar + "bin" + global::System.IO.Path.DirectorySeparatorChar + "claude",
					global::ForkPlus.SystemEnvironment.UserProfileDirectory + global::System.IO.Path.DirectorySeparatorChar + ".local" + global::System.IO.Path.DirectorySeparatorChar + "bin" + global::System.IO.Path.DirectorySeparatorChar + "claude.exe",
					"%userprofile%\\.local\\bin\\claude.exe",
					"/usr/local/bin/claude"
				});
			}

			public Claude(string path)
			{
				Path = path;
			}
		}

		public abstract string Name { get; }

		public abstract string Path { get; }

		public static AiAgent[] GetAvailableAiAgents()
		{
			List<AiAgent> list = new List<AiAgent>(2);
			string text = Claude.TryFindInstance();
			if (text != null)
			{
				list.Add(new Claude(text));
			}
			return list.ToArray();
		}

		[Null]
		protected static string FindExistingInstance(string[] patterns)
		{
			foreach (string text in patterns)
			{
				try
				{
					string text2 = Environment.ExpandEnvironmentVariables(text);
					if (File.Exists(text2))
					{
						return text2;
					}
					return null;
				}
				catch (Exception ex)
				{
					Log.Error("Failed to find agent instance for '" + text + "'", ex);
				}
			}
			return null;
		}
	}
}
