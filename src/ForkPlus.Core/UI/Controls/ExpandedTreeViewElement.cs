using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ForkPlus.UI.Controls
{
	public class ExpandedTreeViewElement
	{
		public static class Coder
		{
			private static readonly string TitleKey = "title";

			private static readonly string ChildrenKey = "children";

			private static readonly string ObsoleteTitleKey = "Name";

			private static readonly string ObsoleteChildrenKey = "Children";

			[Null]
			public static ExpandedTreeViewElement[] DecodeExpandedTreeViewElementArray([Null] JArray jsonArray)
			{
				if (jsonArray == null)
				{
					return null;
				}
				List<ExpandedTreeViewElement> list = new List<ExpandedTreeViewElement>(jsonArray.Count);
				foreach (JToken item in jsonArray)
				{
					ExpandedTreeViewElement expandedTreeViewElement = Decode(item as JObject);
					if (expandedTreeViewElement != null)
					{
						list.Add(expandedTreeViewElement);
					}
				}
				return list.ToArray();
			}

			[Null]
			public static ExpandedTreeViewElement Decode([Null] JObject json)
			{
				if (json == null || !json.HasValues)
				{
					return null;
				}
				string text = json[TitleKey]?.Value<string>() ?? json[ObsoleteTitleKey]?.Value<string>();
				ExpandedTreeViewElement[] array = DecodeExpandedTreeViewElementArray(json[ChildrenKey] as JArray) ?? DecodeExpandedTreeViewElementArray(json[ObsoleteChildrenKey] as JArray);
				if (text == null || array == null)
				{
					return null;
				}
				return new ExpandedTreeViewElement(text, array);
			}

			[Null]
			public static JArray EncodeExpandedTreeViewElementArray([Null] ExpandedTreeViewElement[] entries)
			{
				if (entries == null)
				{
					return null;
				}
				JArray jArray = new JArray();
				for (int i = 0; i < entries.Length; i++)
				{
					JObject jObject = Encode(entries[i]);
					if (jObject != null)
					{
						jArray.Add(jObject);
					}
				}
				return jArray;
			}

			[Null]
			public static JObject Encode([Null] ExpandedTreeViewElement element)
			{
				if (element == null)
				{
					return null;
				}
				JObject obj = new JObject { 
				{
					TitleKey,
					new JValue(element.Name)
				} };
				obj.Add(value: EncodeExpandedTreeViewElementArray(element.Children), propertyName: ChildrenKey);
				return obj;
			}
		}

		public string Name { get; }

		public ExpandedTreeViewElement[] Children { get; }

		public ExpandedTreeViewElement(string name, ExpandedTreeViewElement[] children)
		{
			Name = name;
			Children = children;
		}
	}
}
