using System.Collections.Generic;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class OpenSubmoduleCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open Submodule In New Tab";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule parentGitModule, Submodule[] submodules)
		{
			List<Submodule> list = new List<Submodule>(submodules.Length);
			List<Submodule> submodulesToInitialize = new List<Submodule>(submodules.Length);
			foreach (Submodule submodule in submodules)
			{
				if (!new IsSubmoduleInitializedGitCommand().Execute(parentGitModule, submodule).Result)
				{
					submodulesToInitialize.Add(submodule);
				}
				else
				{
					list.Add(submodule);
				}
			}
			foreach (Submodule item in list)
			{
				string path = parentGitModule.MakePath(item.Path);
				if (!ServiceLocator.WindowManager.OpenRepository(path, parentGitModule))
				{
					GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(path);
					if (gitCommandResult.Error is GitCommandError.UnsafeRepository)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
					}
				}
			}
			if (submodulesToInitialize.Count == 0)
			{
				return;
			}
			RepositoryUserControl.Commands.UpdateSubmodule.Execute(repositoryUserControl, parentGitModule, submodulesToInitialize.ToArray(), delegate(GitCommandResult updateResult)
			{
				if (!updateResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, updateResult.Error).ShowDialog();
					return;
				}
				foreach (Submodule item2 in submodulesToInitialize)
				{
					string path2 = parentGitModule.MakePath(item2.Path);
					ServiceLocator.WindowManager.OpenRepository(path2, parentGitModule);
				}
			});
		}
	}
}
