using System;
using System.Collections.Generic;
using System.IO;

namespace ForkPlus.Shell.Commands
{
	public class GetLocalSshKeysCommand
	{
		public SshKey[] Execute()
		{
			string localSSHDirectory = SystemEnvironment.LocalSSHDirectory;
			if (localSSHDirectory == null)
			{
				return new SshKey[0];
			}
			string[] files;
			try
			{
				files = Directory.GetFiles(localSSHDirectory);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read files in '" + localSSHDirectory + "'", ex);
				return new SshKey[0];
			}
			List<SshKey> list = new List<SshKey>();
			string[] array = files;
			foreach (string text in array)
			{
				if (!text.EndsWith(".pub"))
				{
					continue;
				}
				string text2;
				string fileNameWithoutExtension;
				string rawPublicKey;
				try
				{
					text2 = Path.ChangeExtension(text, null);
					if (!File.Exists(text2))
					{
						Log.Error("Failed to find private key: '" + text2 + "'");
						continue;
					}
					fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
					rawPublicKey = File.ReadAllText(text);
				}
				catch (Exception ex2)
				{
					Log.Error("Failed to find private key '" + text + "'", ex2);
					continue;
				}
				list.Add(new SshKey(text2, fileNameWithoutExtension, rawPublicKey));
			}
			return list.ToArray();
		}
	}
}
