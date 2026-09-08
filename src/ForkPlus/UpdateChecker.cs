using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using ForkPlus.Settings;
using Newtonsoft.Json.Linq;

namespace ForkPlus
{
	public class UpdateInfo
	{
		public string LatestVersion { get; set; } = "";

		public string CurrentVersion { get; set; } = "";

		public bool HasUpdate { get; set; }

		public string ReleaseName { get; set; } = "";

		public string ReleaseNotes { get; set; } = "";

		public string ReleaseUrl { get; set; } = "";

		public string DownloadUrl { get; set; } = "";

		/// <summary>检测失败时的错误信息（非空表示检测失败）。</summary>
		public string ErrorMessage { get; set; } = "";
	}

	/// <summary>
	/// 通过 GitHub Releases API 检测新版本。
	/// 使用独立的 HttpClient（UseProxy=false 直连），避免系统代理导致的 504 网关超时。
	/// </summary>
	public class UpdateChecker
	{
		private const string LatestReleaseUrl = "https://api.github.com/repos/hebin123456/ForkPlus/releases/latest";

		private readonly string _releaseUrl;

		private readonly int _timeoutSeconds;

		public UpdateChecker(int timeoutSeconds = 30)
			: this(LatestReleaseUrl, timeoutSeconds)
		{
		}

		/// <summary>测试用：指定 release 查询地址（默认构造直连 GitHub 官方 API）。</summary>
		internal UpdateChecker(string releaseUrl, int timeoutSeconds)
		{
			_releaseUrl = releaseUrl;
			_timeoutSeconds = timeoutSeconds;
		}

		/// <summary>
		/// 查询 GitHub 最新 Release 并与当前版本比较。
		/// 失败时 HasUpdate=false 且 ErrorMessage 非空（不抛异常）。
		/// cancellationToken 可用于中止 HTTP 请求。
		/// </summary>
		public UpdateInfo CheckLatestRelease(CancellationToken cancellationToken = default(CancellationToken))
		{
			UpdateInfo info = new UpdateInfo
			{
				CurrentVersion = App.Version
			};
			if (cancellationToken.IsCancellationRequested)
			{
				info.ErrorMessage = "Cancelled";
				return info;
			}
			try
			{
				// 尝试 1（直连）：UseProxy=false 绕过系统代理，
				// 避免用户代理（clash/v2ray 等）对 api.github.com 不通时返回 504。
				if (!TryFetchReleaseJson(useProxy: false, cancellationToken, out string body, out string directError))
				{
					// 尝试 2（系统代理）：部分网络的直连出网会被网关劫持，返回
					// 302 AuthenticationRequired（要求门户登录）之类的非成功响应或门户 HTML；
					// 此时若系统已配置代理，走代理通常可正常到达 GitHub。
					// loopback 地址无法经代理到达，等价于直接重试一次。
					bool viaProxy = !IsLoopbackUrl(_releaseUrl);
					if (!TryFetchReleaseJson(viaProxy, cancellationToken, out body, out string proxyError))
					{
						// 两次都失败：以直连错误为主报错（与旧行为一致，便于诊断直连网络）
						info.ErrorMessage = directError;
						Log.Warn("Update check failed (direct): " + directError + " (via proxy): " + proxyError);
						return info;
					}
				}
				JObject json = JObject.Parse(body);
				string tagName = json["tag_name"]?.Value<string>() ?? "";
				if (string.IsNullOrEmpty(tagName))
				{
					// 限流或异常响应（无 tag_name）
					info.ErrorMessage = json["message"]?.Value<string>() ?? "Invalid response";
					Log.Warn("Update check invalid response: " + info.ErrorMessage);
					return info;
				}
				info.LatestVersion = NormalizeVersion(tagName);
				info.ReleaseName = json["name"]?.Value<string>() ?? "";
				info.ReleaseNotes = json["body"]?.Value<string>() ?? "";
				info.ReleaseUrl = json["html_url"]?.Value<string>() ?? "";
				JArray assets = json["assets"] as JArray;
				if (assets != null && assets.Count > 0)
				{
					info.DownloadUrl = assets[0]["browser_download_url"]?.Value<string>() ?? info.ReleaseUrl;
				}
				else
				{
					info.DownloadUrl = info.ReleaseUrl;
				}
				info.HasUpdate = IsNewerVersion(info.LatestVersion, info.CurrentVersion);
			}
			catch (OperationCanceledException)
			{
				info.ErrorMessage = "Cancelled";
			}
			catch (Exception ex)
			{
				info.ErrorMessage = ex.Message;
				Log.Warn("Update check exception: " + ex.Message);
			}
			return info;
		}

