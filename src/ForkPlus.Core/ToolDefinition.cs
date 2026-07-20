namespace ForkPlus
{
	public struct ToolDefinition
	{
		public ToolType Type { get; }

		public string FriendlyName => Type switch
		{
			ToolType.Antigravity => "Antigravity", 
			ToolType.Atom => "Atom", 
			ToolType.AraxisMerge => "Araxis Merge", 
			ToolType.BeyondCompare => "Beyond Compare", 
			ToolType.Cursor => "Cursor", 
			ToolType.Fleet => "Fleet", 
			ToolType.GoLand => "GoLand", 
			ToolType.IntelliJIdea => "IntelliJ IDEA", 
			ToolType.KDiff3 => "KDiff3", 
			ToolType.P4Merge => "P4Merge", 
			ToolType.PhpStorm => "PhpStorm", 
			ToolType.PyCharm => "PyCharm", 
			ToolType.Rider => "Rider", 
			ToolType.SublimeText => "Sublime Text", 
			ToolType.VSCode => "VS Code", 
			ToolType.VSCodeInsiders => "Visual Studio Code Insiders", 
			ToolType.VisualStudio => "Visual Studio", 
			ToolType.Unity3d => "YAMLMerge", 
			ToolType.WebStorm => "WebStorm", 
			ToolType.WinMerge => "WinMerge", 
			ToolType.Zed => "Zed", 
			_ => null, 
		};

		public string[] Paths { get; }

		public string[] Arguments { get; }

		public ToolDefinition(ToolType type, string[] paths, string[] arguments)
		{
			Type = type;
			Paths = paths;
			Arguments = arguments;
		}
	}
}
