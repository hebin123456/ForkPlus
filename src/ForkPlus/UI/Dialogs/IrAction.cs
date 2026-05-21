using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	public abstract class IrAction
	{
		public class SelectFirst : IrAction
		{
		}

		public class Squash : IrAction
		{
			public string[] Shas { get; }

			public Squash(string[] shas)
			{
				Shas = shas;
			}
		}

		public class Fixup : IrAction
		{
			public Sha? InitialDst { get; set; }

			public string Sha { get; }

			public Fixup(string sha, Sha? destination = null)
			{
				Sha = sha;
				InitialDst = destination;
			}
		}

		public class Drop : IrAction
		{
			public string[] Shas { get; }

			public Drop(string[] shas)
			{
				Shas = shas;
			}
		}

		public class Reword : IrAction
		{
			public string Sha { get; }

			public Reword(string sha)
			{
				Sha = sha;
			}
		}

		public class Edit : IrAction
		{
			public string Sha { get; }

			public Edit(string sha)
			{
				Sha = sha;
			}
		}

		public class Move : IrAction
		{
			public Sha? InitialDst { get; set; }

			public string Sha { get; }

			public Move(string sha, Sha? destination)
			{
				Sha = sha;
				InitialDst = destination;
			}
		}
	}
}
