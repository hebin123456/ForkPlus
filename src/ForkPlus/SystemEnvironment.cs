using System;
using System.IO;

namespace ForkPlus
{
	public static class SystemEnvironment
	{
		[Null]
		public static string LocalSSHDirectory
		{
			get
			{
				try
				{
					string text = Environment.ExpandEnvironmentVariables("%HOME%");
					if (Directory.Exists(text))
					{
						return Path.Combine(text, ".ssh");
					}
				}
				catch
				{
				}
				try
				{
					return Path.Combine(Environment.ExpandEnvironmentVariables("%userprofile%"), ".ssh");
				}
				catch
				{
					return null;
				}
			}
		}
	}
}
