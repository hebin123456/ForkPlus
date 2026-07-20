namespace ForkPlus.Git
{
	public class TextDiffContent : DiffContent
	{
		public int TabWidth { get; }

		public bool EntireFile { get; }

		public string Text { get; }

		public TextDiffContent(ChangedFile changedFile, string text, int tabWidth, bool entireFile)
			: base(changedFile)
		{
			Text = text;
			TabWidth = tabWidth;
			EntireFile = entireFile;
		}
	}
}
