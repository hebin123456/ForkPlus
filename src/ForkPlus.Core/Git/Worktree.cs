using System.IO;

namespace ForkPlus.Git
{
	public struct Worktree
	{
		public string Path { get; }

		public string HeadString { get; }

		public bool IsMain { get; }

		public bool IsActive { get; }

		public string FriendlyName
		{
			get
			{
				try
				{
					return System.IO.Path.GetFileName(Path);
				}
				catch
				{
					return Path;
				}
			}
		}

		public Worktree(string path, string head, bool isMain, bool isActive)
		{
			Path = path;
			HeadString = head;
			IsMain = isMain;
			IsActive = isActive;
		}

		public bool DataEquals(Worktree worktree)
		{
			if (Path == worktree.Path && HeadString == worktree.HeadString && IsMain == worktree.IsMain)
			{
				return IsActive == worktree.IsActive;
			}
			return false;
		}
	}
}
