using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts
{
	public class ToastNotification
	{
		public class Coder
		{
			public static string EncodeString(ToastNotification notificaion)
			{
				return Encode(notificaion).ToString();
			}

			private static JToken Encode(ToastNotification notificaion)
			{
				return new JObject
				{
					{ "id", notificaion.ThreadId },
					{ "url", notificaion.Url }
				};
			}

			[UnconditionalSuppressMessage("AotAnalysis", "IL3050",
				Justification = "Newtonsoft.Json 13.0+ 对 POCO 类型 AOT 友好，反序列化类型编译期已知。")]
			[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
				Justification = "Newtonsoft.Json 13.0+ 对 POCO 类型 AOT 友好，反序列化类型编译期已知。")]
			public static ToastNotification DecodeString(string jsonString)
			{
				if (JsonConvert.DeserializeObject(jsonString) is JObject json)
				{
					string @string = json.GetString("id");
					if (@string != null)
					{
						string string2 = json.GetString("url");
						if (string2 != null)
						{
							return new ToastNotification(@string, string2);
						}
					}
				}
				Log.Error("Cannot parse ToastNotification json");
				return null;
			}
		}

		public string ThreadId { get; }

		public string Url { get; }

		public ToastNotification(string threadId, string url)
		{
			ThreadId = threadId;
			Url = url;
		}
	}
}
