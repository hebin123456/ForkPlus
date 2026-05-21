using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
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

		static Connection()
		{
			ClientHandler = new HttpClientHandler();
			Client = new HttpClient(ClientHandler);
			ClientHandler.UseCookies = false;
		}

		public Connection(string serverUrl, [Null] IRestServiceAuthentication authentication)
		{
			ServerUrl = serverUrl;
			Authentication = authentication;
		}

		public HttpRequestResult Request(ApiRequest apiRequest, bool jsonRequest = false)
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
			request.Headers.Add("User-Agent", App.UserAgent);
			if (jsonRequest)
			{
				request.Headers.Add("Accept", "application/json; charset=utf-8");
			}
			if (Authentication != null && !Authentication.Authorize(request))
			{
				return HttpRequestResult.Failure(new ServiceError.AuthorizationLoadingError());
			}
			Task<HttpResponseMessage> task = Client.SendAsync(request);
			try
			{
				task.Wait();
			}
			catch (Exception ex2)
			{
				string innerExceptionMessage2 = GetInnerExceptionMessage(ex2);
				Log.Warn($"{apiRequest.HttpMethod} {text} ({innerExceptionMessage2})");
				return HttpRequestResult.Failure(new ServiceError.UnknownError(innerExceptionMessage2));
			}
			HttpResponseMessage result = task.Result;
			Log.Debug($"{stopwatch.ElapsedMilliseconds,7}ms: {apiRequest.HttpMethod} {text} ({(int)result.StatusCode})");
			if (IsError(result.StatusCode))
			{
				Log.Warn($"{(int)result.StatusCode} {result.ReasonPhrase}");
				if (result.StatusCode == HttpStatusCode.NotFound)
				{
					return HttpRequestResult.Failure(new ServiceError.NotFound(text));
				}
				if (IsJsonError(result))
				{
					return DeserializeJsonError(result);
				}
				return HttpRequestResult.Failure(new ServiceError.HttpError(result.StatusCode));
			}
			Task<string> task2 = result.Content.ReadAsStringAsync();
			task2.Wait();
			return HttpRequestResult.Success(result.Headers, task2.Result);
		}

		private static HttpRequestResult DeserializeJsonError(HttpResponseMessage errorResponse)
		{
			Task<string> task = errorResponse.Content.ReadAsStringAsync();
			task.Wait();
			string result = task.Result;
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
			return response.Content.Headers.ContentType.MediaType == "application/json";
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
			HttpRequestResult httpRequestResult = Request(apiRequest, jsonRequest: true);
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
