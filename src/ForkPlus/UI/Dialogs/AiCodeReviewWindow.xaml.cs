using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Accounts;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ForkPlus.UI.Dialogs
{
	public partial class AiCodeReviewWindow : CustomWindow
	{
		private RepositoryUserControl _repositoryUserControl;

		private bool _startUpFinished;

		private AiCodeReviewTarget _target;

		private const int AiResultColumn = 2;

		private static string _cachedCss;

		public AiCodeReviewWindow()
		{
			base.ShowInTaskbar = true;
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			InitializeComponent();
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode(this))
			{
				base.Title = PreferencesLocalization.Current("AI Code Review");
				TitleTextBlock.Text = PreferencesLocalization.Current("AI Code Review");
			}
		}

		public AiCodeReviewWindow(RepositoryUserControl repositoryUserControl, AiCodeReviewTarget target, [Null] AiAgent aiAgent)
			: this()
		{
			AiCodeReviewWindow aiCodeReviewWindow = this;
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode(this))
			{
				return;
			}
			_repositoryUserControl = repositoryUserControl;
			_target = target;
			if (target is AiCodeReviewTarget.Branch branch)
			{
				base.Title = ((aiAgent != null) ? (branch.Name + " - " + aiAgent.Name + " Review") : (branch.Name + " - OpenAI Review"));
				LocalBranch localBranch = _repositoryUserControl.RepositoryData.References.LocalMain(_repositoryUserControl.GitModule);
				RemoteBranch remoteBranch = _repositoryUserControl.RepositoryData.References.Upstream(localBranch);
				TitleTextBlock.Text = PreferencesLocalization.FormatCurrent("Code review for {0}...{1}", branch.Name, remoteBranch.Name);
			}
			else if (target is AiCodeReviewTarget.ShaRange shaRange)
			{
				base.Title = ((aiAgent != null) ? (shaRange.Dst.ToAbbreviatedString() + " - " + aiAgent.Name + " Review") : (shaRange.Dst.ToAbbreviatedString() + " - OpenAI Review"));
				TitleTextBlock.Text = PreferencesLocalization.FormatCurrent("Code review for {0}..{1}", shaRange.Src.ToAbbreviatedString(), shaRange.Dst.ToAbbreviatedString());
			}
			base.Loaded += async delegate
			{
				await aiCodeReviewWindow.InitializeWebView();
			};
			base.SizeChanged += Window_SizeChanged;
			base.Activated += Window_Activated;
			if (aiAgent != null)
			{
				ReviewWithAiAgent(repositoryUserControl.GitModule, _target, aiAgent);
			}
			else if (ForkPlusSettings.Default.OpenAiLoggedIn)
			{
				ReviewWithOpenAi(repositoryUserControl.GitModule, _target);
			}
			RestoreAiResultColumnWidth();
			RevisionDetails.Initialize(repositoryUserControl, RevisionDetailsUserControlMode.AiReview);
			RevisionDetails.Loaded += delegate
			{
				if (target is AiCodeReviewTarget.Branch branch2)
				{
					aiCodeReviewWindow.RevisionDetails.ShowRevisionDetails(new RevisionDiffTarget.Range(branch2.Dst, branch2.Src));
				}
				else if (target is AiCodeReviewTarget.ShaRange shaRange2)
				{
					aiCodeReviewWindow.RevisionDetails.ShowRevisionDetails(new RevisionDiffTarget.Range(shaRange2.Dst, shaRange2.Src));
				}
			};
			RevisionDetails.RevisionDetailsUpdated += delegate(object s, RevisionDetails e)
			{
				aiCodeReviewWindow.RefreshTitle(e);
			};
			GridSplitter.DragCompleted += delegate
			{
				aiCodeReviewWindow.SaveAiResultColumnWidth();
			};
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
		}

		private async Task InitializeWebView()
		{
			await AiResponseWebView.EnsureCoreWebView2Async();
			UpdateWebViewTheme();
			AiResponseWebView.CoreWebView2.ContextMenuRequested += delegate(object s, CoreWebView2ContextMenuRequestedEventArgs e)
			{
				e.Handled = true;
			};
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			UpdateWebViewTheme();
		}

		private void UpdateWebViewTheme()
		{
			if (base.IsLoaded && AiResponseWebView.CoreWebView2 != null)
			{
				AiResponseWebView.CoreWebView2.Profile.PreferredColorScheme = ((ForkPlusSettings.Default.Theme != ThemeType.Dark) ? CoreWebView2PreferredColorScheme.Light : CoreWebView2PreferredColorScheme.Dark);
			}
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode(this))
			{
				return;
			}
			this.SetWindowLocationState(ForkPlusSettings.Default.AiResultWindowLocationState);
		}

		protected override void OnLocationChanged(EventArgs e)
		{
			base.OnLocationChanged(e);
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.AiResultWindowLocationState = this.GetWindowLocationState();
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode(this))
			{
				return;
			}
			AiResponseWebView?.Dispose();
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.AiResultWindowLocationState = this.GetWindowLocationState();
			}
		}

		private void Window_Activated(object sender, EventArgs e)
		{
			if (!_startUpFinished)
			{
				_startUpFinished = true;
			}
		}

		private void RefreshTitle(RevisionDetails revisionDetails)
		{
			revisionDetails.MessageParts(out var subject, out var _);
			base.Title = revisionDetails.Sha.ToAbbreviatedString() + " " + subject;
		}

		private void RestoreAiResultColumnWidth()
		{
			double aiResultColumnWidth = ForkPlusSettings.Default.AiResultColumnWidth;
			AiResultGrid.ColumnDefinitions[2].Width = new GridLength(aiResultColumnWidth, GridUnitType.Pixel);
		}

		private void SaveAiResultColumnWidth()
		{
			double value = AiResultGrid.ColumnDefinitions[2].Width.Value;
			ForkPlusSettings.Default.AiResultColumnWidth = value;
			ForkPlusSettings.Default.Save();
		}

		private void ReviewWithOpenAi(GitModule gitModule, AiCodeReviewTarget target)
		{
			BusyIndicator.Show();
			AiResponseWebView.Collapse();
			AiResponseFallback.Collapse();
			Sha src;
			Sha dst;
			if (target is AiCodeReviewTarget.Branch branch)
			{
				src = branch.Src;
				dst = branch.Dst;
			}
			else
			{
				if (!(target is AiCodeReviewTarget.ShaRange shaRange))
				{
					return;
				}
				src = shaRange.Src;
				dst = shaRange.Dst;
			}
			_repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("AI Code Review"), delegate(JobMonitor monitor)
			{
				GitCommandResult<string> patchResult = new GetRangePatchGitCommand().Execute(gitModule, src, dst);
				if (!patchResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						BusyIndicator.Collapse();
						AiResponseWebView.Collapse();
						AiResponseFallback.Show();
						AiResponseFallback.FallbackTitle = "Error";
						AiResponseFallback.FallbackMessage = "Cannot get diff:\n" + patchResult.Error.FriendlyDescription;
						SendAiReviewCompletedNotification(gitModule, success: false);
					});
				}
				else
				{
					PrivateAccessTokenAuthentication authentication = new PrivateAccessTokenAuthentication("https://api.openai.com", "generic");
					OpenAiService openAiService = new OpenAiService(new Connection("https://api.openai.com", authentication));
					ServiceResult<OpenAiResponse> codeReviewResult = openAiService.CodeReview(patchResult.Result, monitor);
					if (!codeReviewResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							ShowError(codeReviewResult.Error.FriendlyMessage);
							SendAiReviewCompletedNotification(gitModule, success: false);
						});
					}
					else
					{
						GitCommandResult<string> btResult = BtRequest.Run(() => default(BtMdToHtmlResult), delegate(ref BtMdToHtmlResult x)
						{
							return Bt.bt_md_to_html(codeReviewResult.Result.Message, ref x);
						}, delegate(ref BtMdToHtmlResult x)
						{
							return GitCommandResult<string>.Success(x.html.GetUtf8String());
						}, delegate(ref BtMdToHtmlResult x)
						{
							Bt.bt_release_md_to_html(ref x);
						});
						base.Dispatcher.Async(delegate
						{
							if (!btResult.Succeeded)
							{
								ShowError(btResult.Error.FriendlyDescription);
								SendAiReviewCompletedNotification(gitModule, success: false);
							}
							else
							{
								ShowMarkdownOutput(btResult.Result);
								SendAiReviewCompletedNotification(gitModule, success: true);
							}
						});
					}
				}
			});
		}

		private void ReviewWithAiAgent(GitModule gitModule, AiCodeReviewTarget target, AiAgent aiAgent)
		{
			BusyIndicator.Show();
			AiResponseWebView.Collapse();
			AiResponseFallback.Collapse();
			_repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("AI Code Review"), delegate(JobMonitor monitor)
			{
				GitCommandResult<string> codeReviewResult = new MakeCodeReviewShellCommand().Execute(aiAgent, target, gitModule.Path, monitor);
				if (!codeReviewResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						ShowError(codeReviewResult.Error.FriendlyDescription);
						SendAiReviewCompletedNotification(gitModule, success: false);
					});
				}
				else
				{
					GitCommandResult<string> btResult = BtRequest.Run(() => default(BtMdToHtmlResult), delegate(ref BtMdToHtmlResult x)
					{
						return Bt.bt_md_to_html(codeReviewResult.Result, ref x);
					}, delegate(ref BtMdToHtmlResult x)
					{
						return GitCommandResult<string>.Success(x.html.GetUtf8String());
					}, delegate(ref BtMdToHtmlResult x)
					{
						Bt.bt_release_md_to_html(ref x);
					});
					base.Dispatcher.Async(delegate
					{
						if (!btResult.Succeeded)
						{
							ShowError(btResult.Error.FriendlyDescription);
							SendAiReviewCompletedNotification(gitModule, success: false);
						}
						else
						{
							ShowMarkdownOutput(btResult.Result);
							SendAiReviewCompletedNotification(gitModule, success: true);
						}
					});
				}
			});
		}

		private void ShowError(string error)
		{
			BusyIndicator.Collapse();
			AiResponseWebView.Collapse();
			AiResponseFallback.Show();
			AiResponseFallback.FallbackTitle = "Error";
			AiResponseFallback.FallbackMessage = error;
		}

		private void ShowMarkdownOutput(string html)
		{
			BusyIndicator.Collapse();
			AiResponseFallback.Collapse();
			AiResponseWebView.Show();
			string css = GetCss();
			try
			{
				string htmlContent = "<!DOCTYPE html>\n<html>\n    <head>\n        <meta charset='utf-8'>\n        <style>\n            " + css + "\n        </style>\n    </head>\n    <body>\n        " + html + "\n    </body>\n</html>";
				AiResponseWebView.NavigateToString(htmlContent);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to navigate WebView to markdown HTML", ex);
			}
		}

		private static string GetCss()
		{
			if (_cachedCss != null)
			{
				return _cachedCss;
			}
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string name = "ForkPlus.Assets.md-ai-output.css";
				using Stream stream = executingAssembly.GetManifestResourceStream(name);
				using StreamReader streamReader = new StreamReader(stream);
				_cachedCss = streamReader.ReadToEnd();
				return _cachedCss;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read CSS resource", ex);
				return string.Empty;
			}
		}

		private void SendAiReviewCompletedNotification(GitModule gitModule, bool success)
		{
			string text = RepositoryName(gitModule);
			if (text != null && !base.IsActive)
			{
				string arg = WebUtility.HtmlEncode("ai-review:" + base.Title);
				string arg2 = (success ? "AI Code Review Completed" : "AI Code Review Failed");
				string text2 = null;
				if (_target is AiCodeReviewTarget.Branch branch)
				{
					text2 = branch.Name;
				}
				else if (_target is AiCodeReviewTarget.ShaRange { Dst: var dst })
				{
					text2 = dst.ToAbbreviatedString();
				}
				string arg3 = WebUtility.HtmlEncode(text + ": " + text2);
				NotificationManager.SendWindowsNotification($"<?xml version=\"1.0\" encoding =\"utf-8\" ?>\n<toast launch=\"{arg}\" >\n<audio silent=\"true\"/>\n<visual>\n    <binding template=\"ToastGeneric\">\n        <text hint-maxLines=\"1\" >{arg2}</text>\n        <text>{arg3}</text>\n    </binding>\n</visual>\n</toast>\n");
			}
		}

		[Null]
		private static string RepositoryName(GitModule gitModule)
		{
			RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == gitModule.Path);
			if (repository.HasValue)
			{
				RepositoryManager.Repository valueOrDefault = repository.GetValueOrDefault();
				return valueOrDefault.Name();
			}
			return null;
		}

	}
}
