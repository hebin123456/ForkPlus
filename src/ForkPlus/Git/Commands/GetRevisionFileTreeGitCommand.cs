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
			return GitCommandResult<FileTreeItem[]>.Success(ParseTree(gitRequestResult.Stdout, parentDirectory, sortPerLevel: true));
		}

		// v5.3.0（文件树过滤）：一次性递归拉取整棵树（-r 递归 blob + -t 连同树对象/目录，
		// 输出为完整相对路径，目录行先于其内容）。供文件树过滤前的"整树补全"使用——懒加载
		// 树只加载已展开目录，过滤需要全量路径集合。输出顺序即 git 树序（父先于子），由
		// 构建方负责分层排序；此处不做层内排序（跨层无法统一排）。
		public GitCommandResult<FileTreeItem[]> ExecuteRecursive(GitModule gitModule, Sha treeSha)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("ls-tree", "-r", "-t", "-z", treeSha.ToString()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<FileTreeItem[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<FileTreeItem[]>.Success(ParseTree(gitRequestResult.Stdout, "", sortPerLevel: false));
		}

		private static FileTreeItem[] ParseTree(string stdout, string parentDirectory, bool sortPerLevel)
		{
			string[] array = stdout.Split(Consts.Chars.Nul, StringSplitOptions.RemoveEmptyEntries);
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
						string path = parentDirectory.Length == 0 ? text2 : Path.Combine(parentDirectory, text2);
						list.Add(new FileTreeItem(text2, PathHelper.NormalizeUnix(path), result, itemType));
					}
				}
			}
			if (sortPerLevel)
			{
				list.Sort(FileTreeItemComparer);
			}
			return list.ToArray();
		}

		// v5.3.0：层内排序规则从 Execute 内联提取共享（目录优先 + 自然排序）——
		// 文件树"整树补全"构建时按层调用同一规则。
		public static readonly Comparison<FileTreeItem> FileTreeItemComparer = delegate(FileTreeItem x, FileTreeItem y)
		{
			int num = -1 * x.ItemType.CompareTo(y.ItemType);
			return (num != 0) ? num : NaturalStringComparer.Instance.Compare(x.Filename, y.Filename);
		};

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
