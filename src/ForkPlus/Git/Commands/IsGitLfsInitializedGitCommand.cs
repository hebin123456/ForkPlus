using System;
using System.IO;

namespace ForkPlus.Git.Commands
{
	public class IsGitLfsInitializedGitCommand
	{
		public static readonly string LfsPrePushHook = "pre-push";

		public bool Execute(GitModule gitModule)
		{
			Benchmarker benchmarker = new Benchmarker("IsGitLfsInitializedGitCommand");
			try
			{
				string path = gitModule.HookPath(LfsPrePushHook);
				if (!File.Exists(path))
				{
					return false;
				}
				return File.ReadAllText(path).Contains("git lfs pre-push");
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to read pre-push hook file", ex);
				return false;
			}
			finally
			{
				benchmarker.ReportElapsed();
			}
		}
	}
}
