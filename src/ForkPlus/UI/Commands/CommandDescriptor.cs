using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class CommandDescriptor
	{
		public delegate void CallConverter(object[] arguments, RepositoryUserControl repositoryUserControl);

		public string Name { get; }

		public Argument[] Arguments { get; }

		public CallConverter Converter { get; }

		public CommandDescriptor(string name, Argument[] arguments, CallConverter converter)
		{
			Name = name;
			Arguments = arguments;
			Converter = converter;
		}
	}
}
