using System;
using System.Text;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class CopyRevisionInfoCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Copy Commit Info";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.C, KeyModifiers.Control | KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(Revision[] revisions)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < revisions.Length; i++)
			{
				Revision revision = revisions[i];
				stringBuilder.Append(revision.Sha.ToAbbreviatedString() + " - " + revision.Message);
				if (i < revisions.Length - 1)
				{
					stringBuilder.Append(Environment.NewLine);
				}
			}
			ServiceLocator.Clipboard.SetText(stringBuilder.ToString());
		}
	}
}
