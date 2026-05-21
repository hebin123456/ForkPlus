namespace ForkPlus.UI
{
	public class Workspace
	{
		public string Name { get; }

		public string[] Repositories { get; set; }

		[Null]
		public string ActiveRepository { get; set; }

		public Workspace(string name, string[] repositories, [Null] string activeRepository)
		{
			Name = name;
			Repositories = repositories;
			ActiveRepository = activeRepository;
		}
	}
}
