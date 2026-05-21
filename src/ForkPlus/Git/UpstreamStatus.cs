using System.Diagnostics;

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
					return $"'{branch.Name}' {Ahead} commits ahead, {Behind} commits behind '{branch.UpstreamFullName}'";
				}
				return $"'{branch.Name}' {Ahead} commits ahead '{branch.UpstreamFullName}'";
			}
			if (Behind > 0)
			{
				return $"'{branch.Name}' {Behind} commits behind '{branch.UpstreamFullName}'";
			}
			return "";
		}
	}
}
