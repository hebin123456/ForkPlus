using System.Text;

namespace ForkPlus.Git
{
	public class CredentialHelperArguments
	{
		public string Host { get; }

		public string Protocol { get; }

		[Null]
		public string Username { get; set; }

		[Null]
		public string Password { get; set; }

		[Null]
		public static CredentialHelperArguments Parse(string rawDescription)
		{
			string text = null;
			string text2 = null;
			string username = null;
			string[] array = rawDescription.Split(Consts.Chars.NewLine);
			foreach (string text3 in array)
			{
				int num = text3.IndexOf('=');
				if (num != -1)
				{
					string text4 = text3.Substring(0, num);
					string text5 = text3.Substring(num + 1);
					switch (text4)
					{
					case "protocol":
						text = text5;
						break;
					case "host":
						text2 = text5;
						break;
					case "username":
						username = text5;
						break;
					default:
						Log.Warn("Unknown credentials description parameter: '" + text3 + "'");
						break;
					}
				}
			}
			if (text == null)
			{
				Log.Error("Credentials description doesn't contain protocol;");
				return null;
			}
			if (text2 == null)
			{
				Log.Error("Credentials description doesn't contain protocol;");
				return null;
			}
			return new CredentialHelperArguments(text2, text, username);
		}

		public CredentialHelperArguments(string host, string protocol, [Null] string username)
		{
			Host = host;
			Protocol = protocol;
			Username = username;
		}

		public string Export()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("protocol=");
			stringBuilder.Append(Protocol);
			stringBuilder.Append("\n");
			stringBuilder.Append("host=");
			stringBuilder.Append(Host);
			stringBuilder.Append("\n");
			string username = Username;
			if (username != null)
			{
				stringBuilder.Append("username=");
				stringBuilder.Append(username);
				stringBuilder.Append("\n");
			}
			string password = Password;
			if (password != null)
			{
				stringBuilder.Append("password=");
				stringBuilder.Append(password);
				stringBuilder.Append("\n");
			}
			stringBuilder.Append("\n");
			return stringBuilder.ToString();
		}
	}
}