		/// <summary>
		/// 发起一次 release 查询。返回 true 时 body 为 JSON 响应体；返回 false 表示本次尝试失败
		/// （非成功状态码 / 响应体非 JSON / 网络异常 / 超时），error 为失败原因。
		/// 响应体解析提前到这里：直连被劫持跳到门户登录页时拿到的是 200+HTML，
		/// 必须按失败处理才能触发系统代理回退。
		/// </summary>
		private bool TryFetchReleaseJson(bool useProxy, CancellationToken cancellationToken, out string body, out string error)
		{
			body = null;
			error = null;
			try
			{
				HttpClientHandler handler = new HttpClientHandler { UseProxy = useProxy };
				using (HttpClient client = new HttpClient(handler))
				{
					client.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);
					client.DefaultRequestHeaders.UserAgent.ParseAdd(App.UserAgent);
					client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
					HttpResponseMessage response = client.GetAsync(_releaseUrl, cancellationToken).GetAwaiter().GetResult();
					if (!response.IsSuccessStatusCode)
					{
						error = ((int)response.StatusCode).ToString() + " " + response.ReasonPhrase;
						Log.Warn("Update check HTTP error (useProxy=" + useProxy + "): " + error);
						return false;
					}
					string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					JObject.Parse(content); // 非 JSON（如门户 HTML）会抛异常，按失败处理
					body = content;
					return true;
				}
			}
			catch (OperationCanceledException ex)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					throw; // 用户主动取消：交给外层统一记为 Cancelled
				}
				// HttpClient.Timeout 超时（.NET 5+ 为 TaskCanceledException）：按普通失败处理，
				// 让外层有机会走代理重试，而不是直接放弃
				error = ex.Message;
				return false;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				return false;
			}
		}

		/// <summary>地址是否为 loopback（系统代理无法访问用户本机，第二次尝试等价于直连重试）。</summary>
		private static bool IsLoopbackUrl(string url)
		{
			return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.IsLoopback;
		}

		/// <summary>
		/// 是否应该自动检测：开关开启 且 距上次检测达到设定间隔。
		/// 间隔下限 12 小时，避免过于频繁。
		/// </summary>
		public static bool ShouldAutoCheck()
		{
			if (!ForkPlusSettings.Default.CheckForUpdatesAutomatically)
			{
				return false;
			}
			int intervalHours = Math.Max(12, ForkPlusSettings.Default.UpdateCheckIntervalHours);
			return DateTime.Now - ForkPlusSettings.Default.LastUpdateCheck >= TimeSpan.FromHours(intervalHours);
		}

		/// <summary>此版本是否被用户标记为“不再提醒”。</summary>
		public static bool IsVersionSkipped(string version)
		{
			if (string.IsNullOrEmpty(version))
			{
				return false;
			}
			string skipped = ForkPlusSettings.Default.SkippedUpdateVersion;
			return !string.IsNullOrEmpty(skipped) && skipped == version;
		}

		/// <summary>记录本次检测时间并持久化。</summary>
		public static void MarkChecked()
		{
			ForkPlusSettings.Default.LastUpdateCheck = DateTime.Now;
			ForkPlusSettings.Default.Save();
		}

		/// <summary>标记跳过指定版本（不再提醒）。</summary>
		public static void SkipVersion(string version)
		{
			ForkPlusSettings.Default.SkippedUpdateVersion = version ?? "";
			ForkPlusSettings.Default.Save();
		}

		/// <summary>规范化版本号：去掉 "v"/"V" 前缀。</summary>
		public static string NormalizeVersion(string tag)
		{
			if (string.IsNullOrEmpty(tag))
			{
				return "";
			}
			string v = tag.Trim();
			if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
			{
				v = v.Substring(1);
			}
			return v;
		}

		/// <summary>语义化版本比较：latest 是否严格大于 current。</summary>
		public static bool IsNewerVersion(string latest, string current)
		{
			if (string.IsNullOrEmpty(latest) || string.IsNullOrEmpty(current))
			{
				return false;
			}
			if (Version.TryParse(NormalizeVersion(latest), out Version lv) &&
				Version.TryParse(NormalizeVersion(current), out Version cv))
			{
				return lv > cv;
			}
			return string.CompareOrdinal(NormalizeVersion(latest), NormalizeVersion(current)) > 0;
		}
	}
}
