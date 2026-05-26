using System.Diagnostics;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git
{
	[DebuggerDisplay("{DebugString}")]
	public struct UpstreamStatus
	{
		public static UpstreamStatus Invalid => new UpstreamStatus(-1, -1);

		public int Behind { get; }

		public int Ahead { get; }

		public bool IsValid => Behind != -1;

		private string DebugString
		{
			get
			{
				if (!IsValid)
				{
					return "[removed]";
				}
				if (Behind == 0 && Ahead == 0)
				{
					return "";
				}
				if (Behind > 0)
				{
					if (Ahead > 0)
					{
						return $"{Behind}↓ {Ahead}↑";
					}
					return $"{Behind}↓";
				}
				return $"{Ahead}↑";
			}
		}

		public UpstreamStatus(int behind, int ahead)
		{
			Behind = behind;
			Ahead = ahead;
		}

		public string ToShortDescription()
		{
			if (!IsValid)
			{
				return "";
			}
			if (Ahead > 0)
			{
				if (Behind > 0)
				{
					return $"{Ahead}↑ {Behind}↓";
				}
				return $"{Ahead}↑";
			}
			if (Behind > 0)
			{
				return $"{Behind}↓";
			}
			return "";
		}

		public string ToLongDescription(LocalBranch branch)
		{
			if (!IsValid)
			{
				return "";
			}
			if (Ahead > 0)
			{
				if (Behind > 0)
				{
					return PreferencesLocalization.FormatCurrent("'{0}' {1} commits ahead, {2} commits behind '{3}'", branch.Name, Ahead, Behind, branch.UpstreamFullName);
				}
				return PreferencesLocalization.FormatCurrent("'{0}' {1} commits ahead '{2}'", branch.Name, Ahead, branch.UpstreamFullName);
			}
			if (Behind > 0)
			{
				return PreferencesLocalization.FormatCurrent("'{0}' {1} commits behind '{2}'", branch.Name, Behind, branch.UpstreamFullName);
			}
			return "";
		}
	}
}
