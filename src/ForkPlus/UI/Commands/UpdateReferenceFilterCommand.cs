using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class UpdateReferenceFilterCommand
	{
		private enum Mode
		{
			Remove,
			Set
		}

		public void ClearFilter(RepositoryUserControl repositoryUserControl)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule != null)
			{
				gitModule.Settings.FilterReferences = new string[0];
				gitModule.Settings.Save();
				repositoryUserControl.InvalidateAndRefresh(SubDomain.ReferenceSettings);
			}
		}

		public void ToggleActiveBranchFilter(RepositoryUserControl repositoryUserControl)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			if (gitModule.Settings.FilterReferences.Length != 0)
			{
				ClearFilter(repositoryUserControl);
				return;
			}
			LocalBranch localBranch = repositoryUserControl.RepositoryData?.References.ActiveBranch;
			if (localBranch != null)
			{
				SetFilterState(repositoryUserControl, localBranch, ReferenceFilterState.Filter);
			}
		}

		public void SetFilterState(RepositoryUserControl repositoryUserControl, Reference reference, ReferenceFilterState filterStatus, bool clearOld = false)
		{
			string text = (reference as LocalBranch)?.UpstreamFullReference;
			string[] patterns = ((text == null) ? new string[1] { reference.FullReference } : new string[2] { reference.FullReference, text });
			SetFilterState(repositoryUserControl, patterns, filterStatus, clearOld);
		}

		public void SetFilterState(RepositoryUserControl repositoryUserControl, string[] patterns, ReferenceFilterState filterStatus, bool clearOld)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			Reference[] array = repositoryUserControl.RepositoryData?.References.Items;
			if (array != null)
			{
				string[] oldPatterns = (clearOld ? new string[0] : gitModule.Settings.FilterReferences);
				string[] oldPatterns2 = (clearOld ? new string[0] : gitModule.Settings.HiddenReferences);
				string[] validFilterReferences;
				string[] validFilterReferences2;
				switch (filterStatus)
				{
				case ReferenceFilterState.None:
					validFilterReferences = GetValidFilterReferences(oldPatterns, array, patterns, Mode.Remove);
					validFilterReferences2 = GetValidFilterReferences(oldPatterns2, array, patterns, Mode.Remove);
					break;
				case ReferenceFilterState.Filter:
					validFilterReferences = GetValidFilterReferences(oldPatterns, array, patterns, Mode.Set);
					validFilterReferences2 = GetValidFilterReferences(oldPatterns2, array, patterns, Mode.Remove);
					break;
				case ReferenceFilterState.Hide:
					validFilterReferences = GetValidFilterReferences(oldPatterns, array, patterns, Mode.Remove);
					validFilterReferences2 = GetValidFilterReferences(oldPatterns2, array, patterns, Mode.Set);
					break;
				default:
					throw new CannotReachHereException();
				}
				gitModule.Settings.FilterReferences = validFilterReferences;
				gitModule.Settings.HiddenReferences = validFilterReferences2;
				gitModule.Settings.Save();
				repositoryUserControl.InvalidateAndRefresh(SubDomain.ReferenceSettings);
			}
		}

		private static string[] GetValidFilterReferences(string[] oldPatterns, Reference[] allReferences, string[] patternsToModify, Mode modification)
		{
			List<string> list = new List<string>(oldPatterns.Length + 1);
			string[] array = oldPatterns;
			foreach (string text in array)
			{
				if (patternsToModify.ContainsItem(text))
				{
					continue;
				}
				foreach (Reference reference in allReferences)
				{
					if (text.EndsWith("/"))
					{
						if (patternsToModify.AnyItem((string pattern) => reference.FullReference.StartsWith(pattern)))
						{
							list.Add(text);
							break;
						}
					}
					else if (reference.FullReference == text)
					{
						list.Add(text);
					}
				}
			}
			if (modification == Mode.Set)
			{
				array = patternsToModify;
				foreach (string item in array)
				{
					list.Add(item);
				}
			}
			return list.ToArray();
		}
	}
}
