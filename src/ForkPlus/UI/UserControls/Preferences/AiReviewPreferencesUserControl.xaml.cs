using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Jobs;
using ForkPlus.Settings;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class AiReviewPreferencesUserControl : UserControl
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private bool _initialized;

		public AiReviewPreferencesUserControl()
		{
			InitializeComponent();
		}

		public void Initialize()
		{
			ServiceUrlTextBox.Text = ForkPlusSettings.Default.AiReviewServiceUrl;
			ApiKeyTextBox.Text = ForkPlusSettings.Default.AiReviewApiKey;
			AutoFetchModelsCheckBox.IsChecked = ForkPlusSettings.Default.AiReviewAutoFetchModels;
			RetryCountTextBox.Text = ForkPlusSettings.Default.AiReviewRetryCount.ToString();
			TimeoutTextBox.Text = ForkPlusSettings.Default.AiReviewTimeoutSeconds.ToString();
			RefreshModelItems(ForkPlusSettings.Default.AiReviewModels, ForkPlusSettings.Default.AiReviewSelectedModel);
			_initialized = true;
			if (ForkPlusSettings.Default.AiReviewAutoFetchModels && ForkPlusSettings.Default.AiReviewModels.Length == 0 && IsConfigured())
			{
				RefreshModels();
			}
		}

		public void Save()
		{
			SaveCurrentModel();
			ForkPlusSettings.Default.Save();
		}

		private void ServiceUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewServiceUrl = NormalizeUrl(ServiceUrlTextBox.Text);
			if (ForkPlusSettings.Default.AiReviewAutoFetchModels)
			{
				RefreshModels();
			}
		}

		private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewApiKey = ApiKeyTextBox.Text ?? "";
			if (ForkPlusSettings.Default.AiReviewAutoFetchModels)
			{
				RefreshModels();
			}
		}

		private void AutoFetchModelsCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewAutoFetchModels = AutoFetchModelsCheckBox.IsChecked.GetValueOrDefault();
			if (ForkPlusSettings.Default.AiReviewAutoFetchModels)
			{
				RefreshModels();
			}
		}

		private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_initialized)
			{
				SaveCurrentModel();
			}
		}

		private void ModelComboBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				SaveCurrentModel();
			}
		}

		private void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
		{
			RefreshModels();
		}

		private void RetryCountTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			if (!int.TryParse(RetryCountTextBox.Text, out int value))
			{
				value = 3;
			}
			ForkPlusSettings.Default.AiReviewRetryCount = Math.Max(0, value);
		}

		private void TimeoutTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			if (!int.TryParse(TimeoutTextBox.Text, out int value))
			{
				value = 300;
			}
			ForkPlusSettings.Default.AiReviewTimeoutSeconds = Math.Max(10, value);
		}

		private void RefreshModels()
		{
			SaveCurrentModel();
			ForkPlusSettings.Default.AiReviewServiceUrl = NormalizeUrl(ServiceUrlTextBox.Text);
			ForkPlusSettings.Default.AiReviewApiKey = ApiKeyTextBox.Text ?? "";
			if (!IsConfigured())
			{
				SetStatus("Set service URL and API key first.");
				return;
			}
			SetBusy(true);
			SetStatus("Refreshing models...");
			string selectedModel = ForkPlusSettings.Default.AiReviewSelectedModel;
			_jobQueue.Add(PreferencesLocalization.Current("Refresh AI models"), delegate
			{
				var response = OpenAiService.CreateFromAiReviewSettings().ListModels();
				Dispatcher.Async(delegate
				{
					SetBusy(false);
					if (!response.Succeeded)
					{
						SetStatus(response.Error.FriendlyMessage);
						return;
					}
					ForkPlusSettings.Default.AiReviewModels = response.Result;
					if (string.IsNullOrWhiteSpace(selectedModel) && response.Result.Length > 0)
					{
						ForkPlusSettings.Default.AiReviewSelectedModel = response.Result[0];
						selectedModel = response.Result[0];
					}
					RefreshModelItems(response.Result, selectedModel);
					SetStatus(PreferencesLocalization.FormatCurrent("Detected {0} models.", response.Result.Length));
				});
			}, JobFlags.Hidden, showMessageWhenDone: false);
		}

		private void RefreshModelItems(string[] models, string selectedModel)
		{
			ModelComboBox.Items.Clear();
			foreach (string model in models ?? new string[0])
			{
				ModelComboBox.Items.Add(model);
			}
			if (!string.IsNullOrWhiteSpace(selectedModel))
			{
				ModelComboBox.Text = selectedModel;
			}
			else if (ModelComboBox.Items.Count > 0)
			{
				ModelComboBox.SelectedIndex = 0;
			}
		}

		private void SaveCurrentModel()
		{
			ForkPlusSettings.Default.AiReviewSelectedModel = (ModelComboBox.Text ?? "").Trim();
		}

		private bool IsConfigured()
		{
			return !string.IsNullOrWhiteSpace(ServiceUrlTextBox.Text) && !string.IsNullOrWhiteSpace(ApiKeyTextBox.Text);
		}

		private void SetBusy(bool busy)
		{
			BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
			RefreshModelsButton.IsEnabled = !busy;
		}

		private void SetStatus(string text)
		{
			StatusTextBlock.Text = PreferencesLocalization.Current(text);
		}

		private static string NormalizeUrl(string url)
		{
			string normalized = (url ?? "").Trim().TrimEnd('/');
			if (normalized.EndsWith("/v1/models", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(0, normalized.Length - "/v1/models".Length);
			}
			if (normalized.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(0, normalized.Length - "/v1/chat/completions".Length);
			}
			if (normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(0, normalized.Length - "/v1".Length);
			}
			return normalized;
		}
	}
}
