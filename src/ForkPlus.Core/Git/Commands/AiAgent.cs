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
				return FindExistingInstance(new string[1] { "%userprofile%\\.local\\bin\\claude.exe" });
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
