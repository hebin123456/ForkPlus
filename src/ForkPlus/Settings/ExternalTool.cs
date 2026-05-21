using Newtonsoft.Json.Linq;

namespace ForkPlus.Settings
{
	public class ExternalTool
	{
		public static class Coder
		{
			internal static JValue Encode(ToolType target)
			{
				return target switch
				{
					ToolType.Custom => new JValue("Custom"), 
					ToolType.AraxisMerge => new JValue("AraxisMerge"), 
					ToolType.BeyondCompare => new JValue("BeyondCompare"), 
					ToolType.Cursor => new JValue("Cursor"), 
					ToolType.KDiff3 => new JValue("KDiff3"), 
					ToolType.P4Merge => new JValue("P4Merge"), 
					ToolType.VSCode => new JValue("VSCode"), 
					ToolType.VisualStudio => new JValue("VisualStudio"), 
					ToolType.Unity3d => new JValue("Unity3d"), 
					ToolType.WinMerge => new JValue("WinMerge"), 
					_ => null, 
				};
			}

			internal static ToolType? DecodeExternalToolType([Null] JToken jToken)
			{
				string text = jToken?.Value<string>();
				if (text == null)
				{
					return null;
				}
				return text switch
				{
					"Custom" => ToolType.Custom, 
					"AraxisMerge" => ToolType.AraxisMerge, 
					"BeyondCompare" => ToolType.BeyondCompare, 
					"Cursor" => ToolType.Cursor, 
					"KDiff3" => ToolType.KDiff3, 
					"P4Merge" => ToolType.P4Merge, 
					"VSCode" => ToolType.VSCode, 
					"VisualStudio" => ToolType.VisualStudio, 
					"Unity3d" => ToolType.Unity3d, 
					"WinMerge" => ToolType.WinMerge, 
					_ => null, 
				};
			}

			internal static JObject Encode(ExternalTool target)
			{
				JObject jObject = new JObject();
				jObject.Add("Type", Encode(target.Type));
				string name = target.Name;
				if (name != null)
				{
					jObject.Add("Name", new JValue(name));
				}
				string path = target.Path;
				if (path != null)
				{
					jObject.Add("Path", new JValue(path));
				}
				string arguments = target.Arguments;
				if (arguments != null)
				{
					jObject.Add("Arguments", new JValue(arguments));
				}
				bool? isPrimary = target.IsPrimary;
				if (isPrimary.HasValue)
				{
					bool valueOrDefault = isPrimary.GetValueOrDefault();
					jObject.Add("IsPrimary", new JValue(valueOrDefault));
				}
				isPrimary = target.IsVisible;
				if (isPrimary.HasValue)
				{
					bool valueOrDefault2 = isPrimary.GetValueOrDefault();
					jObject.Add("IsVisible", new JValue(valueOrDefault2));
				}
				return jObject;
			}

			[Null]
			internal static ExternalTool Decode([Null] JToken jToken)
			{
				if (!(jToken is JObject jObject))
				{
					return null;
				}
				ToolType? toolType = DecodeExternalToolType(jObject["Type"]);
				if (toolType.HasValue)
				{
					ToolType valueOrDefault = toolType.GetValueOrDefault();
					string name = null;
					if (valueOrDefault == ToolType.Custom)
					{
						name = jObject["Name"]?.Value<string>();
					}
					string path = jObject["Path"]?.Value<string>();
					string arguments = jObject["Arguments"]?.Value<string>();
					bool? isPrimary = jObject["IsPrimary"]?.Value<bool>();
					bool? isVisible = jObject["IsVisible"]?.Value<bool>();
					return new ExternalTool(valueOrDefault, name, path, arguments, isPrimary, isVisible);
				}
				return null;
			}
		}

		public ToolType Type { get; }

		[Null]
		public string Name { get; }

		[Null]
		public string Path { get; }

		[Null]
		public string Arguments { get; }

		public bool? IsPrimary { get; }

		public bool? IsVisible { get; }

		public ExternalTool(ToolType type, [Null] string name, [Null] string path, [Null] string arguments, bool? isPrimary, bool? isVisible)
		{
			Type = type;
			Name = name;
			Path = path;
			Arguments = arguments;
			IsPrimary = isPrimary;
			IsVisible = isVisible;
		}
	}
}
