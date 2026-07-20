using System;
using System.Text.RegularExpressions;

namespace ForkPlus.Git
{
	public class AskPassRequest
	{
		public class SshPassphrase : AskPassRequest
		{
			private static readonly Regex Regex = new Regex("Enter\\s+passphrase\\s*for\\s*key\\s*['\"]([^'\"]+)['\"]\\:\\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

			public string KeyPath { get; }

			[Null]
			public new static SshPassphrase Parse(string requestString)
			{
				Match match = Regex.Match(requestString);
				if (match.Success && match.Groups.Count == 2)
				{
					return new SshPassphrase(match.Groups[1].Value);
				}
				return null;
			}

			public SshPassphrase(string keyPath)
			{
				KeyPath = keyPath;
			}
		}

		public class SshUserPassword : AskPassRequest
		{
			private static readonly Regex Regex = new Regex("(\\S+)'s\\s+password:\\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

			public Uri Url { get; }

			public string Username { get; }

			[Null]
			public new static SshUserPassword Parse(string requestString)
			{
				Match match = Regex.Match(requestString);
				if (match.Success && match.Groups.Count == 2)
				{
					try
					{
						Uri uri = new Uri("ssh://" + match.Groups[1].Value);
						string text = ParseUsername(uri);
						if (text != null)
						{
							return new SshUserPassword(uri, text);
						}
					}
					catch (Exception ex)
					{
						Log.Error("Failed to create SSH url", ex);
						return null;
					}
				}
				return null;
			}

			public SshUserPassword(Uri url, string username)
			{
				Url = url;
				Username = username;
			}

			[Null]
			private static string ParseUsername(Uri uri)
			{
				string userInfo = uri.UserInfo;
				if (string.IsNullOrEmpty(userInfo))
				{
					return null;
				}
				int num = userInfo.IndexOf(":");
				return num switch
				{
					0 => null, 
					-1 => userInfo, 
					_ => userInfo.Substring(0, num), 
				};
			}
		}

		[Null]
		public static AskPassRequest Parse(string requestString)
		{
			SshPassphrase sshPassphrase = SshPassphrase.Parse(requestString);
			if (sshPassphrase != null)
			{
				return sshPassphrase;
			}
			SshUserPassword sshUserPassword = SshUserPassword.Parse(requestString);
			if (sshUserPassword != null)
			{
				return sshUserPassword;
			}
			Log.Warn("Cannot parse askpass request '" + requestString + "'");
			return null;
		}
	}
}
