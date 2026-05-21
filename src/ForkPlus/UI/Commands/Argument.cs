using System;

namespace ForkPlus.UI.Commands
{
	public class Argument
	{
		public string Name { get; }

		public ArgumentType Type { get; }

		public object Tag { get; }

		public Argument(ArgumentType type, string name = null, object tag = null)
		{
			Name = name ?? CreateDefaultName(type);
			Type = type;
			Tag = tag;
		}

		private static string CreateDefaultName(ArgumentType type)
		{
			return type switch
			{
				ArgumentType.Tag => "tag", 
				ArgumentType.LocalBranch => "branch", 
				ArgumentType.RemoteBranch => "remote branch", 
				ArgumentType.Branch => "branch", 
				ArgumentType.Reference => "branch or tag", 
				ArgumentType.FeatureBranch => "feature branch", 
				ArgumentType.HotfixBranch => "hotfix branch", 
				ArgumentType.ReleaseBranch => "release branch", 
				ArgumentType.Remote => "remote", 
				ArgumentType.RepositoryFile => "file", 
				ArgumentType.Workspace => "workspace", 
				_ => throw new Exception($"Default name must be defined for argument type {type}"), 
			};
		}
	}
}
