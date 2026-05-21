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
			string @string = json.GetString("choices", 0, "message", "content");
			if (@string == null)
			{
				Log.Warn("Cannot parse OpenAiResponse");
				return null;
			}
			char[] trimChars = "```".ToCharArray();
			return new OpenAiResponse(@string.TrimStart(trimChars).TrimEnd(trimChars).Trim());
		}
	}
}
