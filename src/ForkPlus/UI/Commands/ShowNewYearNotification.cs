using System;
using System.Threading;
using System.Windows.Input;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowNewYearNotification : IUICommand, IForkPlusCommand
	{
		private static readonly int NewYear = 2026;

		public static bool NotificationRequired
		{
			get
			{
				if (ForkPlusSettings.Default.SeenNewYear2026)
				{
					return false;
				}
				DateTime now = DateTime.Now;
				if (now.Year == NewYear && now.DayOfYear < 8)
				{
					return true;
				}
				if (now.Year == NewYear - 1 && now.Month == 12 && now.Day == 31)
				{
					return true;
				}
				return false;
			}
		}

		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl == null)
			{
				return;
			}
			ForkPlusSettings.Default.SeenNewYear2026 = true;
			ForkPlusSettings.Default.Save();
			activeRepositoryUserControl.JobQueue.Add("☆     ☆     ☆", delegate(JobMonitor m)
			{
				string text = new GetGlobalUserIdentityGitCommand().Execute()?.Result?.Name;
				for (int i = 0; i < 6; i++)
				{
					Message(m, 0.25, "★☆☆★☆☆★☆☆★☆☆★☆☆★☆☆");
					Message(m, 0.25, "☆★☆☆★☆☆★☆☆★☆☆★☆☆★☆");
					Message(m, 0.25, "☆☆★☆☆★☆☆★☆☆★☆☆★☆☆★");
				}
				Message(m, 0.5, " ");
				Message(m, 3.0, "★☆★     Hi!     ★☆★");
				Message(m, 3.0, "★☆★ I was asked to deliver a message ★☆★");
				if (DateTime.Now.Year == NewYear)
				{
					Message(m, 3.0, $"☆ {NewYear} has just started! ☆");
				}
				else
				{
					Message(m, 3.0, $"It’s {NewYear} somewhere…");
				}
				Message(m, 1.0, "☆ Time goes by! ☆");
				Message(m, 1.0, "★ Time goes by! ★");
				Message(m, 1.0, "☆ Time goes by! ☆");
				Message(m, 1.0, "☆ I wish you a great year! ☆");
				Message(m, 1.0, "★ I wish you a great year! ★");
				Message(m, 1.0, "☆ I wish you a great year! ☆");
				Message(m, 1.0, "☆ May your code have no bugs! ☆");
				Message(m, 1.0, "★ May your code have no bugs! ★");
				Message(m, 1.0, "☆ May your code have no bugs! ☆");
				Message(m, 1.0, "☆ May branches merge on the first try ☆");
				Message(m, 1.0, "★ May branches merge on the first try ★");
				Message(m, 1.0, "☆ May branches merge on the first try ☆");
				Message(m, 1.0, "☆ May CI be green ☆");
				Message(m, 1.0, "★ May CI be green ★");
				Message(m, 1.0, "☆ May CI be green ☆");
				Message(m, 1.0, "☆May your servers work stable!☆");
				Message(m, 1.0, "★May your servers work stable!★");
				Message(m, 1.0, "☆May your servers work stable!☆");
				int num = 17;
				for (int j = 0; j < num; j++)
				{
					double seconds = 0.25;
					if (j == 0 || j == num - 1)
					{
						seconds = 0.5;
					}
					Message(m, seconds, $"       May {NewYear} bring health, peace, and fortune!".Substring(j));
				}
				Message(m, 0.5, " ");
				if (!string.IsNullOrEmpty(text))
				{
					Message(m, 6.0, "Happy New Year " + text + "!");
				}
				else
				{
					Message(m, 6.0, "\ud83c\udf81 Happy New Year! \ud83c\udf81");
				}
				Message(m, 3.0, "Sincerely, your Fork");
				Message(m, 2.0, "OK, it's time to work...");
				Message(m, 3.0, "Pull. Push. Cry. Repeat.");
				Message(m, 3.0, "And yeah... I forgot the most important one");
				Message(m, 3.0, "Don't tell anybody forks talk to you");
			});
		}

		private static void Message(JobMonitor monitor, double seconds, string message)
		{
			monitor.Update(0.0, message);
			Thread.Sleep((int)(seconds * 1000.0));
		}
	}
}
