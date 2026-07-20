using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts.AiServices
{
	public class OpenAiResponse
	{
		public string Message { get; }

		public OpenAiResponse(string message)
		{
			Message = message;
		}

		[Null]
		public static OpenAiResponse Decode(JObject json)
		{
			string message = DecodeMessageContent(json["choices"]?[0]?["message"]?["content"]);
			if (string.IsNullOrWhiteSpace(message))
			{
				message = json.GetString("choices", 0, "message", "reasoning_content");
			}
			if (string.IsNullOrWhiteSpace(message))
			{
				message = json.GetString("choices", 0, "text");
			}
			if (message == null)
			{
				Log.Warn("Cannot parse OpenAiResponse");
				return null;
			}
			char[] trimChars = "```".ToCharArray();
			return new OpenAiResponse(message.TrimStart(trimChars).TrimEnd(trimChars).Trim());
		}

		[Null]
		private static string DecodeMessageContent([Null] JToken content)
		{
			if (content == null || content.Type == JTokenType.Null)
			{
				return null;
			}
			if (content.Type == JTokenType.String)
			{
				return content.Value<string>();
			}
			if (content is JArray array)
			{
				System.Text.StringBuilder builder = new System.Text.StringBuilder();
				foreach (JToken part in array)
				{
					string text = part["text"]?.Value<string>() ?? part["content"]?.Value<string>();
					if (!string.IsNullOrEmpty(text))
					{
						builder.Append(text);
					}
				}
				return builder.Length == 0 ? null : builder.ToString();
			}
			return content.ToString();
		}
	}
}
