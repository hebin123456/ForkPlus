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
