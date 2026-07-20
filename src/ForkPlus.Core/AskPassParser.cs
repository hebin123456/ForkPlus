namespace ForkPlus
{
	public class AskPassParser
	{
		public static string ParseSshKey(string request)
		{
			if (!request.StartsWith("Enter passphrase"))
			{
				return null;
			}
			int num = request.IndexOf('\'');
			if (num == -1)
			{
				return null;
			}
			int num2 = request.IndexOf('\'', num + 1);
			if (num2 == -1)
			{
				return null;
			}
			return request.Substring(num + 1, num2 - num - 1);
		}
	}
}
