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

		/// <summary>
		/// git 2.39+ 凭据协议 v2 的能力协商参数（capability[]=authtype / capability[]=state 等）。
		/// 本 helper 是 v1-only（仅用户名/密码），不实现 authtype/state；保留这些仅用于识别
		/// （避免落 default 分支打 "Unknown credentials description parameter" 警告），响应时不回显。
		/// </summary>
		public System.Collections.Generic.IReadOnlyList<string> Capabilities { get; [Null] private set; }

		/// <summary>
		/// git 2.43+ 在 401 重试时下发的 WWW-Authenticate 头值（wwwauth[]=Basic realm="..."）。
		/// v1 helper 无需消费——git 自行按 Basic 走账密；保留仅用于识别，避免误警。
		/// </summary>
		public System.Collections.Generic.IReadOnlyList<string> WwwAuthHeaders { get; [Null] private set; }

		[Null]
		public static CredentialHelperArguments Parse(string rawDescription)
		{
			string text = null;
			string text2 = null;
			string username = null;
			string password = null;
			System.Collections.Generic.List<string> capabilities = null;
			System.Collections.Generic.List<string> wwwAuth = null;
			string[] array = rawDescription.Split(Consts.Chars.NewLine);
			foreach (string text3 in array)
			{
				int num = text3.IndexOf('=');
				if (num != -1)
				{
					string text4 = text3.Substring(0, num);
					string text5 = text3.Substring(num + 1);
					// git 2.39+ 凭据协议 v2：capability[]=xxx / wwwauth[]=xxx 是数组型参数，
					// 键本身含 '=' 之后的 '[]' 标记。先按前缀识别，避免落 default 打误警。
					if (text4 == "capability[]")
					{
						(capabilities ?? (capabilities = new System.Collections.Generic.List<string>())).Add(text5);
					}
					else if (text4 == "wwwauth[]")
					{
						(wwwAuth ?? (wwwAuth = new System.Collections.Generic.List<string>())).Add(text5);
					}
					else
					{
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
						case "password":
							// 凭据收编（Layer C）：git 在 store/erase 时把完整凭据（含 password 行）
							// 写到 helper 的 stdin，get 时不含。此前该行落 default 分支打
							// "Unknown credentials description parameter" 警告，且 store 语义拿不到密码。
							password = text5;
							break;
						default:
							Log.Warn("Unknown credentials description parameter: '" + text3 + "'");
							break;
						}
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
			return new CredentialHelperArguments(text2, text, username)
			{
				Password = password,
				Capabilities = capabilities,
				WwwAuthHeaders = wwwAuth
			};
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
