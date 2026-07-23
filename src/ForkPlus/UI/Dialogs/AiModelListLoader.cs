using System;
using System.Collections.Generic;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Settings;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// AI 模型下拉列表加载助手（零 WPF 依赖）。
	/// 承载 AiTextResult/AiCodeReview/AiDevelopment 三窗口共有的"后台拉取 /v1/models 列表"纯逻辑。
	/// View 负责 ComboBox.Items 填充 + Dispatcher 调度（后台线程调用 LoadModels，UI 线程回填）。
	/// </summary>
	public static class AiModelListLoader
	{
		/// <summary>当前选中的模型（读写 ForkPlusSettings.Default.AiReviewSelectedModel，写时持久化）。</summary>
		public static string CurrentModel
		{
			get => ForkPlusSettings.Default.AiReviewSelectedModel;
			set
			{
				if (!string.Equals(value, ForkPlusSettings.Default.AiReviewSelectedModel, StringComparison.OrdinalIgnoreCase))
				{
					ForkPlusSettings.Default.AiReviewSelectedModel = value;
					ForkPlusSettings.Default.Save();
				}
			}
		}

		/// <summary>当前是否已配置 AI（OpenAiService.IsAiReviewConfigured 的转发，便于 View 减少依赖）。</summary>
		public static bool IsConfigured => OpenAiService.IsAiReviewConfigured();

		/// <summary>
		/// 后台线程调用：拉取完整模型列表。成功返回已过滤空白项的非空列表；未配置/失败/空返回 null。
		/// View 应在 ThreadPool.QueueUserWorkItem 中调用本方法，拿到结果后用 Dispatcher.Async 回填 ComboBox。
		/// </summary>
		public static List<string> LoadModels()
		{
			List<string> models = null;
			try
			{
				if (OpenAiService.IsAiReviewConfigured())
				{
					OpenAiService aiService = OpenAiService.CreateFromAiReviewSettings();
					ServiceResult<string[]> result = aiService.ListModels();
					if (result.Succeeded && result.Result != null)
					{
						models = new List<string>(result.Result);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to load AI model list: " + ex.Message);
			}
			if (models == null || models.Count == 0)
			{
				return null;
			}
			// 过滤空白项
			List<string> filtered = new List<string>(models.Count);
			foreach (string m in models)
			{
				if (!string.IsNullOrWhiteSpace(m))
				{
					filtered.Add(m);
				}
			}
			return filtered.Count > 0 ? filtered : null;
		}

		/// <summary>
		/// 在已填充的列表中定位应选中的索引（匹配当前设置）。
		/// 返回 (selectedIndex, shouldInsertCurrent)：shouldInsertCurrent=true 表示当前模型不在列表中，
		/// View 应在索引 0 插入它并选中（保持用户自定义模型可见）。
		/// </summary>
		public static (int SelectedIndex, bool ShouldInsertCurrent) FindSelectedIndex(IReadOnlyList<string> models, string currentModel)
		{
			if (string.IsNullOrWhiteSpace(currentModel))
			{
				return (models.Count > 0 ? 0 : -1, false);
			}
			for (int i = 0; i < models.Count; i++)
			{
				if (string.Equals(models[i], currentModel, StringComparison.OrdinalIgnoreCase))
				{
					return (i, false);
				}
			}
			return (-1, true);
		}
	}
}
