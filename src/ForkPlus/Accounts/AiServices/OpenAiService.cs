using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts.AiServices
{
	internal class OpenAiService : RestClientBase
	{
		private readonly string _model;

		public OpenAiService(Connection connection)
			: this(connection, null)
		{
		}

		public OpenAiService(Connection connection, string model)
			: base(connection)
		{
			_model = string.IsNullOrWhiteSpace(model) ? "gpt-4o" : model;
		}

		public static bool IsAiReviewConfigured()
		{
			return !string.IsNullOrWhiteSpace(ForkPlusSettings.Default.AiReviewServiceUrl)
				&& !string.IsNullOrWhiteSpace(ForkPlusSettings.Default.AiReviewApiKey)
				&& !string.IsNullOrWhiteSpace(ForkPlusSettings.Default.AiReviewSelectedModel);
		}

		public static OpenAiService CreateFromAiReviewSettings()
		{
			string serviceUrl = NormalizeServiceUrl(ForkPlusSettings.Default.AiReviewServiceUrl);
			PrivateAccessTokenAuthentication authentication = new PrivateAccessTokenAuthentication(serviceUrl, "ai-review", ForkPlusSettings.Default.AiReviewApiKey);
			return new OpenAiService(new Connection(serviceUrl, authentication, ForkPlusSettings.Default.AiReviewTimeoutSeconds), ForkPlusSettings.Default.AiReviewSelectedModel);
		}

		public ServiceResult<OpenAiResponse> Test()
		{
			return OpenAiRequest("This is a test request. Please answer in one word whether it was successful.");
		}

		public ServiceResult<OpenAiResponse> GenerateCommitMessage(string patchString, GitModule gitModule, JobMonitor monitor, Action<string> onChunk = null)
		{
			monitor.Update(0.0, PreferencesLocalization.FormatCurrent("Generating with {0}...", _model));
			int pageGuideLinePosition = ForkPlusSettings.Default.PageGuideLinePosition;
			int commitSubjectLowLimit = ForkPlusSettings.Default.CommitSubjectLowLimit;
			int commitSubjectHighLimit = ForkPlusSettings.Default.CommitSubjectHighLimit;
			string responseLanguage = CommitMessageResponseLanguage();
			string commitMessageRegex = gitModule?.Settings?.CommitMessageRegex;
			if (string.IsNullOrWhiteSpace(commitMessageRegex))
			{
				commitMessageRegex = ForkPlusSettings.Default.CommitMessageRegex;
			}
			string regexInstruction = string.IsNullOrWhiteSpace(commitMessageRegex) ? "" : $"\nThe commit message must match this Go regular expression when represented as `title\\ndescription` using one LF between the title and description:\n`{commitMessageRegex}`\nIf there is no description, match the title only. If the regex implies a required prefix, issue id, type, scope, or format, follow it strictly.\n";
			string text = $"\nWrite a commit message for my changes.\nThe commit message must be written in {responseLanguage}.\nExplain what were the changes and why the changes were done.\nFocus the most important changes.\nUse the present tense.\nUse a single word lowercase commit prefix only if it is natural for {responseLanguage} and allowed by the configured format.\nHard wrap lines at {pageGuideLinePosition} characters.\nEnsure the title is less than {commitSubjectLowLimit} (soft limit) and {commitSubjectHighLimit} (hard limit).\nDo not start any lines with the hash symbol.{regexInstruction}\nOnly respond with the commit message.\n\nBelow is my git diff:\n\n```\n{patchString}\n```\n";
			monitor.AppendOutputLine(PreferencesLocalization.Current("Message:\n"));
			monitor.AppendOutputLine(text);
			monitor.AppendOutputLine(PreferencesLocalization.Current("\nResponse:\n"));
			// onChunk 回调：流式 chunk 实时通知调用方（用于即时写入 commit 框）
			ServiceResult<OpenAiResponse> serviceResult = OpenAiRequestStreamingWithRetry(text, monitor, onChunk);
			if (!serviceResult.Succeeded)
			{
				monitor.Fail(serviceResult.Error.FriendlyMessage);
				monitor.AppendOutputLine(serviceResult.Error.FriendlyMessage);
				return ServiceResult<OpenAiResponse>.Failure(serviceResult.Error);
			}
			string message = serviceResult.Result.Message;
			// 流式输出已将内容逐 chunk 追加到 monitor，此处无需再 AppendOutputLine(message)
			if (!MatchesCommitMessageRegex(message, commitMessageRegex, out string regexError) && !string.IsNullOrWhiteSpace(commitMessageRegex))
			{
				monitor.AppendOutputLine(regexError);
				monitor.AppendOutputLine(PreferencesLocalization.Current("Regenerating commit message with configured regex..."));
				ServiceResult<OpenAiResponse> retryResult = OpenAiRequestStreamingWithRetry(CreateRegexRetryPrompt(message, commitMessageRegex, responseLanguage), monitor);
				if (retryResult.Succeeded)
				{
					message = retryResult.Result.Message;
				}
				if (!MatchesCommitMessageRegex(message, commitMessageRegex, out regexError))
				{
					monitor.AppendOutputLine(regexError);
				}
			}
			CommitMessageHelper.SplitCommitBody(message, out var subject, out var _);
			monitor.Success(subject.Quotify());
			return ServiceResult<OpenAiResponse>.Success(new OpenAiResponse(message));
		}

		private static string CreateRegexRetryPrompt(string previousMessage, string commitMessageRegex, string responseLanguage)
		{
			return $"\nRewrite the following commit message in {responseLanguage} so that it matches this Go regular expression when represented as `title\\ndescription` using one LF between the title and description:\n`{commitMessageRegex}`\nIf there is no description, match the title only. Only respond with the corrected commit message. Do not add explanations.\n\nPrevious commit message:\n```\n{previousMessage}\n```\n";
		}

		public static bool MatchesCommitMessageRegex(string message, string pattern, out string error)
		{
			error = null;
			if (string.IsNullOrWhiteSpace(pattern))
			{
				return true;
			}
			try
			{
				if (Regex.IsMatch(NormalizeCommitMessageForRegex(message), pattern, RegexOptions.Singleline))
				{
					return true;
				}
				error = PreferencesLocalization.FormatCurrent("Commit message does not match configured regex: {0}", pattern);
				return false;
			}
			catch (Exception ex)
			{
				Log.Warn("Cannot validate Go commit message regex locally with .NET regex engine: " + ex.Message);
				return true;
			}
		}

		private static string NormalizeCommitMessageForRegex(string message)
		{
			string normalized = (message ?? "").Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
			int lineBreakIndex = normalized.IndexOf('\n');
			if (lineBreakIndex < 0)
			{
				return normalized;
			}
			string title = normalized.Substring(0, lineBreakIndex).TrimEnd();
			string description = normalized.Substring(lineBreakIndex + 1).TrimStart('\n');
			if (string.IsNullOrEmpty(description))
			{
				return title;
			}
			return title + "\n" + description;
		}

		private static string CommitMessageResponseLanguage()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			if (string.Equals(language, "zh-Hans", StringComparison.OrdinalIgnoreCase))
			{
				return "Simplified Chinese";
			}
			if (string.Equals(language, "zh-Hant", StringComparison.OrdinalIgnoreCase))
			{
				return "Traditional Chinese";
			}
			if (string.Equals(language, "ja-JP", StringComparison.OrdinalIgnoreCase))
			{
				return "Japanese";
			}
			if (string.Equals(language, "ko-KR", StringComparison.OrdinalIgnoreCase))
			{
				return "Korean";
			}
			if (string.Equals(language, "fr-FR", StringComparison.OrdinalIgnoreCase))
			{
				return "French";
			}
			if (string.Equals(language, "de-DE", StringComparison.OrdinalIgnoreCase))
			{
				return "German";
			}
			if (string.Equals(language, "es-ES", StringComparison.OrdinalIgnoreCase))
			{
				return "Spanish";
			}
			return "English";
		}

		public ServiceResult<OpenAiResponse> CodeReview(string patchString, JobMonitor monitor, Action<string> onChunk = null)
		{
			monitor.Update(0.0, PreferencesLocalization.FormatCurrent("Reviewing with {0}...", _model));
			string text = "\nMake code review for changes.\nDo not thank.\n\nBelow is the git diff:\n\n```\n" + patchString + "\n```\n";
			monitor.AppendOutputLine(PreferencesLocalization.Current("Message:\n"));
			monitor.AppendOutputLine(text);
			monitor.AppendOutputLine(PreferencesLocalization.Current("\nResponse:\n"));
			ServiceResult<OpenAiResponse> serviceResult = OpenAiRequestStreamingWithRetry(text, monitor, onChunk);
			if (!serviceResult.Succeeded)
			{
				monitor.Fail(serviceResult.Error.FriendlyMessage);
				monitor.AppendOutputLine(serviceResult.Error.FriendlyMessage);
				return ServiceResult<OpenAiResponse>.Failure(serviceResult.Error);
			}
			// 流式输出已将内容逐 chunk 追加到 monitor，此处无需再 AppendOutputLine
			monitor.Success(PreferencesLocalization.Current("reviewed"));
			return ServiceResult<OpenAiResponse>.Success(serviceResult.Result);
		}

		public ServiceResult<OpenAiResponse> CodeReviewFiles(string reviewContext, JobMonitor monitor, Action<string> onChunk = null)
		{
			monitor.Update(0.0, PreferencesLocalization.FormatCurrent("Reviewing files with {0}...", _model));
			string text = "\nReview the following file changes.\nUse concise Chinese by default unless the code/comment language clearly suggests another language.\nReturn actionable findings only.\nGroup findings by file. Start each file section with exactly `## File: relative/path`, using the same relative path from the review context.\nFor every issue, include file path and line number in the format `path:line` when possible.\nIf a finding has a safe concrete fix, also include it in one fenced JSON block named `forkplus-ai-suggestions` with this shape:\n\n```forkplus-ai-suggestions\n[\n  {\n    \"file\": \"relative/path\",\n    \"line\": 12,\n    \"comment\": \"why this should change\",\n    \"oldText\": \"exact text to replace\",\n    \"newText\": \"replacement text\"\n  }\n]\n```\n\nOnly include suggestions when `oldText` is an exact contiguous snippet from the full file content. Do not invent fixes for uncertain findings.\nIf there are no issues for a file, omit that file section. If there are no issues in all files, say clearly that no obvious issues were found.\n\nThe review context includes both full file content and diff.\n\n" + reviewContext;
			monitor.AppendOutputLine(PreferencesLocalization.Current("Message:\n"));
			monitor.AppendOutputLine(text);
			monitor.AppendOutputLine(PreferencesLocalization.Current("\nResponse:\n"));
			ServiceResult<OpenAiResponse> serviceResult = OpenAiRequestStreamingWithRetry(text, monitor, onChunk);
			if (!serviceResult.Succeeded)
			{
				monitor.Fail(serviceResult.Error.FriendlyMessage);
				monitor.AppendOutputLine(serviceResult.Error.FriendlyMessage);
				return ServiceResult<OpenAiResponse>.Failure(serviceResult.Error);
			}
			// 流式输出已将内容逐 chunk 追加到 monitor，此处无需再 AppendOutputLine
			monitor.Success(PreferencesLocalization.Current("reviewed"));
			return ServiceResult<OpenAiResponse>.Success(serviceResult.Result);
		}

		public ServiceResult<string[]> ListModels()
		{
			return RequestWithRetry(new ApiRequest(HttpMethod.Get, "/v1/models"), DecodeModels, null);
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
			jObject3.Add("model", _model);
			jObject3.Add("messages", new JArray(jObject, jObject2));
			jObject3.Add("stream", false);
			apiRequest.SetJson(jObject3);
			return LocalizeCancellationError(Request(apiRequest, OpenAiResponse.Decode));
		}

		private ServiceResult<OpenAiResponse> OpenAiRequestWithRetry(string message, JobMonitor monitor)
		{
			return RequestWithRetry(CreateChatRequest(message), OpenAiResponse.Decode, monitor);
		}

		private ApiRequest CreateChatRequest(string message)
		{
			ApiRequest apiRequest = new ApiRequest(HttpMethod.Post, "/v1/chat/completions");
			JObject jObject = new JObject();
			jObject.Add("role", "system");
			jObject.Add("content", "You are a helpful assistant.");
			JObject jObject2 = new JObject();
			jObject2.Add("role", "user");
			jObject2.Add("content", message);
			JObject jObject3 = new JObject();
			jObject3.Add("model", _model);
			jObject3.Add("messages", new JArray(jObject, jObject2));
			jObject3.Add("stream", false);
			apiRequest.SetJson(jObject3);
			return apiRequest;
		}

		// 流式版本：开启 SSE 流式输出，AI 生成的文本逐 chunk 到达，立即追加到 monitor 输出。
		// 解决"卡一段时间然后没输出"的问题——用户能看到内容逐步出现，且连接因持续收到数据不会超时。
		public ServiceResult<OpenAiResponse> OpenAiRequestStreamingWithRetry(string message, JobMonitor monitor, Action<string> onChunk = null)
		{
			int retryCount = Math.Max(0, ForkPlusSettings.Default.AiReviewRetryCount);
			int normalRetryAttempt = 0;
			int queuedWaitSeconds = 0;
			int maxQueuedWaitSeconds = MaxQueuedWaitSeconds();
			ServiceResult<OpenAiResponse> result = null;
			while (true)
			{
				if (monitor?.IsCanceled == true)
				{
					return ServiceResult<OpenAiResponse>.Failure(new ServiceError.Cancelled());
				}
				result = OpenAiRequestStreaming(message, monitor, onChunk);
				if (monitor?.IsCanceled == true)
				{
					return ServiceResult<OpenAiResponse>.Failure(new ServiceError.Cancelled());
				}
				if (result.Succeeded || !ShouldRetry(result.Error))
				{
					return LocalizeCancellationError(result);
				}
				int queuedDelaySeconds;
				bool isQueuedWait = IsQueuedWaitError(result.Error, out queuedDelaySeconds);
				if (!isQueuedWait)
				{
					string msg = result.Error?.FriendlyMessage ?? "";
					if (IsTransientServiceMessage(msg))
					{
						isQueuedWait = true;
						queuedDelaySeconds = 30;
					}
				}
				if (isQueuedWait)
				{
					int remainingQueuedWaitSeconds = Math.Max(0, maxQueuedWaitSeconds - queuedWaitSeconds);
					if (remainingQueuedWaitSeconds <= 0)
					{
						break;
					}
					int waitSeconds = Math.Min(queuedDelaySeconds, remainingQueuedWaitSeconds);
					queuedWaitSeconds += waitSeconds;
					monitor?.Update(0.0, PreferencesLocalization.FormatCurrent("Queued. Waiting {0} before checking again...", FormatRetryDelay(waitSeconds)));
				monitor?.AppendOutputLine(PreferencesLocalization.FormatCurrent("AI request is queued. Waiting {0} before checking again...", FormatRetryDelay(waitSeconds)));
					if (!WaitBeforeRetry(waitSeconds, monitor))
					{
						return ServiceResult<OpenAiResponse>.Failure(new ServiceError.Cancelled());
					}
					continue;
				}
				if (normalRetryAttempt >= retryCount)
				{
					break;
				}
				normalRetryAttempt++;
				int delaySeconds = RetryDelaySeconds(normalRetryAttempt);
				monitor?.Update(0.0, PreferencesLocalization.FormatCurrent("Retrying in {0}s ({1}/{2})...", delaySeconds, normalRetryAttempt, retryCount));
				monitor?.AppendOutputLine(PreferencesLocalization.FormatCurrent("AI service is busy or queued. Retrying in {0}s ({1}/{2})...", delaySeconds, normalRetryAttempt, retryCount));
				if (!WaitBeforeRetry(delaySeconds, monitor))
				{
					return ServiceResult<OpenAiResponse>.Failure(new ServiceError.Cancelled());
				}
			}
			return LocalizeCancellationError(result);
		}

		/// <summary>
		/// 多轮对话流式请求（带重试）：携带历史消息上下文，实现 AI 对话记忆。
		/// historyMessages 为之前的 user/assistant 消息（不含 system），systemPrompt 为系统提示，
		/// currentUserMessage 为本次用户输入。
		/// </summary>
		public ServiceResult<OpenAiResponse> OpenAiRequestStreamingWithRetry(IList<JObject> historyMessages, string systemPrompt, string currentUserMessage, JobMonitor monitor, Action<string> onChunk = null)
		{
			int retryCount = Math.Max(0, ForkPlusSettings.Default.AiReviewRetryCount);
			int normalRetryAttempt = 0;
			int queuedWaitSeconds = 0;
			int maxQueuedWaitSeconds = MaxQueuedWaitSeconds();
			ServiceResult<OpenAiResponse> result = null;
			while (true)
			{
				if (monitor?.IsCanceled == true)
				{
					return ServiceResult<OpenAiResponse>.Failure(new ServiceError.Cancelled());
				}
				result = OpenAiRequestStreaming(historyMessages, systemPrompt, currentUserMessage, monitor, onChunk);
				if (monitor?.IsCanceled == true)
				{
					return ServiceResult<OpenAiResponse>.Failure(new ServiceError.Cancelled());
				}
				if (result.Succeeded || !ShouldRetry(result.Error))
				{
					return LocalizeCancellationError(result);
				}
				int queuedDelaySeconds;
				bool isQueuedWait = IsQueuedWaitError(result.Error, out queuedDelaySeconds);
				if (!isQueuedWait)
				{
					string msg = result.Error?.FriendlyMessage ?? "";
					if (IsTransientServiceMessage(msg))
					{
						isQueuedWait = true;
						queuedDelaySeconds = 30;
					}
				}
				if (isQueuedWait)
				{
					int remainingQueuedWaitSeconds = maxQueuedWaitSeconds - queuedWaitSeconds;
					if (remainingQueuedWaitSeconds <= 0)
					{
						break;
					}
					int waitSeconds = Math.Min(queuedDelaySeconds, remainingQueuedWaitSeconds);
					queuedWaitSeconds += waitSeconds;
					monitor?.Update(0.0, PreferencesLocalization.FormatCurrent("Queued. Waiting {0} before checking again...", FormatRetryDelay(waitSeconds)));
				monitor?.AppendOutputLine(PreferencesLocalization.FormatCurrent("AI request is queued. Waiting {0} before checking again...", FormatRetryDelay(waitSeconds)));
					if (!WaitBeforeRetry(waitSeconds, monitor))
					{
						return ServiceResult<OpenAiResponse>.Failure(new ServiceError.Cancelled());
					}
					continue;
				}
				if (normalRetryAttempt >= retryCount)
				{
					break;
				}
				normalRetryAttempt++;
				int delaySeconds = RetryDelaySeconds(normalRetryAttempt);
				monitor?.Update(0.0, PreferencesLocalization.FormatCurrent("Retrying in {0}s ({1}/{2})...", delaySeconds, normalRetryAttempt, retryCount));
				monitor?.AppendOutputLine(PreferencesLocalization.FormatCurrent("AI service is busy or queued. Retrying in {0}s ({1}/{2})...", delaySeconds, normalRetryAttempt, retryCount));
				if (!WaitBeforeRetry(delaySeconds, monitor))
				{
					return ServiceResult<OpenAiResponse>.Failure(new ServiceError.Cancelled());
				}
			}
			return LocalizeCancellationError(result);
		}

		private ServiceResult<OpenAiResponse> OpenAiRequestStreaming(string message, JobMonitor monitor, Action<string> onChunk)
		{
			ApiRequest request = CreateChatStreamRequest(message);
			StringBuilder content = new StringBuilder();
			Connection.HttpRequestResult httpResult = Connection.RequestStream(request, true, monitor, delegate(string line)
			{
				ParseSseLine(line, content, monitor, onChunk);
			});
			if (!httpResult.Succeeded)
			{
				return ServiceResult<OpenAiResponse>.Failure(DecodeStreamError(httpResult.Error));
			}
			char[] trimChars = "```".ToCharArray();
			string trimmed = content.ToString().TrimStart(trimChars).TrimEnd(trimChars).Trim();
			return ServiceResult<OpenAiResponse>.Success(new OpenAiResponse(trimmed));
		}

		/// <summary>多轮对话流式请求：携带历史消息上下文。</summary>
		private ServiceResult<OpenAiResponse> OpenAiRequestStreaming(IList<JObject> historyMessages, string systemPrompt, string currentUserMessage, JobMonitor monitor, Action<string> onChunk)
		{
			ApiRequest request = CreateChatStreamRequest(historyMessages, systemPrompt, currentUserMessage);
			StringBuilder content = new StringBuilder();
			Connection.HttpRequestResult httpResult = Connection.RequestStream(request, true, monitor, delegate(string line)
			{
				ParseSseLine(line, content, monitor, onChunk);
			});
			if (!httpResult.Succeeded)
			{
				return ServiceResult<OpenAiResponse>.Failure(DecodeStreamError(httpResult.Error));
			}
			char[] trimChars = "```".ToCharArray();
			string trimmed = content.ToString().TrimStart(trimChars).TrimEnd(trimChars).Trim();
			return ServiceResult<OpenAiResponse>.Success(new OpenAiResponse(trimmed));
		}

		/// <summary>
		/// 流式路径错误解码：将 RemoteServiceJsonError（含完整错误 JSON，但 FriendlyMessage 为通用文案）
		/// 转换为 RemoteServiceError（FriendlyMessage 为真实错误文本）。
		/// 这样 ShouldRetry / IsQueuedWaitError / IsTransientServiceMessage 能从真实文本中识别排队关键字。
		/// 非流式路径经 RestClientBase.Decode → DecodeJsonError 已做此处理，流式路径此前绕过了该链。
		/// </summary>
		private ServiceError DecodeStreamError(ServiceError error)
		{
			if (error is ServiceError.RemoteServiceJsonError jsonError)
			{
				ServiceResult<OpenAiResponse> decoded = DecodeJsonError<OpenAiResponse>(jsonError);
				if (decoded.Error != null)
				{
					return decoded.Error;
				}
			}
			return error;
		}

		private static void ParseSseLine(string line, StringBuilder content, JobMonitor monitor, Action<string> onChunk)
		{
			// SSE 格式：每行以 "data: " 开头，内容是 JSON chunk；空行是事件分隔；":" 开头是注释/keepalive。
			if (string.IsNullOrEmpty(line) || line.StartsWith(":") || !line.StartsWith("data:"))
			{
				return;
			}
			string data = line.Substring(5).Trim();
			if (data == "[DONE]")
			{
				return;
			}
			try
			{
				JObject chunk = JObject.Parse(data);
				string delta = chunk["choices"]?[0]?["delta"]?["content"]?.Value<string>();
				if (!string.IsNullOrEmpty(delta))
				{
					content.Append(delta);
					monitor?.Append(delta);
					onChunk?.Invoke(delta);
				}
			}
			catch
			{
				// 忽略部分行/keepalive 的 JSON 解析错误
			}
		}

		private ApiRequest CreateChatStreamRequest(string message)
		{
			ApiRequest apiRequest = new ApiRequest(HttpMethod.Post, "/v1/chat/completions");
			JObject jObject = new JObject();
			jObject.Add("role", "system");
			jObject.Add("content", "You are a helpful assistant.");
			JObject jObject2 = new JObject();
			jObject2.Add("role", "user");
			jObject2.Add("content", message);
			JObject jObject3 = new JObject();
			jObject3.Add("model", _model);
			jObject3.Add("messages", new JArray(jObject, jObject2));
			jObject3.Add("stream", true);
			apiRequest.SetJson(jObject3);
			return apiRequest;
		}

		/// <summary>
		/// 构造多轮对话流式请求：system + 历史消息（user/assistant 交替）。
		/// 用于 AI 辅助开发窗口的多轮上下文记忆。
		/// </summary>
		private ApiRequest CreateChatStreamRequest(IList<JObject> historyMessages, string systemPrompt, string currentUserMessage)
		{
			ApiRequest apiRequest = new ApiRequest(HttpMethod.Post, "/v1/chat/completions");
			JArray messages = new JArray();
			JObject systemMsg = new JObject();
			systemMsg.Add("role", "system");
			systemMsg.Add("content", systemPrompt ?? "You are a helpful assistant.");
			messages.Add(systemMsg);
			// 追加历史对话（保持顺序，user/assistant 交替）
			if (historyMessages != null)
			{
				foreach (JObject msg in historyMessages)
				{
					messages.Add(msg);
				}
			}
			// 追加当前用户消息
			JObject userMsg = new JObject();
			userMsg.Add("role", "user");
			userMsg.Add("content", currentUserMessage);
			messages.Add(userMsg);
			JObject body = new JObject();
			body.Add("model", _model);
			body.Add("messages", messages);
			body.Add("stream", true);
			apiRequest.SetJson(body);
			return apiRequest;
		}

		private ServiceResult<T> RequestWithRetry<T>(ApiRequest request, Func<JObject, T> decoder, JobMonitor monitor)
		{
			int retryCount = Math.Max(0, ForkPlusSettings.Default.AiReviewRetryCount);
			int normalRetryAttempt = 0;
			int queuedWaitSeconds = 0;
			int maxQueuedWaitSeconds = MaxQueuedWaitSeconds();
			ServiceResult<T> result = null;
			while (true)
			{
				if (monitor?.IsCanceled == true)
				{
					return ServiceResult<T>.Failure(new ServiceError.Cancelled());
				}
				result = Request(request, decoder, monitor);
				if (monitor?.IsCanceled == true)
				{
					return ServiceResult<T>.Failure(new ServiceError.Cancelled());
				}
				if (result.Succeeded || !ShouldRetry(result.Error))
				{
					return LocalizeCancellationError(result);
				}
				int queuedDelaySeconds;
				bool isQueuedWait = IsQueuedWaitError(result.Error, out queuedDelaySeconds);
				if (!isQueuedWait)
				{
					string message = result.Error?.FriendlyMessage ?? "";
					if (IsTransientServiceMessage(message))
					{
						isQueuedWait = true;
						queuedDelaySeconds = 30;
					}
				}
				if (isQueuedWait)
				{
					int remainingQueuedWaitSeconds = Math.Max(0, maxQueuedWaitSeconds - queuedWaitSeconds);
					if (remainingQueuedWaitSeconds <= 0)
					{
						break;
					}
					int waitSeconds = Math.Min(queuedDelaySeconds, remainingQueuedWaitSeconds);
					queuedWaitSeconds += waitSeconds;
					monitor?.Update(0.0, PreferencesLocalization.FormatCurrent("Queued. Waiting {0} before checking again...", FormatRetryDelay(waitSeconds)));
				monitor?.AppendOutputLine(PreferencesLocalization.FormatCurrent("AI request is queued. Waiting {0} before checking again...", FormatRetryDelay(waitSeconds)));
					if (!WaitBeforeRetry(waitSeconds, monitor))
					{
						return ServiceResult<T>.Failure(new ServiceError.Cancelled());
					}
					continue;
				}
				if (normalRetryAttempt >= retryCount)
				{
					break;
				}
				normalRetryAttempt++;
				int delaySeconds = RetryDelaySeconds(normalRetryAttempt);
				monitor?.Update(0.0, PreferencesLocalization.FormatCurrent("Retrying in {0}s ({1}/{2})...", delaySeconds, normalRetryAttempt, retryCount));
				monitor?.AppendOutputLine(PreferencesLocalization.FormatCurrent("AI service is busy or queued. Retrying in {0}s ({1}/{2})...", delaySeconds, normalRetryAttempt, retryCount));
				if (!WaitBeforeRetry(delaySeconds, monitor))
				{
					return ServiceResult<T>.Failure(new ServiceError.Cancelled());
				}
			}
			return LocalizeCancellationError(result);
		}

		private static bool ShouldRetry(ServiceError error)
		{
			if (error is ServiceError.HttpError httpError)
			{
				return IsTransientHttpStatus(httpError.ErrorCode);
			}
			string message = error?.FriendlyMessage ?? "";
			return message.Contains("408")
				|| message.Contains("409")
				|| message.Contains("425")
				|| message.Contains("429")
				|| message.Contains("500")
				|| message.Contains("502")
				|| message.Contains("503")
				|| message.Contains("504")
				|| message.Contains("524")
				|| message.Contains("529")
				|| message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
				|| IsTransientServiceMessage(message)
				|| IsCancellationMessage(message);
		}

		private static bool IsTransientHttpStatus(HttpStatusCode statusCode)
		{
			int status = (int)statusCode;
			return status == 408
				|| status == 409
				|| status == 425
				|| status == 429
				|| status == 500
				|| status == 502
				|| status == 503
				|| status == 504
				|| status == 524
				|| status == 529;
		}

		private static bool IsTransientServiceMessage(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
			{
				return false;
			}
			return message.IndexOf("queue", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("queued", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("busy", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("overload", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("too many requests", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("try again", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("temporarily unavailable", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("service unavailable", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("model is loading", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.Contains("排队")
				|| message.Contains("隊列")
				|| message.Contains("队列")
				|| message.Contains("等待")
				|| message.Contains("預計")
				|| message.Contains("预计")
				|| message.Contains("繁忙")
				|| message.Contains("稍后")
				|| message.Contains("稍後")
				|| message.Contains("限流")
				|| message.Contains("频率")
				|| message.Contains("頻率");
		}

		private static int RetryDelaySeconds(int retryAttempt)
		{
			int[] delays = new[] { 5, 10, 20, 30 };
			if (retryAttempt <= 0)
			{
				return delays[0];
			}
			return delays[Math.Min(retryAttempt - 1, delays.Length - 1)];
		}

		private static bool IsQueuedWaitError(ServiceError error, out int delaySeconds)
		{
			delaySeconds = 30;
			string message = error?.FriendlyMessage ?? "";
			if (!LooksLikeQueuedWaitMessage(message))
			{
				return false;
			}
			if (TryParseQueuedRetryDelaySeconds(message, out int parsedDelaySeconds))
			{
				delaySeconds = parsedDelaySeconds;
			}
			return true;
		}

		private static bool TryParseQueuedRetryDelaySeconds(string message, out int delaySeconds)
		{
			delaySeconds = 0;
			MatchCollection matches = Regex.Matches(message, @"(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>milliseconds?|millisecond|ms|seconds?|second|secs?|sec|s|minutes?|minute|mins?|min|m|hours?|hour|hrs?|hr|h|毫秒|秒钟|秒|分鐘|分钟|分|小時|小时)");
			foreach (Match match in matches)
			{
				string rawValue = match.Groups["value"].Value.Replace(',', '.');
				if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
				{
					continue;
				}
				string unit = match.Groups["unit"].Value.ToLowerInvariant();
				double seconds = value;
				if (unit == "ms" || unit.Contains("millisecond") || unit.Contains("毫秒"))
				{
					seconds = value / 1000.0;
				}
				else if (unit == "m" || unit.StartsWith("min") || unit.Contains("minute") || unit.Contains("分"))
				{
					seconds = value * 60.0;
				}
				else if (unit == "h" || unit.StartsWith("hr") || unit.Contains("hour") || unit.Contains("小時") || unit.Contains("小时"))
				{
					seconds = value * 3600.0;
				}
				delaySeconds = Math.Max(5, (int)Math.Ceiling(seconds));
				return true;
			}
			return false;
		}

		private static bool LooksLikeQueuedWaitMessage(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
			{
				return false;
			}
			return message.IndexOf("queue", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("queued", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("waiting", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("wait", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("eta", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("estimated", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("pending", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("busy", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("overload", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("too many requests", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("try again", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("temporarily unavailable", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("service unavailable", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("model is loading", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.Contains("排队")
				|| message.Contains("隊列")
				|| message.Contains("队列")
				|| message.Contains("等待")
				|| message.Contains("預計")
				|| message.Contains("预计")
				|| message.Contains("繁忙")
				|| message.Contains("稍后")
				|| message.Contains("稍後")
				|| message.Contains("限流")
				|| message.Contains("频率")
				|| message.Contains("頻率");
		}

		private static int MaxQueuedWaitSeconds()
		{
			// 排队最大等待时间独立于请求超时：排队可能持续较久（高峰期），
			// 用 TimeoutSeconds（默认300s）会过早放弃。这里取 TimeoutSeconds 与 1800s(30分钟) 的较大值，
			// 确保排队场景有足够时间等待，而不是快速失败返回错误。
			return Math.Max(ForkPlusSettings.Default.AiReviewTimeoutSeconds, 1800);
		}

		private static string FormatRetryDelay(int seconds)
		{
			if (seconds >= 60)
			{
				int minutes = seconds / 60;
				int restSeconds = seconds % 60;
				if (restSeconds == 0)
				{
					return PreferencesLocalization.FormatCurrent("{0} min", minutes);
				}
				return PreferencesLocalization.FormatCurrent("{0} min {1}s", minutes, restSeconds);
			}
			return PreferencesLocalization.FormatCurrent("{0}s", seconds);
		}

		private static bool WaitBeforeRetry(int delaySeconds, JobMonitor monitor)
		{
			int remainingMilliseconds = Math.Max(0, delaySeconds * 1000);
			while (remainingMilliseconds > 0)
			{
				if (monitor?.IsCanceled == true)
				{
					return false;
				}
				int sleepMilliseconds = Math.Min(250, remainingMilliseconds);
				Thread.Sleep(sleepMilliseconds);
				remainingMilliseconds -= sleepMilliseconds;
			}
			return monitor?.IsCanceled != true;
		}

		private static ServiceResult<T> LocalizeCancellationError<T>(ServiceResult<T> result)
		{
		 if (result != null && !result.Succeeded)
		 {
		  string message = result.Error?.FriendlyMessage ?? "";
		  if (IsCancellationMessage(message))
		  {
		   return ServiceResult<T>.Failure(new ServiceError.RemoteServiceError(PreferencesLocalization.Current("AI request timed out or was canceled.")));
		  }
		  if (message.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0
		   && (message.IndexOf("country", StringComparison.OrdinalIgnoreCase) >= 0
		    || message.IndexOf("region", StringComparison.OrdinalIgnoreCase) >= 0
		    || message.IndexOf("territory", StringComparison.OrdinalIgnoreCase) >= 0))
		  {
		   return ServiceResult<T>.Failure(new ServiceError.RemoteServiceError(PreferencesLocalization.Current("Country, region, or territory not supported")));
		  }
		 }
		 return result;
		}

		private static bool IsCancellationMessage(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
			{
				return false;
			}
			return message.IndexOf("task was canceled", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("task canceled", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("canceled", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.IndexOf("cancelled", StringComparison.OrdinalIgnoreCase) >= 0
				|| message.Contains("已取消一个任务")
				|| message.Contains("工作已取消")
				|| message.Contains("作業已取消");
		}

		private static string[] DecodeModels(JObject json)
		{
			if (!(json["data"] is JArray data))
			{
				return null;
			}
			List<string> models = new List<string>();
			foreach (JToken token in data)
			{
				string id = token["id"]?.Value<string>();
				if (!string.IsNullOrWhiteSpace(id))
				{
					models.Add(id);
				}
			}
			return models.ToArray();
		}

		private static string NormalizeServiceUrl(string serviceUrl)
		{
			string normalized = (serviceUrl ?? "https://api.openai.com").Trim().TrimEnd('/');
			if (normalized.EndsWith("/v1/models", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(0, normalized.Length - "/v1/models".Length);
			}
			if (normalized.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(0, normalized.Length - "/v1/chat/completions".Length);
			}
			if (normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(0, normalized.Length - "/v1".Length);
			}
			return normalized;
		}

		protected override ServiceResult<T> DecodeJsonError<T>(ServiceError.RemoteServiceJsonError jsonError)
		{
			string text = DecodeServiceError(jsonError.Json);
			if (text != null)
			{
				return ServiceResult<T>.Failure(new ServiceError.RemoteServiceError(text));
			}
			// DecodeServiceError 无法解析时，用原始 JSON 文本作为错误消息，
			// 而非通用的"远程服务返回了错误响应"——后者不含排队/瞬时错误关键字，
			// 会导致 ShouldRetry 无法识别排队场景而直接放弃重试。
			string rawJson = jsonError.Json?.ToString(Newtonsoft.Json.Formatting.None) ?? "";
			if (!string.IsNullOrWhiteSpace(rawJson))
			{
				return ServiceResult<T>.Failure(new ServiceError.RemoteServiceError(
					PreferencesLocalization.FormatCurrent("AI service returned an error: {0}", TrimErrorMessage(rawJson, 1000))));
			}
			return base.DecodeJsonError<T>(jsonError);
		}

		[Null]
		public static string DecodeServiceError([Null] JContainer json)
		{
			if (json != null)
			{
				string text = DecodeServiceErrorToken(json);
				if (!string.IsNullOrWhiteSpace(text))
				{
					// 附加 HTTP 状态码（由 Connection.DeserializeJsonError 注入），让 ShouldRetry 能
					// 通过状态码数字（429/503 等）识别排队/限流场景——仅靠消息文本可能不含这些关键字。
					int? statusCode = ExtractHttpStatusCode(json);
					if (statusCode.HasValue && statusCode.Value >= 300)
					{
						text = $"[HTTP {statusCode.Value}] {text}";
					}
					Log.Warn(text);
					return text;
				}
				string rawJson = json.ToString(Newtonsoft.Json.Formatting.None);
				Log.Warn("Cannot parse Error json: " + rawJson);
				return PreferencesLocalization.FormatCurrent("AI service returned an error: {0}", TrimErrorMessage(rawJson, 1000));
			}
			Log.Warn("Cannot parse Error json");
			return null;
		}

		/// <summary>提取 Connection.DeserializeJsonError 注入的 HTTP 状态码（若无返回 null）。</summary>
		private static int? ExtractHttpStatusCode(JContainer json)
		{
			if (json is JObject obj)
			{
				JToken token = obj["__http_status_code__"];
				if (token != null && token.Type == JTokenType.Integer)
				{
					return token.Value<int>();
				}
			}
			return null;
		}

		[Null]
		private static string DecodeServiceErrorToken([Null] JToken token)
		{
			if (token == null || token.Type == JTokenType.Null)
			{
				return null;
			}
			if (token.Type == JTokenType.String)
			{
				return token.Value<string>();
			}
			if (token is JObject obj)
			{
				string text = obj.GetString("error", "message")
					?? obj.GetString("error", "detail")
					?? obj.GetString("error", "code")
					?? obj.GetString("message")
					?? obj.GetString("detail")
					?? obj.GetString("error_description")
					?? DecodeServiceErrorToken(obj["error"])
					?? DecodeServiceErrorToken(obj["errors"]);
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text;
				}
				foreach (JProperty property in obj.Properties())
				{
					text = DecodeServiceErrorToken(property.Value);
					if (!string.IsNullOrWhiteSpace(text))
					{
						return text;
					}
				}
			}
			if (token is JArray array)
			{
				List<string> messages = new List<string>();
				foreach (JToken item in array)
				{
					string text = DecodeServiceErrorToken(item);
					if (!string.IsNullOrWhiteSpace(text))
					{
						messages.Add(text);
					}
				}
				if (messages.Count > 0)
				{
					return string.Join("\n", messages);
				}
			}
			return null;
		}

		private static string TrimErrorMessage(string message, int maxLength)
		{
			if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
			{
				return message ?? "";
			}
			return message.Substring(0, maxLength) + "...";
		}
	}
}
