using System;
using System.Collections.Generic;

namespace ForkPlus.Git
{
	public static class GitConfigReferencesExtension
	{
		public static ReferenceStorage.UpstreamTrackingReference[] ReadUpstreams(this GitConfig self)
		{
			List<ReferenceStorage.UpstreamTrackingReference> list = new List<ReferenceStorage.UpstreamTrackingReference>();
			GitConfig.Section[] sections = self.Sections;
			for (int i = 0; i < sections.Length; i++)
			{
				GitConfig.Section section = sections[i];
				if (!(section.Name == "branch") || !(section.Subsection != ""))
				{
					continue;
				}
				string text = "refs/heads/" + section.Subsection;
				string text2 = null;
				string text3 = null;
				GitConfig.Variable[] variables = section.Variables;
				for (int j = 0; j < variables.Length; j++)
				{
					GitConfig.Variable variable = variables[j];
					if (variable.Name == "remote")
					{
						text2 = variable.Value;
					}
					else if (variable.Name == "merge")
					{
						text3 = variable.Value;
					}
				}
				if (text2 != null && text3 != null)
				{
					if (text3.IndexOf("refs/heads/") == 0)
					{
						string text4 = text3.Substring("refs/heads/".Length);
						list.Add(new ReferenceStorage.UpstreamTrackingReference(text, "refs/remotes/" + text2 + "/" + text4));
						continue;
					}
					Log.Warn("Invalid upstream binding: " + text + " -> " + text2 + ":" + text3);
				}
			}
			ReferenceStorage.UpstreamTrackingReference[] array = list.ToArray();
			Array.Sort(array, (ReferenceStorage.UpstreamTrackingReference x, ReferenceStorage.UpstreamTrackingReference y) => string.CompareOrdinal(x.LocalBranch, y.LocalBranch));
			return array;
		}
	}
}
