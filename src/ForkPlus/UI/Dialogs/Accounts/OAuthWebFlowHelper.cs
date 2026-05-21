using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.Dialogs.Accounts
{
	internal static class OAuthWebFlowHelper
	{
		private const string DefaultSuccessHtml = "<!DOCTYPE html><html><head>\n<style>body{display:flex;align-items:center;justify-content:center;font-family:Helvetica,Arial;color:#484D4D;}h2,p{font-weight:300;font-size:30;text-align:center;}img{display:block;margin-left: auto;margin-right:auto;width:50%;}</style>\n<title>Authentication successful</title></head>\n<body><div>\n  <img src=\"https://hebin.me/images/logo.png\"/>\n  <h2>Authentication successful</h2><p>You can now close this page.</p>\n</div></body></html>";

		private const string DefaultFailureHtmlFormat = "<!DOCTYPE html><html><head>\n<style>body{display:flex;align-items:center;justify-content:center;font-family:Helvetica,Arial;color:#484D4D;}h2{font-weight:300;font-size:30;text-align:center;}dt{font-weight:500;}dd{margin-bottom:10px;}img{display:block;margin-left:auto;margin-right:auto;width:50%;}</style>\n<title>Authentication failed</title></head>\n<body><div>\n  <img src=\"https://hebin.me/images/logo.png\"/>\n  <h2>Authentication failed</h2>\n  <dl>\n    <dt>Error:</dt><dd>{0}</dd>\n    <dt>Description:</dt><dd>{1}</dd>\n    <dt>URL:</dt><dd>{2}</dd>\n  </dl>\n</div></body></html>";

		public static string GenerateRandomState()
		{
			return Guid.NewGuid().ToString("N");
		}

		public static ServiceResult<string> Authorize(string authorizeUrl, [Null] string state, CancellationToken cancellationToken)
		{
			Process.Start(new ProcessStartInfo(authorizeUrl)
			{
				UseShellExecute = true
			});
			Task<ServiceResult<string>> task = ListenOAutoCallbackAsync("http://localhost:36106", state, cancellationToken);
			task.Wait();
			return task.Result;
		}

		[Null]
		private static async Task<ServiceResult<string>> ListenOAutoCallbackAsync(string url, string state, CancellationToken cancellationToken)
		{
			if (!url.EndsWith("/"))
			{
				url += "/";
			}
			TaskCompletionSource<ServiceResult<string>> tcs = new TaskCompletionSource<ServiceResult<string>>();
			cancellationToken.Register(delegate
			{
				tcs.SetCanceled();
			});
			HttpListener listener = new HttpListener();
			listener.Prefixes.Add(url);
			listener.Start();
			try
			{
				Task<HttpListenerContext> contextTask = listener.GetContextAsync();
				Task<ServiceResult<string>> cancelTask = tcs.Task;
				if (await Task.WhenAny(contextTask, cancelTask) == cancelTask)
				{
					Log.Info("Cancelling OAuth callback...");
					return ServiceResult<string>.Failure(new ServiceError.Cancelled());
				}
				HttpListenerContext httpListenerContext = await contextTask;
				Log.Info("Received OAuth callback on " + httpListenerContext.Request.RawUrl);
				if (state != null && !(state == httpListenerContext.Request.QueryString["state"]))
				{
					string text = "The response state doesn't match. Authentication is not possible";
					ResponseWithOutput(httpListenerContext, ErrorHtml("state_mismatch", text));
					return ServiceResult<string>.Failure(new ServiceError.UnknownError(text));
				}
				string text2 = httpListenerContext.Request.QueryString["error"];
				if (text2 != null)
				{
					string text3 = httpListenerContext.Request.QueryString["error_description"] ?? text2;
					ResponseWithOutput(httpListenerContext, ErrorHtml(text2, text3, httpListenerContext.Request.QueryString["error_uri"]));
					return ServiceResult<string>.Failure(new ServiceError.UnknownError(text3));
				}
				string text4 = httpListenerContext.Request.QueryString["code"];
				if (text4 == null)
				{
					string text5 = "Communication error. Haven't received OAuth code";
					ResponseWithOutput(httpListenerContext, ErrorHtml("no_code", text5));
					return ServiceResult<string>.Failure(new ServiceError.UnknownError(text5));
				}
				ResponseWithOutput(httpListenerContext, "<!DOCTYPE html><html><head>\n<style>body{display:flex;align-items:center;justify-content:center;font-family:Helvetica,Arial;color:#484D4D;}h2,p{font-weight:300;font-size:30;text-align:center;}img{display:block;margin-left: auto;margin-right:auto;width:50%;}</style>\n<title>Authentication successful</title></head>\n<body><div>\n  <img src=\"https://hebin.me/images/logo.png\"/>\n  <h2>Authentication successful</h2><p>You can now close this page.</p>\n</div></body></html>");
				return ServiceResult<string>.Success(text4);
			}
			finally
			{
				listener.Stop();
				listener.Close();
			}
		}

		private static string ErrorHtml(string error, [Null] string description, [Null] string uri = null)
		{
			Log.Warn("Error: " + error);
			Log.Warn("Error description: " + description);
			return string.Format("<!DOCTYPE html><html><head>\n<style>body{display:flex;align-items:center;justify-content:center;font-family:Helvetica,Arial;color:#484D4D;}h2{font-weight:300;font-size:30;text-align:center;}dt{font-weight:500;}dd{margin-bottom:10px;}img{display:block;margin-left:auto;margin-right:auto;width:50%;}</style>\n<title>Authentication failed</title></head>\n<body><div>\n  <img src=\"https://hebin.me/images/logo.png\"/>\n  <h2>Authentication failed</h2>\n  <dl>\n    <dt>Error:</dt><dd>{0}</dd>\n    <dt>Description:</dt><dd>{1}</dd>\n    <dt>URL:</dt><dd>{2}</dd>\n  </dl>\n</div></body></html>", error, description ?? "", uri ?? "");
		}

		private static void ResponseWithOutput(HttpListenerContext context, string responseString)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(responseString);
			context.Response.ContentLength64 = bytes.Length;
			context.Response.OutputStream.Write(bytes, 0, bytes.Length);
			context.Response.Close();
		}
	}
}
