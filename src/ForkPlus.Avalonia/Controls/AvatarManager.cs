using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 AvatarManager（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/AvatarManager.cs（430 行）：
    //   - LruCache<string, ImageSource> 缓存（128 项）
    //   - Dictionary<string, List<AvatarImage>> _activeRequests 去重并发请求
    //   - WebClient.DownloadDataAsync + DownloadDataCompleted 事件回调下载头像
    //   - Application.Current.Dispatcher.Invoke 回到 UI 线程更新 Source / Cache
    //   - GenerateAvatar 用 DrawingVisual + DrawingContext 绘制文字头像（占位）
    //     带 5 套 LinearGradientBrush 渐变背景 + 圆角裁切
    //   - RoundCorners 用 DrawingVisual + PushClip(RectangleGeometry) 裁切
    //   - MD5 计算 Gravatar URL；正则识别 GitHub Anonymous Email
    //   - DesignTimeHelper.IsInDesignMode() 设计时跳过下载
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WebClient → HttpClient（spike 用单例 HttpClient + GetByteArrayAsync）
    //      WPF WebClient 已过时（SYSLIB0014），HttpClient 是 .NET 推荐替代
    //   2. Application.Current.Dispatcher.Invoke → Dispatcher.UIThread.Post
    //      （Avalonia 11 的 UI 线程调度入口，Post 异步排队等价 WPF BeginInvoke）
    //   3. ImageSource → Avalonia.Media.IImage（Avalonia 位图抽象）
    //   4. BitmapImage + MemoryStream → Avalonia.Media.Imaging.Bitmap + MemoryStream
    //      （Avalonia Bitmap 直接接受 Stream，无需 BeginInit/EndInit）
    //   5. GenerateAvatar spike 版返回 null（WPF 用 DrawingVisual 绘制文字头像，
    //      Avalonia DrawingImage API 不同且复杂，spike 不实现占位绘制）
    //   6. RoundCorners spike 版直接返回 Bitmap（Avalonia 11 没有 DrawingImage 等价物，
    //      需要 RenderTargetBitmap + DrawingContext 才能裁切，spike 跳过圆角处理）
    //   7. LruCache（来自 ForkPlus.Core）零成本复用
    //
    // spike 版暂不迁移：
    //   - GenerateAvatar 文字头像占位绘制（DrawingVisual + FormattedText）
    //   - RoundCorners 圆角裁切（DrawingVisual + PushClip）
    //   - 5 套 LinearGradientBrush 渐变背景
    //   - GitHub Avatar logo 内嵌资源（pack://application URI 在 Avalonia 用 avares://）
    //   - DesignTimeHelper.IsInDesignMode 检测（Avalonia 用 AvaloniaDesignModeService，
    //     spike 版直接下载，设计时不会触发实际请求）
    public class AvatarManager
    {
        // 对照 WPF: GravatarUrlFormat = "https://en.gravatar.com/avatar/{0}?d=404"
        private const string GravatarUrlFormat = "https://en.gravatar.com/avatar/{0}?d=404";

        // 对照 WPF: GitHubEmail = "noreply@github.com"
        private const string GitHubEmail = "noreply@github.com";

        // 对照 WPF: AnonymousEmailSuffix = "@users.noreply.github.com"
        private const string AnonymousEmailSuffix = "@users.noreply.github.com";

        // 对照 WPF: AnonymousEmailRegex
        private static readonly Regex AnonymousEmailRegex =
            new Regex("^(?:(\\d+)\\+)?(.+?)@users\\.noreply\\.github\\.com$");

        private static readonly object Padlock = new object();

        // HttpClient 静态共享（避免每次请求创建新实例，避免端口耗尽）
        private static readonly HttpClient HttpClient = new HttpClient();

        // 对照 WPF: LruCache<string, ImageSource> _avatarCache (128 项)
        private readonly LruCache<string, IImage> _avatarCache = new LruCache<string, IImage>(128);
        private readonly LruCache<string, IImage> _urlAvatarCache = new LruCache<string, IImage>(128);

        // 对照 WPF: Dictionary<string, List<AvatarImage>> _activeRequests
        //   去重并发请求：同一 email 的多个 AvatarImage 共享一次下载
        private readonly Dictionary<string, List<AvatarImage>> _activeRequests = new Dictionary<string, List<AvatarImage>>();
        private readonly Dictionary<string, List<AvatarImage>> _urlActiveRequests = new Dictionary<string, List<AvatarImage>>();

        private static AvatarManager _default;

        // 对照 WPF: public static AvatarManager Default
        //   单例模式 + 双检锁
        public static AvatarManager Default
        {
            get
            {
                lock (Padlock)
                {
                    if (_default == null)
                    {
                        _default = new AvatarManager();
                    }
                    return _default;
                }
            }
        }

        // 对照 WPF: public void RequestAvatar(AvatarImage avatarImage, UserIdentity userIdentity)
        //   缓存命中直接 SetImage，否则生成占位 → SetImage → 异步下载
        public void RequestAvatar(AvatarImage avatarImage, UserIdentity userIdentity)
        {
            string email = userIdentity.Email.ToLower();
            if (_avatarCache.TryGet(email, out var cached))
            {
                avatarImage.SetImage(cached, avatarImage.UserIdentity);
                return;
            }
            // spike 版 GenerateAvatar 返回 null，直接进入下载流程
            DownloadAvatar(email, avatarImage);
        }

        // 对照 WPF: public void RequestAvatar(AvatarImage avatarImage, string url)
        public void RequestAvatar(AvatarImage avatarImage, string url)
        {
            if (_urlAvatarCache.TryGet(url, out var cached))
            {
                avatarImage.SetImage(cached, avatarImage.UserIdentity);
            }
            else
            {
                DownloadAvatarUrl(url, avatarImage);
            }
        }

        // 对照 WPF: private void DownloadAvatar(string email, AvatarImage imageControl, ImageSource defaultAvatar)
        //   spike 版省略 defaultAvatar 占位参数（GenerateAvatar 返回 null）
        private void DownloadAvatar(string email, AvatarImage imageControl)
        {
            // 已有进行中的请求 → 加入等待列表
            lock (_activeRequests)
            {
                if (_activeRequests.TryGetValue(email, out var waiters))
                {
                    waiters.Add(imageControl);
                    return;
                }
                _activeRequests[email] = new List<AvatarImage> { imageControl };
            }

            // 异步下载（HttpClient + Task.Run）
            _ = Task.Run(async () =>
            {
                IImage downloaded = null;
                try
                {
                    Uri uri = GitHubUri(email) ?? GravatarUri(email);
                    byte[] bytes = await HttpClient.GetByteArrayAsync(uri).ConfigureAwait(false);
                    downloaded = LoadImage(bytes);
                }
                catch (Exception ex)
                {
                    Log.Warn("Avatar downloading failed with error: '" + ex.Message + "'");
                }

                // 回到 UI 线程更新 Source / Cache
                Dispatcher.UIThread.Post(() =>
                {
                    lock (_activeRequests)
                    {
                        if (_activeRequests.TryGetValue(email, out var waiters))
                        {
                            foreach (AvatarImage img in waiters)
                            {
                                img.SetImage(downloaded, imageControl.UserIdentity);
                            }
                        }
                        _activeRequests.Remove(email);
                    }
                    if (downloaded != null)
                    {
                        _avatarCache.Put(email, downloaded);
                    }
                });
            });
        }

        // 对照 WPF: private void DownloadAvatar(string url, AvatarImage imageControl)
        //   重命名为 DownloadAvatarUrl，避免与 email 版签名冲突
        //   （WPF 版有 3 个参数不会冲突，spike 版省略 defaultAvatar 后签名相同）
        private void DownloadAvatarUrl(string url, AvatarImage imageControl)
        {
            lock (_urlActiveRequests)
            {
                if (_urlActiveRequests.TryGetValue(url, out var waiters))
                {
                    waiters.Add(imageControl);
                    return;
                }
                _urlActiveRequests[url] = new List<AvatarImage> { imageControl };
            }

            _ = Task.Run(async () =>
            {
                IImage downloaded = null;
                try
                {
                    byte[] bytes = await HttpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                    downloaded = LoadImage(bytes);
                }
                catch (Exception ex)
                {
                    Log.Warn("Avatar (url) downloading failed with error: '" + ex.Message + "'");
                }

                Dispatcher.UIThread.Post(() =>
                {
                    lock (_urlActiveRequests)
                    {
                        if (_urlActiveRequests.TryGetValue(url, out var waiters))
                        {
                            foreach (AvatarImage img in waiters)
                            {
                                img.SetImage(downloaded, imageControl.UserIdentity);
                            }
                        }
                        _urlActiveRequests.Remove(url);
                    }
                    if (downloaded != null)
                    {
                        _urlAvatarCache.Put(url, downloaded);
                    }
                });
            });
        }

        // 对照 WPF: private static ImageSource LoadImage(byte[] imageData)
        //   WPF 用 BitmapImage + MemoryStream + BeginInit/EndInit + Freeze
        //   Avalonia 用 Bitmap(Stream) 直接构造
        private static IImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                return null;
            }
            try
            {
                using (var stream = new MemoryStream(imageData))
                {
                    return new Bitmap(stream);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Image decoding failed: '{ex}'");
                return null;
            }
        }

        // 对照 WPF: private static Uri GravatarUri(string email)
        //   MD5 hash email → Gravatar URL
        private static Uri GravatarUri(string email)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(email));
                var sb = new StringBuilder(32);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return new Uri(string.Format(GravatarUrlFormat, sb.ToString()));
            }
        }

        // 对照 WPF: private static Uri GitHubUri(string email)
        //   识别 GitHub Anonymous Email → avatars.githubusercontent.com/<username>
        private static Uri GitHubUri(string email)
        {
            if (email.EndsWith(AnonymousEmailSuffix))
            {
                string username = AnonymousGitHubUsername(email);
                if (username != null)
                {
                    return new Uri("https://avatars.githubusercontent.com/" + username);
                }
            }
            return null;
        }

        // 对照 WPF: private static string AnonymousGitHubUsername(string email)
        private static string AnonymousGitHubUsername(string email)
        {
            Match match = AnonymousEmailRegex.Match(email);
            if (match.Groups.Count < 3)
            {
                return null;
            }
            return match.Groups[2].Value;
        }

        // spike 版新增：清空缓存（与 IAutoCompleteProvider 等管理类对齐）
        public void ClearCache()
        {
            lock (_activeRequests) _activeRequests.Clear();
            lock (_urlActiveRequests) _urlActiveRequests.Clear();
        }
    }
}
