namespace ForkPlus
{
	public static class StringHelper
	{
		public static bool IsSha(string line)
		{
			if (line.Length < 5 || line.Length > 40)
			{
				return false;
			}
			foreach (char c in line)
			{
				if ((c <= '/' || c >= ':') && (c <= '`' || c >= 'g') && (c <= '@' || c >= 'G'))
				{
					return false;
				}
			}
			return true;
		}
	}
}
