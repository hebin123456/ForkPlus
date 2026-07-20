namespace ForkPlus.Git
{
	public class TextContent : Content
	{
		public string Text { get; }

		public TextContent(string path, bool isTracked, string text)
			: base(path, isTracked)
		{
			Text = text;
		}
	}
}
