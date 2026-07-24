using System.Collections.Generic;
using Avalonia.Input;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowFileHistoryWindowCommand : IUICommand, IForkPlusCommand, IPaletteCommand
	{
		public abstract class Mode
		{
			public class File : Mode
			{
				public File(string path)
					: base(path)
				{
				}
			}

			public class Directory : Mode
			{
				public Directory(string path)
					: base(path)
				{
				}
			}

			public class Hunk : Mode
			{
				public Range LineRange { get; }

				public Hunk(string path, Range lineRange)
					: base(path)
				{
					LineRange = lineRange;
				}
			}

			public string Path { get; }

			public Mode(string path)
			{
				Path = path;
			}
		}

		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("File History...", new Argument[1]
			{
				new Argument(ArgumentType.RepositoryFile)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (arguments[0] is string path)
				{
					RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(repositoryUserControl, new Mode.File(path), null);
				}
			})
		};

		public string Title => "History... ";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl, Mode mode, Sha? sha)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			Sha? headSha = repositoryData.References.HeadSha;
			if (!headSha.HasValue)
			{
				return;
			}
			Sha valueOrDefault = headSha.GetValueOrDefault();
			GitCommandResult<Sha?> gitCommandResult = CommitGraphHelper.FindBranchTip(gitModule, commitGraphCache, valueOrDefault, repositoryData.References.ReferenceStorage, sha);
			Sha? fartherestTipSha = null;
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
				return;
			}
			fartherestTipSha = gitCommandResult.Result;
			List<Reference> source = repositoryData.References.Items.Filter(delegate(Reference x)
			{
				Sha sha2 = x.Sha;
				Sha? sha3 = fartherestTipSha;
				return sha2 == sha3;
			});
			Reference targetReference = IReadOnlyListExtensions.FirstItem(source, (Reference x) => (x as LocalBranch)?.IsActive ?? false) ?? source.FirstItem();
			new FileHistoryWindow(repositoryUserControl, mode, sha, targetReference).Show();
		}
	}
}
