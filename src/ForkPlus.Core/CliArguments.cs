using System;

namespace ForkPlus
{
	public class CliArguments
	{
		private bool _initialized;

		private CliCommand _command;

		public CliCommand Command
		{
			get
			{
				if (!_initialized)
				{
					_command = CliCommand.CreateCliCommand(Environment.GetCommandLineArgs());
					_initialized = true;
				}
				return _command;
			}
		}

		public void RunCommand()
		{
			Command?.Run(null);
		}
	}
}
