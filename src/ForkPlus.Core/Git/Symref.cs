namespace ForkPlus.Git
{
	public struct Symref
	{
		public string Name { get; }

		public string Target { get; }

		public Symref(string name, string target)
		{
			Name = name;
			Target = target;
		}
	}
}
