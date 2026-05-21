using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowAskPassWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(string request, bool noPrompt, string repositoryPath, out string result)
		{
			AskPassRequest askPassRequest = AskPassRequest.Parse(request);
			if (askPassRequest != null)
			{
				string text = QueryFromWindowsCredentialManager(askPassRequest);
				if (text != null)
				{
					result = text;
					return;
				}
			}
			if (noPrompt)
			{
				result = string.Empty;
				return;
			}
			AskPassWindow askPassWindow = new AskPassWindow(request, repositoryPath);
			askPassWindow.ShowDialog();
			result = askPassWindow.Result ?? string.Empty;
		}

		[Null]
		private string QueryFromWindowsCredentialManager(AskPassRequest askPassRequest)
		{
			if (askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
			{
				string text = WindowsCredentialManager.QuerySshPassphrase(sshPassphrase.KeyPath);
				if (string.IsNullOrEmpty(text))
				{
					return null;
				}
				return text;
			}
			if (askPassRequest is AskPassRequest.SshUserPassword sshUserPassword)
			{
				string text2 = WindowsCredentialManager.QuerySshUserPassword(sshUserPassword.Url, sshUserPassword.Username);
				if (string.IsNullOrEmpty(text2))
				{
					return null;
				}
				return text2;
			}
			return null;
		}
	}
}
