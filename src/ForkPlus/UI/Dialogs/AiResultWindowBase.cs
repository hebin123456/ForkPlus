using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// v3.9.0：AI 结果窗口基类。封装 ModelComboBox 初始化/切换等共享逻辑。
	/// AiTextResultWindow / AiCodeReviewWindow / AiCommitComposerWindow 继承此类。
	/// CSS 读取和 Markdown→HTML 转换委托给 AiStreamingWebView 静态方法，与 AiDevelopmentWindow 共用。
	/// </summary>
	public abstract class AiResultWindowBase : CustomWindow
	{
		/// <summary>模型列表是否已完成后台加载（防止重复填充）。</summary>
		protected bool _modelListLoaded;

		// ── 子类通过 XAML 生成的字段实现这些属性 ──

		protected abstract ComboBox AiModelComboBox { get; }
		protected abstract Button AiStopButton { get; }
		protected abstract Button AiRetryButton { get; }
		protected abstract TextBlock AiStatusTextBlock { get; }
		protected abstract ProgressBar AiStatusProgressBar { get; }

		// ── ModelComboBox 共享逻辑（原 3 份几乎完全相同的代码） ──

		/// <summary>初始化模型下拉框。先用当前选中模型占位，再后台拉取完整列表。</summary>
		protected void InitializeModelComboBox()
		{
			string currentModel = ForkPlusSettings.Default.AiReviewSelectedModel;
			if (!string.IsNullOrWhiteSpace(currentModel))
			{
				AiModelComboBox.Items.Add(currentModel);
				AiModelComboBox.SelectedIndex = 0;
			}
			else
			{
				AiModelComboBox.Items.Add(PreferencesLocalization.Current("Select model..."));
				AiModelComboBox.SelectedIndex = 0;
			}

			System.Threading.ThreadPool.QueueUserWorkItem(delegate(object state)
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
					return;
				}

				base.Dispatcher.Post(delegate
				{
					try
					{
						if (_modelListLoaded)
						{
							return;
						}
						_modelListLoaded = true;
						string selected = ForkPlusSettings.Default.AiReviewSelectedModel;
						AiModelComboBox.Items.Clear();
						foreach (string m in models)
						{
							if (!string.IsNullOrWhiteSpace(m))
							{
								AiModelComboBox.Items.Add(m);
							}
						}
						int idx = -1;
						for (int i = 0; i < AiModelComboBox.Items.Count; i++)
						{
							if (string.Equals((string)AiModelComboBox.Items[i], selected, StringComparison.OrdinalIgnoreCase))
							{
								idx = i;
								break;
							}
						}
						if (idx >= 0)
						{
							AiModelComboBox.SelectedIndex = idx;
						}
						else if (!string.IsNullOrWhiteSpace(selected))
						{
							AiModelComboBox.Items.Insert(0, selected);
							AiModelComboBox.SelectedIndex = 0;
						}
						else if (AiModelComboBox.Items.Count > 0)
						{
							AiModelComboBox.SelectedIndex = 0;
						}
					}
					catch (Exception ex)
					{
						Log.Warn("Failed to populate model combo box: " + ex.Message);
					}
				});
			});
		}

		/// <summary>切换模型时保存到设置。子类可 override OnModelChanged 做额外反馈。</summary>
		protected void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (AiModelComboBox.SelectedItem == null)
			{
				return;
			}
			string selected = (string)AiModelComboBox.SelectedItem;
			if (string.IsNullOrWhiteSpace(selected) || selected == PreferencesLocalization.Current("Select model..."))
			{
				return;
			}
			if (string.Equals(selected, ForkPlusSettings.Default.AiReviewSelectedModel, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewSelectedModel = selected;
			ForkPlusSettings.Default.Save();
			OnModelChanged(selected);
		}

		/// <summary>模型切换后的回调，子类可 override 做额外反馈（如更新状态栏文字）。</summary>
		protected virtual void OnModelChanged(string selected)
		{
		}

		// ── CSS / Markdown 共享逻辑（委托给 AiStreamingWebView 静态方法，v3.9.0 统一收口） ──

		/// <summary>读取 md-ai-output.css 嵌入资源（委托 AiStreamingWebView.GetCss）。</summary>
		protected static string GetCss()
		{
			return AiStreamingWebView.GetCss();
		}

		/// <summary>Markdown → HTML（委托 AiStreamingWebView.ConvertMarkdownToHtml）。</summary>
		protected static GitCommandResult<string> ConvertMarkdownToHtml(string markdown)
		{
			return AiStreamingWebView.ConvertMarkdownToHtml(markdown);
		}
	}
}
