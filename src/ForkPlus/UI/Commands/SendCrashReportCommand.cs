using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class SendCrashReportCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Send Crash Report";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			GitCommandResult<string[]> recentCrashReports = GetRecentCrashReports();
			if (!recentCrashReports.Succeeded)
			{
				new ErrorWindow(null, recentCrashReports.Error).ShowDialog();
				return;
			}
			string[] result = recentCrashReports.Result;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append($"{result.Length} crash reports:\n\n");
			for (int i = 0; i < result.Length; i++)
			{
				stringBuilder.Append($"{i + 1}.\n\n```\n");
				stringBuilder.Append(result[i].TrimEnd());
				stringBuilder.Append("\n```\n\n");
			}
			new MessageBoxWindow("Crash Reports", $"Collected {result.Length} crash report(s). Online submission has been removed from this reconstruction build.", "OK", showCancelButton: false, width: 640.0).ShowDialog();
		}

		private GitCommandResult<string[]> GetRecentCrashReports()
		{
			DateTime dateTime = DateTime.Today.AddDays(-5.0);
			string text = $"{dateTime.Year}-{dateTime.Month}-{dateTime.Day}T00:00:00";
			string[] args = new string[3]
			{
				"qe",
				"Application",
				"/q:*[System[TimeCreated[@SystemTime>='" + text + "'] and (Level=2) and (EventID=1026)]]"
			};
			GitRequestResult gitRequestResult = default(GitRequest).Path("wevtutil").Command(args).ExecuteBt();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			List<string> list = new List<string>();
			string text2 = "<Event xmlns=";
			string text3 = "</Event>";
			int startIndex = 0;
			while (true)
			{
				int num = gitRequestResult.Stdout.IndexOf(text2, startIndex);
				if (num == -1)
				{
					break;
				}
				int num2 = gitRequestResult.Stdout.IndexOf(text3, num + text2.Length);
				if (num2 == -1)
				{
					break;
				}
				string message = gitRequestResult.Stdout.Substring(new Range(num, num2 + text3.Length));
				startIndex = num2 + text3.Length;
				string text4 = ParseDataTag(message);
				if (text4 != null && text4.StartsWith("Application: Fork"))
				{
					list.Add(text4);
				}
			}
			return GitCommandResult<string[]>.Success(list.ToArray());
		}

		[Null]
		private string ParseDataTag(string message)
		{
			string text = "<Data>";
			string value = "</Data>";
			int num = message.IndexOf(text);
			if (num == -1)
			{
				return null;
			}
			int num2 = message.IndexOf(value);
			if (num2 == -1)
			{
				return null;
			}
			return message.Substring(new Range(num + text.Length, num2));
		}
	}
}
