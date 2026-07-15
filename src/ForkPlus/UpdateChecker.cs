using System;
using System.Threading;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.Utils.Http;
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
	/// </summary>
	public class UpdateChecker
	{
		private const string GitHubApiBase = "https://api.github.com";

		private const string LatestReleasePath = "repos/hebin123456/ForkPlus/releases/latest";

		private readonly int _timeoutSeconds;

		public UpdateChecker(int timeoutSeconds = 30)
		{
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
				Connection connection = new Connection(GitHubApiBase, null, _timeoutSeconds);
				ApiRequest request = new ApiRequest(LatestReleasePath);
				// 用 JobMonitor 承接取消，关窗时 Cancel() 会中止 HTTP 请求
				JobMonitor monitor = new JobMonitor();
				CancellationTokenRegistration reg = cancellationToken.Register(delegate
				{
					monitor.Cancel();
				});
				Connection.HttpRequestResult result = connection.Request(request, jsonRequest: true, monitor);
				reg.Dispose();
				if (cancellationToken.IsCancellationRequested)
				{
					info.ErrorMessage = "Cancelled";
					return info;
				}
				if (!result.Succeeded)
				{
					info.ErrorMessage = result.Error?.FriendlyMessage ?? "Request failed";
					Log.Warn("Update check failed: " + info.ErrorMessage);
					return info;
				}
				JObject json = JObject.Parse(result.Result);
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
