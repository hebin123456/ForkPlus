using System.Collections.Generic;
using System.IO;

namespace ForkPlus.Git.Commands
{
	public class GetUserColorsGitCommand
	{
		public Dictionary<string, byte> Execute(GitModule gitModule)
		{
			Dictionary<string, byte> dictionary = new Dictionary<string, byte>();
			string path = Path.Combine(gitModule.GitDir(), "fork", "user-colors");
			if (!File.Exists(path))
			{
				return dictionary;
			}
			string[] array = File.ReadAllLines(path);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(Consts.Chars.Space);
				if (array2.Length == 2 && byte.TryParse(array2[1], out var result))
				{
					dictionary[array2[0]] = result;
				}
			}
			return dictionary;
		}
	}
}
