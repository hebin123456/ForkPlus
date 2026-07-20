namespace ForkPlus.Git
{
	public class RepositoryRemotes
	{
		public static readonly RepositoryRemotes Empty = new RepositoryRemotes(new Remote[0]);

		public Remote[] Items { get; }

		public RepositoryRemotes(Remote[] remotes)
		{
			Items = remotes;
		}

		public bool DataEquals(RepositoryRemotes other)
		{
			if (Items.Length != other.Items.Length)
			{
				return false;
			}
			for (int i = 0; i < Items.Length; i++)
			{
				if (!Items[i].DataEquals(other.Items[i]))
				{
					return false;
				}
			}
			return true;
		}
	}
}
