using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// AI 窗口右上角模型下拉框的统一逻辑（v3.10.3 收口）。
	/// 原先 AiResultWindowBase 与 AiDevelopmentWindow 各持有一份几乎相同的实现，行为漂移；
	/// 现两处共用：
	/// 1. <see cref="Initialize"/>：先用当前选中模型占位，后台拉取 /v1/models 完整列表后替换填充；
	/// 2. <see cref="HandleSelectionChanged"/>：切换模型时保存设置并回调（窗口自行做状态反馈）。
	/// </summary>
	internal static class AiModelComboBoxHelper
	{
		/// <summary>每个下拉框只允许后台列表替换填充一次（防止 Loaded 重复触发时覆盖用户选择）。</summary>
		private static readonly ConditionalWeakTable<ComboBox, StrongBox<bool>> ModelListLoaded = new();

		/// <summary>初始化模型下拉框。先用当前选中模型占位，再后台拉取完整列表。</summary>
		public static void Initialize(ComboBox comboBox, Dispatcher dispatcher)
		{
			string currentModel = ForkPlusSettings.Default.AiReviewSelectedModel;
			if (!string.IsNullOrWhiteSpace(currentModel))
			{
				comboBox.Items.Add(currentModel);
				comboBox.SelectedIndex = 0;
			}
			else
			{
				comboBox.Items.Add(PreferencesLocalization.Current("Select model..."));
				comboBox.SelectedIndex = 0;
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

				dispatcher.Post(delegate
				{
					try
					{
						if (ModelListLoaded.TryGetValue(comboBox, out StrongBox<bool> loaded) && loaded.Value)
						{
							return;
						}
						ModelListLoaded.Remove(comboBox);
						ModelListLoaded.Add(comboBox, new StrongBox<bool>(true));
						string selected = ForkPlusSettings.Default.AiReviewSelectedModel;
						comboBox.Items.Clear();
						foreach (string m in models)
						{
							if (!string.IsNullOrWhiteSpace(m))
							{
								comboBox.Items.Add(m);
							}
						}
						int idx = -1;
						for (int i = 0; i < comboBox.Items.Count; i++)
						{
							if (string.Equals((string)comboBox.Items[i], selected, StringComparison.OrdinalIgnoreCase))
							{
								idx = i;
								break;
							}
						}
						if (idx >= 0)
						{
							comboBox.SelectedIndex = idx;
						}
						else if (!string.IsNullOrWhiteSpace(selected))
						{
							comboBox.Items.Insert(0, selected);
							comboBox.SelectedIndex = 0;
						}
						else if (comboBox.Items.Count > 0)
						{
							comboBox.SelectedIndex = 0;
						}
					}
					catch (Exception ex)
					{
						Log.Warn("Failed to populate model combo box: " + ex.Message);
					}
				});
			});
		}

		/// <summary>切换模型时保存到设置，并回调 onModelChanged 做窗口内反馈（如更新状态栏文字）。</summary>
		public static void HandleSelectionChanged(ComboBox comboBox, Action<string> onModelChanged)
		{
			if (comboBox.SelectedItem == null)
			{
				return;
			}
			string selected = (string)comboBox.SelectedItem;
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
			onModelChanged?.Invoke(selected);
		}
	}
}
