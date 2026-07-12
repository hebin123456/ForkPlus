using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ForkPlus.Jobs;
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
			request.Headers.Add("User-Agent", App.UserAgent);
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
					return DeserializeJsonError(errorContentTask.Result);
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

		[Null]
		private HttpRequestResult WaitForCompletion(Task task, [Null] CancellationTokenSource cancellationTokenSource, [Null] JobMonitor monitor, Stopwatch stopwatch, HttpMethod method, string url)
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
				if (cancellationTokenSource?.IsCancellationRequested == true || (_timeoutSeconds > 0 && stopwatch.Elapsed >= TimeSpan.FromSeconds(_timeoutSeconds)))
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

		private static HttpRequestResult DeserializeJsonError(string result)
		{
			if (!(JsonConvert.DeserializeObject(result) is JContainer json))
			{
				Log.Error("Cannot deserialize json in the server error");
				return HttpRequestResult.Failure(new ServiceError.JsonParseError());
			}
			Log.Warn(result);
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
