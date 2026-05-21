using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Git
{
	public class RevisionSearchQuery
	{
		public static class Coder
		{
			public static RevisionSearchQuery[] Decode([Null] JArray jArray)
			{
				if (jArray == null)
				{
					return new RevisionSearchQuery[0];
				}
				List<RevisionSearchQuery> list = new List<RevisionSearchQuery>(jArray.Count);
				foreach (JToken item in jArray)
				{
					RevisionSearchQuery revisionSearchQuery = Decode(item as JObject);
					if (revisionSearchQuery != null)
					{
						list.Add(revisionSearchQuery);
					}
				}
				return list.ToArray();
			}

			public static JArray Encode(RevisionSearchQuery[] searchQuaries)
			{
				JArray jArray = new JArray();
				foreach (RevisionSearchQuery searchQuery in searchQuaries)
				{
					jArray.Add(Encode(searchQuery));
				}
				return jArray;
			}

			private static RevisionSearchQuery Decode(JToken json)
			{
				if (json == null || !json.HasValues)
				{
					return null;
				}
				int type = json["Type"]?.Value<int>() ?? 0;
				RevisionSearchScope scope = (RevisionSearchScope)(json["Scope"]?.Value<int>() ?? 0);
				string searchString = json["SearchString"]?.Value<string>();
				return new RevisionSearchQuery((RevisionSearchType)type, scope, searchString);
			}

			private static JObject Encode(RevisionSearchQuery searchQuery)
			{
				if (searchQuery == null)
				{
					return null;
				}
				return new JObject
				{
					{
						"Type",
						new JValue((long)searchQuery.Type)
					},
					{
						"Scope",
						new JValue((long)searchQuery.Scope)
					},
					{
						"SearchString",
						new JValue(searchQuery.SearchString)
					}
				};
			}
		}

		public RevisionSearchType Type { get; }

		public RevisionSearchScope Scope { get; }

		public string SearchString { get; }

		public RevisionSearchQuery(RevisionSearchType type, RevisionSearchScope scope, string searchString)
		{
			Type = type;
			Scope = scope;
			SearchString = searchString;
		}

		public static bool Equals([Null] RevisionSearchQuery lhs, [Null] RevisionSearchQuery rhs)
		{
			if (lhs == null && rhs == null)
			{
				return true;
			}
			if (lhs == null && rhs != null)
			{
				return false;
			}
			if (lhs != null && rhs == null)
			{
				return false;
			}
			if (lhs.Type == rhs.Type && lhs.Scope == rhs.Scope)
			{
				return lhs.SearchString == rhs.SearchString;
			}
			return false;
		}
	}
}
