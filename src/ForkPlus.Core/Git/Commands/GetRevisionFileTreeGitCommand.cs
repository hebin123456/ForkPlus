using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetRevisionFileTreeGitCommand
	{
		public GitCommandResult<FileTreeItem[]> Execute(GitModule gitModule, string parentDirectory, Sha parentTreeSha)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("ls-tree", "-z", parentTreeSha.ToString()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<FileTreeItem[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.Nul, StringSplitOptions.RemoveEmptyEntries);
			List<FileTreeItem> list = new List<FileTreeItem>(array.Length);
			string[] array2 = array;
			foreach (string text in array2)
			{
				string[] array3 = text.Split(Consts.Chars.Space);
				if (array3.Length < 2)
				{
					continue;
				}
				FileTreeItem.FileTreeItemType itemType = ParseFileTreeType(array3[1]);
				string[] array4 = text.Split(Consts.Chars.Tab);
				if (array4.Length >= 2)
				{
					if (!Sha.TryParse(array4[0].Split(' ')[2], out var result))
					{
						Log.Error("Cannot parse tree SHA in '" + array4[0].Split(' ')[2] + "'");
					}
					else
					{
						string text2 = array4[1];
						string path = Path.Combine(parentDirectory, text2);
						list.Add(new FileTreeItem(text2, PathHelper.NormalizeUnix(path), result, itemType));
					}
				}
			}
			list.Sort(delegate(FileTreeItem x, FileTreeItem y)
			{
				int num = -1 * x.ItemType.CompareTo(y.ItemType);
				return (num != 0) ? num : NaturalStringComparer.Instance.Compare(x.Filename, y.Filename);
			});
			return GitCommandResult<FileTreeItem[]>.Success(list.ToArray());
		}

		private static FileTreeItem.FileTreeItemType ParseFileTreeType(string text)
		{
			return text switch
			{
				"blob" => FileTreeItem.FileTreeItemType.File, 
				"tree" => FileTreeItem.FileTreeItemType.Directory, 
				"commit" => FileTreeItem.FileTreeItemType.Submodule, 
				_ => FileTreeItem.FileTreeItemType.File, 
			};
		}
	}
}
