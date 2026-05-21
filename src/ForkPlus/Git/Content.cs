namespace ForkPlus.Git
{
	public abstract class Content
	{
		public string Path { get; }

		public bool IsTracked { get; }

		public Content(string path, bool isTracked)
		{
			Path = path;
			IsTracked = isTracked;
		}
	}
}
