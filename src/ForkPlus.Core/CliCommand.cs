namespace ForkPlus
{
	public abstract class CliCommand
	{
		public static CliCommand CreateCliCommand(string[] args)
		{
			CliCommand cliCommand = OpenRepositoryCliCommand.Parse(args);
			if (cliCommand != null)
			{
				return cliCommand;
			}
			return null;
		}

		public virtual void Run(string workingDirectory)
		{
		}
	}
}
