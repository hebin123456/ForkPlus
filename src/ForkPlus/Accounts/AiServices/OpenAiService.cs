using System.Net.Http;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts.AiServices
{
	internal class OpenAiService : RestClientBase
	{
		public OpenAiService(Connection connection)
			: base(connection)
		{
		}

		public ServiceResult<OpenAiResponse> Test()
		{
			return OpenAiRequest("This is a test request. Please answer in one word whether it was successful.");
		}

		public ServiceResult<OpenAiResponse> GenerateCommitMessage(string patchString, JobMonitor monitor)
		{
			monitor.Update(0.0, "Generating with OpenAI...");
			int pageGuideLinePosition = ForkPlusSettings.Default.PageGuideLinePosition;
			int commitSubjectLowLimit = ForkPlusSettings.Default.CommitSubjectLowLimit;
			int commitSubjectHighLimit = ForkPlusSettings.Default.CommitSubjectHighLimit;
			string text = $"\nWrite a commit message for my changes.\nExplain what were the changes and why the changes were done.\nFocus the most important changes.\nUse the present tense.\nUse a single word lowercase commit prefix.\nHard wrap lines at {pageGuideLinePosition} characters.\nEnsure the title is less than {commitSubjectLowLimit} (soft limit) and {commitSubjectHighLimit} (hard limit).\nDo not start any lines with the hash symbol.\nOnly respond with the commit message.\n\nBelow is my git diff:\n\n```\n{patchString}\n```\n";
			monitor.AppendOutputLine("Message:\n");
			monitor.AppendOutputLine(text);
			monitor.AppendOutputLine("\nResponse:\n");
			ServiceResult<OpenAiResponse> serviceResult = OpenAiRequest(text);
			if (!serviceResult.Succeeded)
			{
				monitor.Fail(serviceResult.Error.FriendlyMessage);
				monitor.AppendOutputLine(serviceResult.Error.FriendlyMessage);
				return ServiceResult<OpenAiResponse>.Failure(serviceResult.Error);
			}
			string message = serviceResult.Result.Message;
			monitor.AppendOutputLine(message);
			CommitMessageHelper.SplitCommitBody(message, out var subject, out var _);
			monitor.Success(subject.Quotify());
			return ServiceResult<OpenAiResponse>.Success(serviceResult.Result);
		}

		public ServiceResult<OpenAiResponse> CodeReview(string patchString, JobMonitor monitor)
		{
			monitor.Update(0.0, "Reviewing with OpenAI...");
			string text = "\nMake code review for changes.\nDo not thank.\n\nBelow is the git diff:\n\n```\n" + patchString + "\n```\n";
			monitor.AppendOutputLine("Message:\n");
			monitor.AppendOutputLine(text);
			monitor.AppendOutputLine("\nResponse:\n");
			ServiceResult<OpenAiResponse> serviceResult = OpenAiRequest(text);
			if (!serviceResult.Succeeded)
			{
				monitor.Fail(serviceResult.Error.FriendlyMessage);
				monitor.AppendOutputLine(serviceResult.Error.FriendlyMessage);
				return ServiceResult<OpenAiResponse>.Failure(serviceResult.Error);
			}
			monitor.AppendOutputLine(serviceResult.Result.Message);
			monitor.Success("reviewed");
			return ServiceResult<OpenAiResponse>.Success(serviceResult.Result);
		}

		public ServiceResult<OpenAiResponse> OpenAiRequest(string message)
		{
			ApiRequest apiRequest = new ApiRequest(HttpMethod.Post, "/v1/chat/completions");
			JObject jObject = new JObject();
			jObject.Add("role", "system");
			jObject.Add("content", "You are a helpful assistant.");
			JObject jObject2 = new JObject();
			jObject2.Add("role", "user");
			jObject2.Add("content", message);
			JObject jObject3 = new JObject();
			jObject3.Add("model", "gpt-4o");
			jObject3.Add("messages", new JArray(jObject, jObject2));
			apiRequest.SetJson(jObject3);
			return Request(apiRequest, OpenAiResponse.Decode);
		}

		protected override ServiceResult<T> DecodeJsonError<T>(ServiceError.RemoteServiceJsonError jsonError)
		{
			string text = DecodeServiceError(jsonError.Json as JObject);
			if (text != null)
			{
				return ServiceResult<T>.Failure(new ServiceError.RemoteServiceError(text));
			}
			return base.DecodeJsonError<T>(jsonError);
		}

		[Null]
		public static string DecodeServiceError([Null] JObject json)
		{
			if (json != null)
			{
				string @string = json.GetString("error", "message");
				if (@string != null)
				{
					Log.Warn(@string);
					return @string;
				}
			}
			Log.Warn("Cannot parse Error json");
			return null;
		}
	}
}
