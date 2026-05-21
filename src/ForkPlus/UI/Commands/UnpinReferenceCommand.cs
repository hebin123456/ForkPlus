using System;
using System.Collections.Generic;
using System.Windows;
using ForkPlus.Git;

namespace ForkPlus.UI.Commands
{
	public class UnpinReferenceCommand
	{
		public void Execute(GitModule gitModule, Reference[] allReferences, Reference reference)
		{
			gitModule.Settings.PinnedReferences = GetValidPinnedReferences(gitModule.Settings.PinnedReferences, allReferences, reference.FullReference);
			gitModule.Settings.Save();
			Application.Current.ActiveRepositoryUserControl().InvalidateAndRefresh(SubDomain.ReferenceSettings);
		}

		private static string[] GetValidPinnedReferences(string[] pinnedReferences, Reference[] allReferences, string exceptingRef)
		{
			List<string> list = new List<string>(pinnedReferences.Length);
			foreach (Reference reference in allReferences)
			{
				if (!(reference.FullReference == exceptingRef) && Array.IndexOf(pinnedReferences, reference.FullReference) != -1)
				{
					list.Add(reference.FullReference);
				}
			}
			return list.ToArray();
		}
	}
}
