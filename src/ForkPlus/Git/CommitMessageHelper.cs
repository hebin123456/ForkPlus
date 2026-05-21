namespace ForkPlus.Git
{
	internal static class CommitMessageHelper
	{
		private const string CommitMessageSeparator = "\n\n";

		public static void SplitCommitBody(string commitBody, out string subject, out string description, string separator = "\n\n")
		{
			commitBody = commitBody ?? string.Empty;
			int num = commitBody.IndexOf(separator);
			if (num != -1)
			{
				subject = commitBody.Substring(0, num);
				description = commitBody.Substring(num + separator.Length);
			}
			else
			{
				subject = commitBody.Trim();
				description = "";
			}
		}

		public static string CreateCommitBody(string subject, string description)
		{
			subject = subject.Trim(Consts.Chars.NewLines);
			description = description.TrimEnd();
			if (string.IsNullOrEmpty(description))
			{
				return subject;
			}
			return subject + "\n\n" + description;
		}
	}
}
