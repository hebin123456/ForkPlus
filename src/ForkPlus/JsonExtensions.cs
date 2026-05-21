using Newtonsoft.Json.Linq;

namespace ForkPlus
{
	internal static class JsonExtensions
	{
		[Null]
		public static int? GetInt(this JObject json, string name)
		{
			int? num = json[name]?.Value<int>();
			if (num.HasValue)
			{
				return num.GetValueOrDefault();
			}
			Log.Warn("Cannot parse '" + name + "'");
			return null;
		}

		[Null]
		public static long? GetLong(this JObject json, string name)
		{
			long? num = json[name]?.Value<long>();
			if (num.HasValue)
			{
				return num.GetValueOrDefault();
			}
			Log.Warn("Cannot parse '" + name + "'");
			return null;
		}

		[Null]
		public static string GetString(this JObject json, string name)
		{
			string text = json[name]?.Value<string>();
			if (text != null)
			{
				return text;
			}
			Log.Warn("Cannot parse '" + name + "'");
			return null;
		}

		[Null]
		public static string GetString(this JObject json, string parent, string name)
		{
			if (json[parent] is JObject jObject)
			{
				string text = jObject[name]?.Value<string>();
				if (text != null)
				{
					return text;
				}
			}
			Log.Warn("Cannot parse '" + parent + "." + name + "'");
			return null;
		}

		[Null]
		public static string GetString(this JObject json, string parent1, string parent, string name)
		{
			if (!(json[parent1] is JObject json2))
			{
				Log.Warn("Cannot parse '" + parent1 + "." + parent + "." + name + "'");
				return null;
			}
			return json2.GetString(parent, name);
		}

		[Null]
		public static string GetString(this JObject json, string parent1, int parent, string name)
		{
			if (json[parent1] is JArray jArray)
			{
				JObject jObject = jArray.GetJObject(parent);
				if (jObject != null)
				{
					string text = jObject[name]?.Value<string>();
					if (text != null)
					{
						return text;
					}
				}
			}
			Log.Warn($"Cannot parse '{parent1}.{parent}.{name}'");
			return null;
		}

		[Null]
		public static string GetString(this JObject json, string parent2, string parent1, int parent, string name)
		{
			if (json[parent2] is JObject jObject && jObject[parent1] is JArray jArray && jArray[parent] is JObject jObject2)
			{
				string text = jObject2[name]?.Value<string>();
				if (text != null)
				{
					return text;
				}
			}
			Log.Warn($"Cannot parse '{parent2}.{parent1}[{parent}].{name}'");
			return null;
		}

		[Null]
		public static string GetString(this JObject json, string parent2, int parent1, string parent, string name)
		{
			if (json[parent2] is JArray jArray && jArray[parent1] is JObject jObject && jObject[parent] is JObject jObject2)
			{
				string text = jObject2[name]?.Value<string>();
				if (text != null)
				{
					return text;
				}
			}
			Log.Warn($"Cannot parse '{parent2}[{parent1}].{parent}.{name}'");
			return null;
		}

		[Null]
		public static string GetString(this JObject json, string parent2, string parent1, string parent, string name)
		{
			if (!(json[parent2] is JObject json2))
			{
				Log.Warn("Cannot parse '" + parent2 + "." + parent1 + "." + parent + "." + name + "'");
				return null;
			}
			return json2.GetString(parent1, parent, name);
		}

		[Null]
		public static JObject GetJObject(this JArray jArray, int index)
		{
			if (jArray.Count <= index)
			{
				Log.Warn($"Cannot read json array element at {index}");
				return null;
			}
			if (!(jArray[index] is JObject result))
			{
				Log.Warn($"Cannot parse '[{index}]'");
				return null;
			}
			return result;
		}
	}
}
