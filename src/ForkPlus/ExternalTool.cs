namespace ForkPlus
{
	public struct ExternalTool
	{
		public ToolType Type { get; }

		public string Name { get; }

		public string Path { get; }

		public bool PathOverridden { get; }

		public string[] Arguments { get; }

		public bool IsPredefined { get; }

		public bool ArgumentsOverridden { get; }

		public bool IsPrimary { get; }

		public bool IsVisible { get; }

		public ExternalTool(ToolType type, string name, string path, bool pathOverridden, string[] arguments, bool argumentsOverridden, bool isPredefined, bool isPrimary, bool isVisible)
		{
			Type = type;
			Name = name;
			Path = path;
			Arguments = arguments;
			IsPredefined = isPredefined;
			PathOverridden = pathOverridden;
			ArgumentsOverridden = argumentsOverridden;
			IsPrimary = isPrimary;
			IsVisible = isVisible;
		}
	}
}
