using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Settings
{
	public class JsonHelper
	{
		public static DateTime DecodeDateTime(JToken json, DateTime defaultValue)
		{
			if (json == null)
			{
				return defaultValue;
			}
			try
			{
				return json.Value<DateTime>();
			}
			catch (FormatException ex)
			{
				Log.Warn("Failed to decode date", ex);
				return defaultValue;
			}
		}

		[Null]
		public static JArray EncodeArray<T>([Null] T[] items, Func<T, JToken> encodeCallback)
		{
			if (items == null)
			{
				return null;
			}
			JArray jArray = new JArray();
			foreach (T arg in items)
			{
				jArray.Add(encodeCallback(arg));
			}
			return jArray;
		}

		public static T[] DecodeArray<T>([Null] JArray jsonArray, Func<JToken, T> decodeCallback)
		{
			if (jsonArray == null)
			{
				return null;
			}
			List<T> list = new List<T>(jsonArray.Count);
			foreach (JToken item in jsonArray)
			{
				T val = decodeCallback(item);
				if (val == null)
				{
					return null;
				}
				list.Add(val);
			}
			return list.ToArray();
		}

		public static JArray EncodeStringArray(string[] array)
		{
			JArray jArray = new JArray();
			foreach (string value in array)
			{
				jArray.Add(new JValue(value));
			}
			return jArray;
		}

		public static string[] DecodeStringArray(JArray jsonArray)
		{
			if (jsonArray == null)
			{
				return null;
			}
			List<string> list = new List<string>(jsonArray.Count);
			foreach (JToken item in jsonArray)
			{
				string text = item.Value<string>();
				if (text == null)
				{
					return null;
				}
				list.Add(text);
			}
			return list.ToArray();
		}
	}
}
