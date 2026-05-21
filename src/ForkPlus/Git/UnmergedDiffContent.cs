namespace ForkPlus.Git
{
	public class UnmergedDiffContent : DiffContent
	{
		public enum ContentType
		{
			Binary,
			Lfs,
			Submodule,
			Text
		}

		public GitModule GitModule { get; }

		public string DiffString { get; }

		public ContentType FileType { get; }

		public UnmergedDiffContent(GitModule gitModule, ChangedFile changedFile, string diffString, ContentType fileType)
			: base(changedFile)
		{
			GitModule = gitModule;
			DiffString = diffString;
			FileType = fileType;
		}
	}
}
