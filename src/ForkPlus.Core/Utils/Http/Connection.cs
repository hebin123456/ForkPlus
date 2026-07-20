using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ForkPlus.Jobs;
using ForkPlus.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Utils.Http
{
	public class Connection
	{
		public class HttpRequestResult : ServiceResult
		{
			[Null]
			public HttpResponseHeaders Headers { get; }

			[Null]
			public string Result { get; }

			public HttpRequestResult(bool success, [Null] HttpResponseHeaders headers, [Null] string result, [Null] ServiceError error)
				: base(success, error)
			{
				Headers = headers;
				Result = result;
			}

			public static HttpRequestResult Success(HttpResponseHeaders headers, string result)
			{
				return new HttpRequestResult(success: true, headers, result, null);
			}

			public static HttpRequestResult Failure(ServiceError error)
			{
				return new HttpRequestResult(success: false, null, null, error);
			}
		}

		protected static readonly HttpClientHandler ClientHandler;

		protected static readonly HttpClient Client;

		public readonly string ServerUrl;

		[Null]
		public readonly IRestServiceAuthentication Authentication;

		private readonly int _timeoutSeconds;

		static Connection()
		{
			ClientHandler = new HttpClientHandler();
			ClientHandler.UseCookies = false;
			Client = new HttpClient(ClientHandler);
		}

		public Connection(string serverUrl, [Null] IRestServiceAuthentication authentication)
			: this(serverUrl, authentication, 0)
		{
		}

		public Connection(string serverUrl, [Null] IRestServiceAuthentication authentication, int timeoutSeconds)
		{
			ServerUrl = serverUrl;
			Authentication = authentication;
			_timeoutSeconds = timeoutSeconds;
		}

		public HttpRequestResult Request(ApiRequest apiRequest, bool jsonRequest = false)
		{
			return Request(apiRequest, jsonRequest, null);
		}

		public HttpRequestResult Request(ApiRequest apiRequest, bool jsonRequest, JobMonitor monitor)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			string text = ServerUrl + apiRequest.Slug;
			HttpRequestMessage request;
			try
			{
				request = new HttpRequestMessage
				{
					RequestUri = new Uri(text),
					Method = apiRequest.HttpMethod
				};
				if (apiRequest.HttpMethod == HttpMethod.Post)
				{
					if (apiRequest.Json != null)
					{
						request.Content = new StringContent(apiRequest.Json.ToString(), Encoding.UTF8, "application/json");
					}
					else if (apiRequest.Parameters != null)
					{
						request.Content = new FormUrlEncodedContent(apiRequest.Parameters);
					}
				}
			}
			catch (Exception ex)
			{
				string innerExceptionMessage = GetInnerExceptionMessage(ex);
				Log.Warn($"{apiRequest.HttpMethod} {text} ({innerExceptionMessage})");
				return HttpRequestResult.Failure(new ServiceError.UnknownError(innerExceptionMessage));
			}
			// 确保请求和响应消息被释放，避免 socket/内存泄漏。
			using (request)
			{
			request.Headers.Add("User-Agent", ServiceLocator.AppContext.UserAgent);
			if (jsonRequest)
			{
				request.Headers.Add("Accept", "application/json; charset=utf-8");
			}
			if (Authentication != null && !Authentication.Authorize(request))
			{
				return HttpRequestResult.Failure(new ServiceError.AuthorizationLoadingError());
			}
			CancellationTokenSource cancellationTokenSource = _timeoutSeconds > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds)) : null;
			monitor?.SetCancellationAction(delegate
			{
				cancellationTokenSource?.Cancel();
			});
			Task<HttpResponseMessage> task = cancellationTokenSource != null ? Client.SendAsync(request, cancellationTokenSource.Token) : Client.SendAsync(request);
			try
			{
				HttpRequestResult waitResult = WaitForCompletion(task, cancellationTokenSource, monitor, stopwatch, apiRequest.HttpMethod, text);
				if (waitResult != null)
				{
					return waitResult;
				}
			}
			catch (Exception ex2)
			{
				monitor?.SetCancellationAction(null);
				cancellationTokenSource?.Dispose();
				string innerExceptionMessage2 = GetInnerExceptionMessage(ex2);
				Log.Warn($"{apiRequest.HttpMethod} {text} ({innerExceptionMessage2})");
				return HttpRequestResult.Failure(new ServiceError.UnknownError(innerExceptionMessage2));
			}
			HttpResponseMessage result = task.Result;
			using (result)
			{
			Log.Debug($"{stopwatch.ElapsedMilliseconds,7}ms: {apiRequest.HttpMethod} {text} ({(int)result.StatusCode})");
			if (IsError(result.StatusCode))
			{
				Log.Warn($"{(int)result.StatusCode} {result.ReasonPhrase}");
				if (result.StatusCode == HttpStatusCode.NotFound)
				{
					ClearCancellation(monitor, cancellationTokenSource);
					return HttpRequestResult.Failure(new ServiceError.NotFound(text));
				}
				if (IsJsonError(result))
					{
						Task<string> errorContentTask = result.Content.ReadAsStringAsync();
						HttpRequestResult errorBodyWaitResult = WaitForCompletion(errorContentTask, cancellationTokenSource, monitor, stopwatch, apiRequest.HttpMethod, text);
						if (errorBodyWaitResult != null)
						{
							return errorBodyWaitResult;
						}
						ClearCancellation(monitor, cancellationTokenSource);
						return DeserializeJsonError(errorContentTask.Result, result.StatusCode);
					}
					ClearCancellation(monitor, cancellationTokenSource);
					return HttpRequestResult.Failure(new ServiceError.HttpError(result.StatusCode));
			}
			Task<string> task2 = result.Content.ReadAsStringAsync();
			HttpRequestResult bodyWaitResult = WaitForCompletion(task2, cancellationTokenSource, monitor, stopwatch, apiRequest.HttpMethod, text);
			if (bodyWaitResult != null)
			{
				return bodyWaitResult;
			}
			ClearCancellation(monitor, cancellationTokenSource);
			return HttpRequestResult.Success(result.Headers, task2.Result);
			}
			}
		}

		public HttpRequestResult RequestStream(ApiRequest apiRequest, bool jsonRequest, JobMonitor monitor, Action<string> onLine)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			string text = ServerUrl + apiRequest.Slug;
			HttpRequestMessage request;
			try
			{
				request = new HttpRequestMessage
				{
					RequestUri = new Uri(text),
					Method = apiRequest.HttpMethod
				};
				if (apiRequest.HttpMethod == HttpMethod.Post)
				{
					if (apiRequest.Json != null)
					{
						request.Content = new StringContent(apiRequest.Json.ToString(), Encoding.UTF8, "application/json");
					}
					else if (apiRequest.Parameters != null)
					{
						request.Content = new FormUrlEncodedContent(apiRequest.Parameters);
					}
				}
			}
			catch (Exception ex)
			{
				string innerExceptionMessage = GetInnerExceptionMessage(ex);
				Log.Warn($"{apiRequest.HttpMethod} {text} ({innerExceptionMessage})");
				return HttpRequestResult.Failure(new ServiceError.UnknownError(innerExceptionMessage));
			}
			using (request)
			{
				request.Headers.Add("User-Agent", ServiceLocator.AppContext.UserAgent);
				if (jsonRequest)
				{
					request.Headers.Add("Accept", "application/json; charset=utf-8");
				}
				if (Authentication != null && !Authentication.Authorize(request))
				{
					return HttpRequestResult.Failure(new ServiceError.AuthorizationLoadingError());
				}
				CancellationTokenSource cancellationTokenSource = _timeoutSeconds > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds)) : null;
				monitor?.SetCancellationAction(delegate
				{
					cancellationTokenSource?.Cancel();
				});
				// HttpCompletionOption.ResponseHeadersRead：立即返回响应头，响应体后续按流读取。
				// 这是 SSE 流式输出的关键——不等整个响应体到达就开始逐行读取。
				Task<HttpResponseMessage> task = cancellationTokenSource != null ? Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationTokenSource.Token) : Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
				try
				{
					HttpRequestResult waitResult = WaitForCompletion(task, cancellationTokenSource, monitor, stopwatch, apiRequest.HttpMethod, text);
					if (waitResult != null)
					{
						return waitResult;
					}
				}
				catch (Exception ex2)
				{
					monitor?.SetCancellationAction(null);
					cancellationTokenSource?.Dispose();
					string innerExceptionMessage2 = GetInnerExceptionMessage(ex2);
					Log.Warn($"{apiRequest.HttpMethod} {text} ({innerExceptionMessage2})");
					return HttpRequestResult.Failure(new ServiceError.UnknownError(innerExceptionMessage2));
				}
				HttpResponseMessage result = task.Result;
				using (result)
				{
					Log.Debug($"{stopwatch.ElapsedMilliseconds,7}ms: {apiRequest.HttpMethod} {text} ({(int)result.StatusCode})");
					if (IsError(result.StatusCode))
					{
						Log.Warn($"{(int)result.StatusCode} {result.ReasonPhrase}");
						if (result.StatusCode == HttpStatusCode.NotFound)
						{
							ClearCancellation(monitor, cancellationTokenSource);
							return HttpRequestResult.Failure(new ServiceError.NotFound(text));
						}
						if (IsJsonError(result))
					{
						Task<string> errorContentTask = result.Content.ReadAsStringAsync();
						HttpRequestResult errorBodyWaitResult = WaitForCompletion(errorContentTask, cancellationTokenSource, monitor, stopwatch, apiRequest.HttpMethod, text);
						if (errorBodyWaitResult != null)
						{
							return errorBodyWaitResult;
						}
						ClearCancellation(monitor, cancellationTokenSource);
						return DeserializeJsonError(errorContentTask.Result, result.StatusCode);
					}
					ClearCancellation(monitor, cancellationTokenSource);
					return HttpRequestResult.Failure(new ServiceError.HttpError(result.StatusCode));
				}
				// 流式读取响应体：逐行读取 SSE 事件，空闲超时（idleStopwatch）在每收到一行后重置。
				// 这样只要 AI 持续输出 chunk 就不会超时，只有真正卡住（无数据到达）才触发超时。
				StringBuilder fullBody = new StringBuilder();
					Stopwatch idleStopwatch = Stopwatch.StartNew();
					try
					{
						Task<Stream> streamTask = result.Content.ReadAsStreamAsync();
						HttpRequestResult streamWaitResult = WaitForCompletion(streamTask, cancellationTokenSource, monitor, stopwatch, apiRequest.HttpMethod, text);
						if (streamWaitResult != null)
						{
							return streamWaitResult;
						}
						Stream responseStream = streamTask.Result;
						StreamReader reader = new StreamReader(responseStream);
						while (true)
						{
							Task<string> lineTask = reader.ReadLineAsync();
							HttpRequestResult lineWaitResult = WaitForCompletion(lineTask, cancellationTokenSource, monitor, stopwatch, apiRequest.HttpMethod, text, idleStopwatch);
							if (lineWaitResult != null)
							{
								return lineWaitResult;
							}
							string line = lineTask.Result;
							if (line == null)
							{
								break;
							}
							// 收到数据，重置空闲计时器
							idleStopwatch.Restart();
							onLine?.Invoke(line);
							fullBody.AppendLine(line);
						}
					}
					catch (Exception ex3)
					{
						string innerExceptionMessage3 = GetInnerExceptionMessage(ex3);
						Log.Warn($"{apiRequest.HttpMethod} {text} ({innerExceptionMessage3})");
						ClearCancellation(monitor, cancellationTokenSource);
						return HttpRequestResult.Failure(new ServiceError.UnknownError(innerExceptionMessage3));
					}
					ClearCancellation(monitor, cancellationTokenSource);
					return HttpRequestResult.Success(result.Headers, fullBody.ToString());
				}
			}
		}

		[Null]
		private HttpRequestResult WaitForCompletion(Task task, [Null] CancellationTokenSource cancellationTokenSource, [Null] JobMonitor monitor, Stopwatch stopwatch, HttpMethod method, string url, [Null] Stopwatch idleStopwatch = null)
		{
			while (true)
			{
				bool completed;
				try
				{
					completed = task.Wait(250);
				}
				catch (Exception ex)
				{
					string innerExceptionMessage = GetInnerExceptionMessage(ex);
					Log.Warn($"{method} {url} ({innerExceptionMessage})");
					ClearCancellation(monitor, cancellationTokenSource);
					return HttpRequestResult.Failure(new ServiceError.UnknownError(innerExceptionMessage));
				}
				if (completed)
				{
					break;
				}
				if (monitor?.IsCanceled == true)
				{
					cancellationTokenSource?.Cancel();
					Log.Warn($"{method} {url} (cancelled)");
					monitor?.SetCancellationAction(null);
					cancellationTokenSource?.Dispose();
					return HttpRequestResult.Failure(new ServiceError.Cancelled());
				}
				Stopwatch timeoutStopwatch = idleStopwatch ?? stopwatch;
				if (cancellationTokenSource?.IsCancellationRequested == true || (_timeoutSeconds > 0 && timeoutStopwatch.Elapsed >= TimeSpan.FromSeconds(_timeoutSeconds)))
				{
					cancellationTokenSource?.Cancel();
					Log.Warn($"{method} {url} (timeout after {_timeoutSeconds}s)");
					monitor?.SetCancellationAction(null);
					cancellationTokenSource?.Dispose();
					return HttpRequestResult.Failure(new ServiceError.UnknownError("The operation timed out."));
				}
			}
			if (task.IsCanceled)
			{
				ClearCancellation(monitor, cancellationTokenSource);
				return HttpRequestResult.Failure(new ServiceError.Cancelled());
			}
			if (task.IsFaulted)
			{
				string innerExceptionMessage = GetInnerExceptionMessage(task.Exception);
				Log.Warn($"{method} {url} ({innerExceptionMessage})");
				ClearCancellation(monitor, cancellationTokenSource);
				return HttpRequestResult.Failure(new ServiceError.UnknownError(innerExceptionMessage));
			}
			return null;
		}

		private static void ClearCancellation([Null] JobMonitor monitor, [Null] CancellationTokenSource cancellationTokenSource)
		{
			monitor?.SetCancellationAction(null);
			cancellationTokenSource?.Dispose();
		}

		private static HttpRequestResult DeserializeJsonError(string result, HttpStatusCode statusCode = HttpStatusCode.OK)
		{
			if (!(JsonConvert.DeserializeObject(result) is JContainer json))
			{
				Log.Error("Cannot deserialize json in the server error");
				return HttpRequestResult.Failure(new ServiceError.JsonParseError());
			}
			Log.Warn(result);
			// 注入 HTTP 状态码到 JSON 根对象，供下游 DecodeServiceError 提取。
			// 排队/限流场景常用 429/503 状态码 + JSON 错误体，但 RemoteServiceJsonError.FriendlyMessage
			// 为通用文案不含状态码，导致 ShouldRetry 无法识别。注入后 DecodeServiceError 能取到状态码数字，
			// ShouldRetry 的 message.Contains("429"/"503"...) 即可命中触发重试。
			if (json is JObject jobj && (int)statusCode >= 300)
			{
				jobj["__http_status_code__"] = (int)statusCode;
			}
			return HttpRequestResult.Failure(new ServiceError.RemoteServiceJsonError(json));
		}

		private static bool IsJsonError(HttpResponseMessage response)
		{
			return response?.Content?.Headers?.ContentType?.MediaType == "application/json";
		}

		private static string GetInnerExceptionMessage(Exception ex)
		{
			Exception innerException = ex.InnerException;
			if (innerException != null)
			{
				return GetInnerExceptionMessage(innerException);
			}
			return ex.Message;
		}

		public ServiceResult<object> JsonRequest(ApiRequest apiRequest)
		{
			return JsonRequest(apiRequest, null);
		}

		public ServiceResult<object> JsonRequest(ApiRequest apiRequest, JobMonitor monitor)
		{
			HttpRequestResult httpRequestResult = Request(apiRequest, jsonRequest: true, monitor);
			if (!httpRequestResult.Succeeded)
			{
				return ServiceResult<object>.Failure(httpRequestResult.Error);
			}
			object obj = JsonConvert.DeserializeObject(httpRequestResult.Result);
			if (obj == null)
			{
				Log.Error("Cannot parse json in the server response content");
				return ServiceResult<object>.Failure(new ServiceError.JsonParseError());
			}
			return ServiceResult<object>.Success(obj);
		}

		private static bool IsError(HttpStatusCode statusCode)
		{
			if (statusCode >= HttpStatusCode.OK)
			{
				return statusCode >= HttpStatusCode.MultipleChoices;
			}
			return true;
		}
	}
}
