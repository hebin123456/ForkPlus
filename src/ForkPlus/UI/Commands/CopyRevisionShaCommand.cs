using System;
using System.Text;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class CopyRevisionShaCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Copy Commit SHA";

		public KeyGesture Shortcut => new KeyGesture(Key.C, KeyModifiers.Control);

		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] Revision[] revisions)
		{
			if (revisions == null)
			{
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < revisions.Length; i++)
			{
				Revision revision = revisions[i];
				stringBuilder.Append(revision.Sha.ToString());
				if (i < revisions.Length - 1)
				{
					stringBuilder.Append(Environment.NewLine);
				}
			}
			ServiceLocator.Clipboard.SetText(stringBuilder.ToString());
		}
	}
}
