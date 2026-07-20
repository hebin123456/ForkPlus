namespace ForkPlus.Git
{
	public class SubmoduleChangedFile : ChangedFile
	{
		public Submodule Submodule { get; }

		public SubmoduleChangedFile(Submodule submodule, string path, StatusType status, StatusType workingDirectoryStatus, ChangeType changeType, bool staged, bool isNew = false, bool tracked = true, [Null] string oldPath = null, [Null] string treeIsh = null, [Null] string fileMode = null)
			: base(path, status, workingDirectoryStatus, changeType, staged, isNew, tracked, oldPath, treeIsh, fileMode)
		{
			Submodule = submodule;
		}

		public SubmoduleChangedFile(Submodule submodule, string path, StatusType status, StatusType workingDirectoryStatus = StatusType.None, [Null] string oldPath = null, [Null] string treeIsh = null, [Null] string fileMode = null)
			: base(path, status, workingDirectoryStatus, oldPath, treeIsh, fileMode)
		{
			Submodule = submodule;
		}
	}
}
