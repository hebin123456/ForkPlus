using System.Collections.Generic;
using System.Windows.Documents;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.CustomCommands
{
	public static class CustomCommandTargetExtensions
	{
		public static Inline[] CreateVariablesList(this CustomCommandTarget target, bool showInternalGitPaths = false, bool showLocalPaths = false)
		{
			List<Inline> list = new List<Inline>();
			if (showInternalGitPaths)
			{
				list.Add(Variable("${git}", "fork git instance"));
				list.Add(Variable("${sh}", "unix shell path"));
			}
			if (showLocalPaths)
			{
				list.Add(Variable("${repo:path}", "repository path"));
			}
			switch (target)
			{
			case CustomCommandTarget.Revision:
				list.Add(Variable("${repo:name}", "repository name"));
				list.Add(Variable("${sha}", "revision sha"));
				list.Add(Variable("${sha:abbr}", "revision sha (abbreviated)"));
				list.Add(Section("Obsolete:"));
				list.Add(Variable("$repository", "repository name"));
				list.Add(Variable("$SHA", "revision sha"));
				list.Add(Variable("$sha", "revision sha (abbreviated)"));
				break;
			case CustomCommandTarget.Repository:
				list.Add(Variable("${repo:name}", "repository name"));
				list.Add(Section("Obsolete:"));
				list.Add(Variable("$repository", "repository name"));
				break;
			case CustomCommandTarget.RepositoryFile:
				list.Add(Variable("${repo:name}", "repository name"));
				list.Add(Variable("${file}", "file path"));
				list.Add(Variable("${file:name}", "file name"));
				list.Add(Variable("${sha}", "revision sha"));
				list.Add(Variable("${sha:abbr}", "revision sha (abbreviated)"));
				list.Add(Section("Obsolete:"));
				list.Add(Variable("$repository", "repository name"));
				list.Add(Variable("$SHA", "revision sha"));
				list.Add(Variable("$sha", "revision sha (abbreviated)"));
				list.Add(Variable("$filepath", "file path"));
				list.Add(Variable("$filename", "file name"));
				break;
			case CustomCommandTarget.Reference:
				list.Add(Variable("${repo:name}", "repository name"));
				list.Add(Variable("${sha}", "revision sha"));
				list.Add(Variable("${sha:abbr}", "revision sha (abbreviated)"));
				list.Add(Variable("${ref}", "branch name"));
				list.Add(Variable("${ref:short}", "branch w/o remote prefix"));
				list.Add(Variable("${ref:full}", "branch full reference"));
				list.Add(Section("Obsolete:"));
				list.Add(Variable("$repository", "repository name"));
				list.Add(Variable("$SHA", "revision sha"));
				list.Add(Variable("$sha", "revision sha (abbreviated)"));
				list.Add(Variable("$name", "branch name"));
				list.Add(Variable("$shortname", "branch w/o remote prefix"));
				list.Add(Variable("$fullreference", "branch full reference"));
				break;
			case CustomCommandTarget.Submodule:
				list.Add(Variable("${repo:name}", "repository name"));
				list.Add(Variable("${submodule}", "submodule"));
				list.Add(Section("Obsolete:"));
				list.Add(Variable("$path", "submodule"));
				list.Add(Variable("$name", "submodule"));
				break;
			}
			if (showLocalPaths)
			{
				list.Add(Variable("$path", "repository path"));
			}
			return list.ToArray();
		}

		private static Run Variable(string variable, string description)
		{
			return new Run(variable + "\t " + Translate(description) + "\n");
		}

		private static Run Section(string text)
		{
			return new Run("\n" + Translate(text) + "\n");
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
