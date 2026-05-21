namespace ForkPlus.Git
{
	public class FileTreeItem
	{
		public enum FileTreeItemType
		{
			File,
			Directory,
			Submodule
		}

		public string Filename { get; }

		public string FilePath { get; }

		public Sha TreeSha { get; }

		public FileTreeItemType ItemType { get; }

		public FileTreeItem(string filename, string filePath, Sha treeSha, FileTreeItemType itemType)
		{
			Filename = filename;
			FilePath = filePath;
			TreeSha = treeSha;
			ItemType = itemType;
		}
	}
}
