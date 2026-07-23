using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.UserControls
{
	/// <summary>
	/// 阶段 3：MergeConflictUserControl 的 ViewModel。
	/// 承载纯逻辑/纯状态（零 WPF 依赖）。
	/// View 保留 MessageBox 弹框、UI 事件处理器壳与子控件属性赋值，通过本 VM 转发纯计算。
	/// 注意：OpenAiService 为 internal 类，本 VM 的 public 方法只返回纯数据结果（AiResolveResult），
	/// 不在 public 签名中暴露 OpenAiService / ServiceResult&lt;OpenAiResponse&gt; 等 internal 类型。
	/// </summary>
	public class MergeConflictUserControlViewModel
	{
		private bool _aiResolving;

		/// <summary>是否正在执行 AI 解决冲突流程（防止重入的纯状态）。</summary>
		public bool IsAiResolving => _aiResolving;

		/// <summary>
		/// 取 GitPoint 的 Sha（Reference 取其 Sha，否则返回 null）。纯静态。
		/// 原 View 第 103-111 行。
		/// </summary>
		[Null]
		public static string GetSha(IGitPoint gitPoint)
		{
			if (gitPoint is ForkPlus.Git.Reference { Sha: var sha })
			{
				return sha.ToString();
			}
			return null;
		}

		/// <summary>
		/// 是否允许合并（两侧均未删除）。纯静态。
		/// 原 View 第 444-451 行。
		/// </summary>
		public static bool IsMergeAllowed(ChangedFile unmergedFile)
		{
			if (unmergedFile.Status != StatusType.Deleted)
			{
				return unmergedFile.WorkingDirectoryStatus != StatusType.Deleted;
			}
			return false;
		}

		/// <summary>
		/// 计算 ResolveButton 的纯状态（label/可用性/外部合并工具显隐），不触碰任何 UI 控件。
		/// 原 View UpdateResolveButton 第 393-442 行的决策逻辑。
		/// View 调用后据此赋值给 ResolveButton/ResolveInExternalMergerButton 等控件。
		/// </summary>
		public static ResolveButtonState ComputeResolveButtonState(
			bool localChecked,
			bool remoteChecked,
			bool isMergeAllowed,
			StatusType localStatus,
			StatusType remoteStatus,
			[Null] string localFriendlyName,
			[Null] string remoteFriendlyName)
		{
			if (localChecked && remoteChecked)
			{
				if (isMergeAllowed)
				{
					System.Collections.Generic.List<ExternalTool> tools = ExternalToolManager.RevealAvailableMergeTools()
						.Filter((ExternalTool x) => x.IsVisible);
					if (tools.Count > 0)
					{
						ExternalTool primary = tools.FirstItemStruct((ExternalTool x) => x.IsPrimary) ?? tools[0];
						return new ResolveButtonState
						{
							Label = PreferencesLocalization.Current("Merge"),
							CanResolve = true,
							ShowExternalMerger = true,
							ExternalMergerLabel = PreferencesLocalization.FormatCurrent("Merge in {0}", primary.Name),
							ShowDropdown = tools.Count > 1
						};
					}
					return new ResolveButtonState
					{
						Label = PreferencesLocalization.Current("Merge"),
						CanResolve = true,
						ShowExternalMerger = false
					};
				}
				return new ResolveButtonState
				{
					Label = PreferencesLocalization.Current("Select version to resolve with"),
					CanResolve = false,
					ShowExternalMerger = false
				};
			}
			if (localChecked)
			{
				return new ResolveButtonState
				{
					Label = (localStatus == StatusType.Deleted)
						? PreferencesLocalization.Current("Delete file")
						: PreferencesLocalization.FormatCurrent("Choose {0}", localFriendlyName ?? ""),
					CanResolve = true,
					ShowExternalMerger = false
				};
			}
			if (remoteChecked)
			{
				return new ResolveButtonState
				{
					Label = (remoteStatus == StatusType.Deleted)
						? PreferencesLocalization.Current("Delete file")
						: PreferencesLocalization.FormatCurrent("Choose {0}", remoteFriendlyName ?? ""),
					CanResolve = true,
					ShowExternalMerger = false
				};
			}
			return new ResolveButtonState
			{
				Label = PreferencesLocalization.Current("Merge"),
				CanResolve = false,
				ShowExternalMerger = false
			};
		}

		/// <summary>
		/// 将 Revision[] 映射为 MergeRevisionViewModel[]（纯数据映射）。
		/// 原 View UpdateRevisionsListBox 第 115 行的 revisions.Map(...) 部分。
		/// </summary>
		public static MergeRevisionViewModel[] MapRevisionsToViewModels(Revision[] revisions)
		{
			return revisions.Map((Revision x) => new MergeRevisionViewModel(x));
		}

		/// <summary>
		/// AI 解决当前文件冲突的核心业务逻辑：读取磁盘上带冲突标记的文件，发送给 AI 流式请求，
		/// 校验输出后返回纯数据结果（AiResolveResult）。不弹任何 MessageBox、不触碰 UI 控件。
		/// 原 View AiResolveButton_Click 第 148-294 行（校验 + 流式 + 校验输出）。
		/// View 据此结果的 Status 弹对应的 MessageBox；Success 时由 View 弹确认框后再调用
		/// <see cref="ApplyResolvedContent"/> 写回。
		/// </summary>
		public async Task<AiResolveResult> TryResolveWithAiAsync(GitModule gitModule, ChangedFile changedFile)
		{
			if (_aiResolving)
			{
				return AiResolveResult.Create(AiResolveStatus.AlreadyResolving);
			}
			if (gitModule == null || changedFile == null)
			{
				return AiResolveResult.Create(AiResolveStatus.InvalidState);
			}
			if (!OpenAiService.IsAiReviewConfigured())
			{
				return AiResolveResult.Create(AiResolveStatus.NotConfigured,
					PreferencesLocalization.Current("AI is not configured. Please configure AI review settings in Preferences first."));
			}
			string filePath;
			try
			{
				filePath = gitModule.MakePath(changedFile.Path);
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve: failed to resolve file path: " + ex.Message);
				return AiResolveResult.Create(AiResolveStatus.PathResolutionFailed);
			}
			string conflictedContent;
			try
			{
				conflictedContent = File.ReadAllText(filePath);
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve: failed to read conflict file: " + ex.Message);
				return AiResolveResult.Create(AiResolveStatus.ReadFailed,
					PreferencesLocalization.FormatCurrent("Failed to read conflict file: {0}", ex.Message));
			}
			if (string.IsNullOrEmpty(conflictedContent)
				|| !conflictedContent.Contains("<<<<<<<") || !conflictedContent.Contains(">>>>>>>"))
			{
				return AiResolveResult.Create(AiResolveStatus.NoConflictMarkers,
					PreferencesLocalization.Current("No conflict markers found in the file."));
			}

			_aiResolving = true;
			try
			{
				string fileName = Path.GetFileName(changedFile.Path);
				string prompt = OpenAiService.BuildResolveConflictsPrompt(fileName, conflictedContent);

				StringBuilder responseBuilder = new StringBuilder();
				Exception requestError = null;
				bool canceled = false;

				await Task.Run(delegate
				{
					try
					{
						OpenAiService aiService = OpenAiService.CreateFromAiReviewSettings();
						JobMonitor monitor = new JobMonitor();
						ServiceResult<OpenAiResponse> result = aiService.OpenAiRequestStreamingWithRetry(
							prompt,
							monitor,
							delegate(string delta)
							{
								if (string.IsNullOrEmpty(delta))
								{
									return;
								}
								lock (responseBuilder)
								{
									responseBuilder.Append(delta);
								}
							});
						if (monitor.IsCanceled)
						{
							canceled = true;
							return;
						}
						if (!result.Succeeded)
						{
							requestError = new Exception(result.Error?.FriendlyMessage ?? "Unknown error");
						}
					}
					catch (Exception ex)
					{
						requestError = ex;
					}
				}).ConfigureAwait(true);

				if (canceled)
				{
					return AiResolveResult.Create(AiResolveStatus.Canceled);
				}
				if (requestError != null)
				{
					Log.Error("AI Resolve failed: " + requestError.Message);
					return AiResolveResult.Create(AiResolveStatus.RequestFailed,
						PreferencesLocalization.FormatCurrent("AI resolve failed: {0}", requestError.Message));
				}

				string resolved;
				lock (responseBuilder)
				{
					resolved = responseBuilder.ToString();
				}
				resolved = OpenAiService.StripCodeFences(resolved);
				if (string.IsNullOrWhiteSpace(resolved))
				{
					return AiResolveResult.Create(AiResolveStatus.EmptyContent,
						PreferencesLocalization.Current("AI returned empty content. Aborting."));
				}
				if (resolved.Contains("<<<<<<<") || resolved.Contains(">>>>>>>") || resolved.Contains("======="))
				{
					return AiResolveResult.Create(AiResolveStatus.ConflictMarkersRemain,
						PreferencesLocalization.Current("AI output still contains conflict markers. Please review and try again, or resolve manually."));
				}

				return AiResolveResult.Success(
					PreferencesLocalization.Current("AI resolved all conflicts. Apply the resolved content?"),
					resolved);
			}
			finally
			{
				_aiResolving = false;
			}
		}

		/// <summary>
		/// 将 AI 解决后的内容写回磁盘并 stage（纯 git 命令逻辑）。
		/// 原 View AiResolveButton_Click 第 306-326 行的写回部分。
		/// View 在用户确认后调用，据返回的 Status 执行 InvalidateAndRefresh / ErrorWindow / MessageBox。
		/// </summary>
		public AiResolveResult ApplyResolvedContent(GitModule gitModule, ChangedFile changedFile, string resolvedContent)
		{
			try
			{
				GitCommandResult gitResult = new ResolveMergeConflictGitCommand().Execute(gitModule, changedFile, resolvedContent);
				if (!gitResult.Succeeded)
				{
					return AiResolveResult.GitFailed(gitResult.Error);
				}
				return AiResolveResult.Applied();
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve: failed to write back: " + ex.Message);
				return AiResolveResult.Create(AiResolveStatus.WriteFailed,
					PreferencesLocalization.FormatCurrent("Failed to apply resolved content: {0}", ex.Message));
			}
		}
	}

	/// <summary>
	/// AI 解决冲突流程的状态枚举。View 据此 switch 决定弹哪种 MessageBox / 执行何种 UI 副作用。
	/// </summary>
	public enum AiResolveStatus
	{
		/// <summary>已有一次 AI 解决在进行中（重入保护，静默返回）。</summary>
		AlreadyResolving,
		/// <summary>gitModule 或 changedFile 为 null（静默返回）。</summary>
		InvalidState,
		/// <summary>MakePath 抛异常（仅记日志，静默返回）。</summary>
		PathResolutionFailed,
		/// <summary>流式请求被用户取消（静默返回）。</summary>
		Canceled,
		/// <summary>AI 未配置（Warning MessageBox）。</summary>
		NotConfigured,
		/// <summary>读取冲突文件失败（Error MessageBox，含 Message）。</summary>
		ReadFailed,
		/// <summary>文件中无冲突标记（Information MessageBox）。</summary>
		NoConflictMarkers,
		/// <summary>AI 流式请求失败（Error MessageBox，含 Message）。</summary>
		RequestFailed,
		/// <summary>AI 返回空内容（Warning MessageBox）。</summary>
		EmptyContent,
		/// <summary>AI 输出仍含冲突标记（Warning MessageBox）。</summary>
		ConflictMarkersRemain,
		/// <summary>AI 解决成功（View 弹 YesNo 确认框，确认后写回）。</summary>
		Success,
		/// <summary>写回成功（View 执行 InvalidateAndRefresh）。</summary>
		Applied,
		/// <summary>git 命令返回失败（View 弹 ErrorWindow，含 GitError）。</summary>
		GitFailed,
		/// <summary>写回过程抛异常（Error MessageBox，含 Message）。</summary>
		WriteFailed
	}

	/// <summary>
	/// AI 解决冲突的纯数据结果。VM 的 public 方法只返回此类型，不暴露 internal 的 OpenAiService。
	/// </summary>
	public class AiResolveResult
	{
		public AiResolveStatus Status { get; }

		/// <summary>供 MessageBox 显示的本地化消息（静默状态为 null）。</summary>
		[Null]
		public string Message { get; }

		/// <summary>AI 解决后的文件内容（仅 Success 状态有值）。</summary>
		[Null]
		public string ResolvedContent { get; }

		/// <summary>git 命令失败时的错误对象（仅 GitFailed 状态有值，供 ErrorWindow 使用）。</summary>
		[Null]
		public GitCommandError GitError { get; }

		private AiResolveResult(AiResolveStatus status, [Null] string message, [Null] string resolvedContent, [Null] GitCommandError gitError)
		{
			Status = status;
			Message = message;
			ResolvedContent = resolvedContent;
			GitError = gitError;
		}

		public static AiResolveResult Create(AiResolveStatus status)
		{
			return new AiResolveResult(status, null, null, null);
		}

		public static AiResolveResult Create(AiResolveStatus status, string message)
		{
			return new AiResolveResult(status, message, null, null);
		}

		public static AiResolveResult Success(string message, string resolvedContent)
		{
			return new AiResolveResult(AiResolveStatus.Success, message, resolvedContent, null);
		}

		public static AiResolveResult Applied()
		{
			return new AiResolveResult(AiResolveStatus.Applied, null, null, null);
		}

		public static AiResolveResult GitFailed(GitCommandError error)
		{
			return new AiResolveResult(AiResolveStatus.GitFailed, null, null, error);
		}
	}

	/// <summary>
	/// ResolveButton 的纯计算状态。View 据此赋值给 ResolveButton / ResolveInExternalMergerButton 等控件。
	/// </summary>
	public struct ResolveButtonState
	{
		/// <summary>ResolveButton.Content 的本地化文本。</summary>
		[Null]
		public string Label;

		/// <summary>ResolveButton 是否可用（Enable/Disable）。</summary>
		public bool CanResolve;

		/// <summary>是否显示 ResolveInExternalMergerButton。</summary>
		public bool ShowExternalMerger;

		/// <summary>ResolveInExternalMergerButton.Content 的本地化文本（ShowExternalMerger 为 false 时为 null）。</summary>
		[Null]
		public string ExternalMergerLabel;

		/// <summary>是否显示 ResolveInExternalMergerDropdownButton（仅 ShowExternalMerger 为 true 时有意义）。</summary>
		public bool ShowDropdown;
	}
}
