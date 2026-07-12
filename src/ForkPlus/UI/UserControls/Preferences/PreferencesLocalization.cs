using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Text.RegularExpressions;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using Newtonsoft.Json.Linq;

namespace ForkPlus.UI.UserControls.Preferences
{
	internal static class PreferencesLocalization
	{
		public const string English = "en";
		public const string SimplifiedChinese = "zh-Hans";
		public const string TraditionalChinese = "zh-Hant";
		public const string Japanese = "ja-JP";
		public const string Korean = "ko-KR";
		public const string French = "fr-FR";
		public const string German = "de-DE";

		private const string LanguagesDirectoryName = "Languages";

		private static readonly DependencyProperty OriginalTextProperty = DependencyProperty.RegisterAttached("OriginalText", typeof(string), typeof(PreferencesLocalization));

		private static readonly DependencyProperty OriginalHeaderProperty = DependencyProperty.RegisterAttached("OriginalHeader", typeof(string), typeof(PreferencesLocalization));

		private static readonly DependencyProperty OriginalContentProperty = DependencyProperty.RegisterAttached("OriginalContent", typeof(string), typeof(PreferencesLocalization));

		private static readonly DependencyProperty OriginalPlaceholderProperty = DependencyProperty.RegisterAttached("OriginalPlaceholder", typeof(string), typeof(PreferencesLocalization));

		private static readonly DependencyProperty OriginalToolTipProperty = DependencyProperty.RegisterAttached("OriginalToolTip", typeof(string), typeof(PreferencesLocalization));

		private static readonly DependencyProperty OriginalTitleProperty = DependencyProperty.RegisterAttached("OriginalTitle", typeof(string), typeof(PreferencesLocalization));

		private static readonly Dictionary<string, string> BuiltInLanguageNames = new Dictionary<string, string>
		{
			{ English, "English" },
			{ SimplifiedChinese, "简体中文" },
			{ TraditionalChinese, "繁體中文" },
			{ Japanese, "日本語" },
			{ Korean, "한국어" },
			{ French, "Français" },
			{ German, "Deutsch" }
		};

		private static readonly Dictionary<string, LoadedLanguage> ExternalLanguages = LoadExternalLanguages();

		private static readonly Dictionary<string, Dictionary<string, string>> ExternalDictionaries = ExternalLanguages.ToDictionary((KeyValuePair<string, LoadedLanguage> item) => item.Key, (KeyValuePair<string, LoadedLanguage> item) => item.Value.Translations);

		private static readonly Dictionary<string, string> ExternalLanguageNames = ExternalLanguages
			.Where((KeyValuePair<string, LoadedLanguage> item) => !string.IsNullOrWhiteSpace(item.Value.Name))
			.ToDictionary((KeyValuePair<string, LoadedLanguage> item) => item.Key, (KeyValuePair<string, LoadedLanguage> item) => item.Value.Name);

		private static readonly object TranslationCacheLock = new object();

		private static readonly Dictionary<string, string> TranslationCache = new Dictionary<string, string>();

		private const int TranslationCacheMaxSize = 4096;

		private sealed class LoadedLanguage
		{
			public string Name { get; }

			public Dictionary<string, string> Translations { get; }

			public LoadedLanguage(string name, Dictionary<string, string> translations)
			{
				Name = name;
				Translations = translations;
			}
		}

		public sealed class LanguageOption
		{
			public string Code { get; }

			public string DisplayName { get; }

			public LanguageOption(string code, string displayName)
			{
				Code = code;
				DisplayName = displayName;
			}
		}

		public static void Apply(DependencyObject root, string language)
		{
			Dictionary<string, string> dictionary = GetDictionary(language);
			ApplyRecursive(root, dictionary, new HashSet<DependencyObject>());
		}

		public static void ApplyCurrent(DependencyObject root)
		{
			Apply(root, ForkPlusSettings.Default.UiLanguage);
		}

		public static string Translate(string text, string language)
		{
			return Translate(text, GetDictionary(language));
		}

		public static string Current(string text)
		{
			return Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		public static string FormatCurrent(string text, params object[] args)
		{
			return string.Format(Current(text), args);
		}

		public static string MenuHeader(string text)
		{
			return Current(text).Replace("_", "__");
		}

		public static string FormatMenuHeader(string text, params object[] args)
		{
			return FormatCurrent(text, args).Replace("_", "__");
		}

		public static void ApplyElement(DependencyObject element, string language)
		{
			ApplyElementCore(element, GetDictionary(language));
		}

		private static Dictionary<string, string> GetDictionary(string language)
		{
			if (ExternalDictionaries.TryGetValue(language ?? "", out var externalDictionary))
			{
				return externalDictionary;
			}
			return null;
		}

		public static LanguageOption[] GetLanguages()
		{
			Dictionary<string, string> languages = new Dictionary<string, string>(BuiltInLanguageNames);
			foreach (KeyValuePair<string, string> item in ExternalLanguageNames)
			{
				languages[item.Key] = item.Value;
			}
			foreach (string language in ExternalDictionaries.Keys)
			{
				if (!languages.ContainsKey(language))
				{
					languages[language] = language;
				}
			}
			return languages
				.Select((KeyValuePair<string, string> item) => new LanguageOption(item.Key, item.Value))
				.OrderBy((LanguageOption option) => LanguageSortOrder(option.Code))
				.ThenBy((LanguageOption option) => option.DisplayName)
				.ToArray();
		}

		private static int LanguageSortOrder(string language)
		{
			if (language == English)
			{
				return 0;
			}
			if (language == SimplifiedChinese)
			{
				return 1;
			}
			if (language == TraditionalChinese)
			{
				return 2;
			}
			if (language == Japanese)
			{
				return 3;
			}
			if (language == Korean)
			{
				return 4;
			}
			if (language == French)
			{
				return 5;
			}
			if (language == German)
			{
				return 6;
			}
			return 10;
		}

		private static Dictionary<string, LoadedLanguage> LoadExternalLanguages()
		{
			Dictionary<string, LoadedLanguage> result = new Dictionary<string, LoadedLanguage>();
			foreach (string filePath in LanguageFilePaths())
			{
				try
				{
					JObject json = JObject.Parse(File.ReadAllText(filePath));
					string code = json["code"]?.Value<string>() ?? Path.GetFileNameWithoutExtension(filePath);
					string name = json["name"]?.Value<string>();
					Dictionary<string, string> translations = DecodeTranslations(json);
					if (!string.IsNullOrWhiteSpace(code) && (translations.Count > 0 || !string.IsNullOrWhiteSpace(name)))
					{
						result[code] = new LoadedLanguage(name, translations);
					}
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to load language file '" + filePath + "'", ex);
				}
			}
			return result;
		}

		private static IEnumerable<string> LanguageFilePaths()
		{
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string directory in LanguageDirectories())
			{
				if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
				{
					continue;
				}
				foreach (string filePath in Directory.GetFiles(directory, "*.json"))
				{
					if (seen.Add(filePath))
					{
						yield return filePath;
					}
				}
			}
		}

		private static IEnumerable<string> LanguageDirectories()
		{
			yield return Path.Combine(AppContext.BaseDirectory, LanguagesDirectoryName);
			yield return Path.Combine(App.ForkDataDirectoryPath, LanguagesDirectoryName);
			yield return Path.Combine(PathHelper.GetParent(typeof(PreferencesLocalization).Assembly.Location), LanguagesDirectoryName);
		}

		private static Dictionary<string, string> DecodeTranslations(JObject json)
		{
			Dictionary<string, string> result = new Dictionary<string, string>();
			JObject translations = json["translations"] as JObject ?? json;
			foreach (JProperty property in translations.Properties())
			{
				if (property.Name == "code" || property.Name == "name" || property.Name == "translations")
				{
					continue;
				}
				string value = property.Value?.Value<string>();
				if (value != null)
				{
					result[property.Name] = value;
				}
			}
			return result;
		}

		private static void ApplyRecursive(DependencyObject element, Dictionary<string, string> dictionary, HashSet<DependencyObject> visited)
		{
			if (element == null || visited.Contains(element))
			{
				return;
			}
			visited.Add(element);
			ApplyElementCore(element, dictionary);
			IEnumerable logicalChildren;
			try
			{
				logicalChildren = LogicalTreeHelper.GetChildren(element);
			}
			catch
			{
				return;
			}
			foreach (object child in logicalChildren)
			{
				if (child is DependencyObject dependencyObject)
				{
					ApplyRecursive(dependencyObject, dictionary, visited);
				}
			}
			if (element is FrameworkElement { ContextMenu: ContextMenu contextMenu })
			{
				ApplyRecursive(contextMenu, dictionary, visited);
			}
		}

		private static void ApplyElementCore(DependencyObject element, Dictionary<string, string> dictionary)
		{
			if (element is TextBlock textBlock)
			{
				if (!HasBinding(element, TextBlock.TextProperty))
				{
					string original = GetOriginal(element, OriginalTextProperty, textBlock.Text);
					textBlock.Text = Translate(original, dictionary);
				}
			}
			if (element is HeaderedContentControl headeredContentControl && headeredContentControl.Header is string header && !HasBinding(element, HeaderedContentControl.HeaderProperty))
			{
				string original = GetOriginal(element, OriginalHeaderProperty, header);
				headeredContentControl.Header = Translate(original, dictionary);
			}
			if (element is HeaderedItemsControl headeredItemsControl && headeredItemsControl.Header is string itemsHeader && !HasBinding(element, HeaderedItemsControl.HeaderProperty))
			{
				string original = GetOriginal(element, OriginalHeaderProperty, itemsHeader);
				headeredItemsControl.Header = Translate(original, dictionary);
			}
			if (element is ContentControl contentControl && contentControl.Content is string content && !HasBinding(element, ContentControl.ContentProperty))
			{
				string original = GetOriginal(element, OriginalContentProperty, content);
				contentControl.Content = Translate(original, dictionary);
			}
			if (element is Window window && !HasBinding(element, Window.TitleProperty))
			{
				string original = GetOriginal(element, OriginalTextProperty, window.Title);
				window.Title = Translate(original, dictionary);
			}
			if (element is PlaceholderTextBox placeholderTextBox)
			{
				if (!HasBinding(element, PlaceholderTextBox.PlaceholderProperty))
				{
					string original = GetOriginal(element, OriginalPlaceholderProperty, placeholderTextBox.Placeholder);
					placeholderTextBox.Placeholder = Translate(original, dictionary);
				}
			}
			if (element is FrameworkElement frameworkElement && frameworkElement.ToolTip is string toolTip && !HasBinding(element, FrameworkElement.ToolTipProperty))
			{
				string original = GetOriginal(element, OriginalToolTipProperty, toolTip);
				frameworkElement.ToolTip = Translate(original, dictionary);
			}
			if (element is ToolbarButton toolbarButton)
			{
				string original = GetOriginal(element, OriginalTitleProperty, toolbarButton.Title);
				toolbarButton.Title = Translate(original, dictionary);
			}
			if (element is ToolbarDropDownButton toolbarDropDownButton)
			{
				string original = GetOriginal(element, OriginalTitleProperty, toolbarDropDownButton.Title);
				toolbarDropDownButton.Title = Translate(original, dictionary);
			}
		}

		private static bool HasBinding(DependencyObject element, DependencyProperty property)
		{
			return BindingOperations.GetBindingExpressionBase(element, property) != null;
		}

		private static string GetOriginal(DependencyObject element, DependencyProperty property, string current)
		{
			string original = element.GetValue(property) as string;
			if (original == null)
			{
				original = current;
				element.SetValue(property, original);
			}
			return original;
		}

		private static string Translate(string text, Dictionary<string, string> dictionary)
		{
			if (string.IsNullOrEmpty(text) || dictionary == null)
			{
				return text;
			}
			if (dictionary.TryGetValue(text, out string translated))
			{
				return translated;
			}
			string cacheKey = RuntimeHelpers.GetHashCode(dictionary) + "\0" + text;
			lock (TranslationCacheLock)
			{
				if (TranslationCache.TryGetValue(cacheKey, out translated))
				{
					return translated;
				}
			}
			translated = TranslatePattern(text, dictionary);
			lock (TranslationCacheLock)
			{
				if (TranslationCache.Count > TranslationCacheMaxSize)
				{
					TranslationCache.Clear();
				}
				TranslationCache[cacheKey] = translated;
			}
			return translated;
		}

		private static string TranslatePattern(string text, Dictionary<string, string> dictionary)
		{
			string result = ReplacePattern(text, @"^Checkout '(.+)'$", "Checkout '{0}'", dictionary);
			result = ReplacePattern(result, @"^Checkout '(.+)' as Worktree\.\.\.$", "Checkout '{0}' as Worktree...", dictionary);
			result = ReplacePattern(result, @"^Cloning (.+)\.\.\.$", "Cloning {0}...", dictionary);
			result = ReplacePattern(result, @"^Login to (.+)$", "Login to {0}", dictionary);
			result = ReplacePattern(result, @"^Log in to (.+)$", "Log in to {0}", dictionary);
			result = ReplacePattern(result, @"^Log in to (.+)\.\.\.$", "Log in to {0}...", dictionary);
			result = ReplacePattern(result, @"^Waiting for response from (.+)\.\.\.$", "Waiting for response from {0}...", dictionary);
			result = ReplacePattern(result, @"^Waiting for reponse from (.+)\.\.\.$", "Waiting for reponse from {0}...", dictionary);
			result = ReplacePattern(result, @"^You are already logged in to (.+) as (.+)$", "You are already logged in to {0} as {1}", dictionary);
			result = ReplacePattern(result, @"^Log out of (.+)\?$", "Log out of {0}?", dictionary);
			result = ReplacePattern(result, @"^Open '(.+)'\.\.\.$", "Open '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Delete '(.+)'\.\.\.$", "Delete '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Rename '(.+)'\.\.\.$", "Rename '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Merge into '(.+)'\.\.\.$", "Merge into '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Start Branch on '(.+)'\.\.\.$", "Start Branch on '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Branch '(.+)' already exists$", "Branch '{0}' already exists", dictionary);
			result = ReplacePattern(result, @"^Branch (.+) already exists$", "Branch {0} already exists", dictionary);
			result = ReplacePattern(result, @"^Also rename (.+)$", "Also rename {0}", dictionary);
			result = ReplacePattern(result, @"^Rename branch '(.+)'$", "Rename branch '{0}'", dictionary);
			result = ReplacePattern(result, @"^Worktree '(.+)' already exists$", "Worktree '{0}' already exists", dictionary);
			result = ReplacePattern(result, @"^Creating branch '(.+)'$", "Creating branch '{0}'", dictionary);
			result = ReplacePattern(result, @"^Sync \(Rebase on '(.+)'\)$", "Sync (Rebase on '{0}')", dictionary);
			result = ReplacePattern(result, @"^Sync '(.+)' \(Rebase on '(.+)'\)$", "Sync '{0}' (Rebase on '{1}')", dictionary);
			result = ReplacePattern(result, @"^Finish \(Merge into '(.+)'\)\.\.\.$", "Finish (Merge into '{0}')...", dictionary);
			result = ReplacePattern(result, @"^Finish '(.+)' \(Merge into '(.+)'\)\.\.\.$", "Finish '{0}' (Merge into '{1}')...", dictionary);
			result = ReplacePattern(result, @"^Sync '(.+)' with '(.+)'$", "Sync '{0}' with '{1}'", dictionary);
			result = ReplacePattern(result, @"^'(.+)' is not in sync with '(.+)'\. You must checkout and sync '(.+)' first\.$", "'{0}' is not in sync with '{1}'. You must checkout and sync '{0}' first.", dictionary);
			result = ReplacePattern(result, @"^Finish '(.+)' and merge it into '(.+)'$", "Finish '{0}' and merge it into '{1}'", dictionary);
			result = ReplacePattern(result, @"^You must sync '(.+)' first$", "You must sync '{0}' first", dictionary);
			result = ReplacePattern(result, @"^You must checkout and sync '(.+)' first$", "You must checkout and sync '{0}' first", dictionary);
			result = ReplacePattern(result, @"^You must sync '(.+)' with '(.+)' first$", "You must sync '{0}' with '{1}' first", dictionary);
			result = ReplacePattern(result, @"^Finishing (.+)\.\.\.$", "Finishing {0}...", dictionary);
			result = ReplacePattern(result, @"^Finish '(.+)'$", "Finish '{0}'", dictionary);
			result = ReplacePattern(result, @"^Fast-forward '(.+)' to '(.+)'$", "Fast-forward '{0}' to '{1}'", dictionary);
			result = ReplacePattern(result, @"^Merging into '(.+)'\.\.\.$", "Merging into '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Merge '(.+)' into (.+)\.\.\.$", "Merge '{0}' into {1}...", dictionary);
			result = ReplacePattern(result, @"^Rebase '(.+)' on '(.+)'\.\.\.$", "Rebase '{0}' on '{1}'...", dictionary);
			result = ReplacePattern(result, @"^Interactively Rebase '(.+)' on '(.+)\.\.\.$", "Interactively Rebase '{0}' on '{1}...", dictionary);
			result = ReplacePattern(result, @"^Push '(.+)' to '(.+)'\.\.\.$", "Push '{0}' to '{1}'...", dictionary);
			result = ReplacePattern(result, @"^Push '(.+)' to '(.+)'$", "Push '{0}' to '{1}'", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) branches to '(.+)'\.\.\.$", "Push {0} branches to '{1}'...", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) branches to '(.+)'$", "Push {0} branches to '{1}'", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) branches to$", "Push {0} branches to", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) branches$", "Push {0} branches", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) branches to remote repository$", "Push {0} branches to remote repository", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) tags to '(.+)'\.\.\.$", "Push {0} tags to '{1}'...", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) tags to '(.+)'$", "Push {0} tags to '{1}'", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) tags to$", "Push {0} tags to", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) tags$", "Push {0} tags", dictionary);
			result = ReplacePattern(result, @"^Push (\d+) tags to remote repository$", "Push {0} tags to remote repository", dictionary);
			result = ReplacePattern(result, @"^Cherry Pick (\d+) commits$", "Cherry Pick {0} commits", dictionary);
			result = ReplacePattern(result, @"^Force Unlock (\d+) Files$", "Force Unlock {0} Files", dictionary);
			result = ReplacePattern(result, @"^Delete (\d+) Branches\.\.\.$", "Delete {0} Branches...", dictionary);
			result = ReplacePattern(result, @"^Delete (\d+) Remote Branches\.\.\.$", "Delete {0} Remote Branches...", dictionary);
			result = ReplacePattern(result, @"^Delete (\d+) Tags\.\.\.$", "Delete {0} Tags...", dictionary);
			result = ReplacePattern(result, @"^Delete (\d+) Stashes\.\.\.$", "Delete {0} Stashes...", dictionary);
			result = ReplacePattern(result, @"^Delete (\d+) stashes$", "Delete {0} stashes", dictionary);
			result = ReplacePattern(result, @"^Open (\d+) submodules$", "Open {0} submodules", dictionary);
			result = ReplacePattern(result, @"^Open All \((\d+)\)$", "Open All ({0})", dictionary);
			result = ReplacePattern(result, @"^Update (\d+) submodules$", "Update {0} submodules", dictionary);
			result = ReplacePattern(result, @"^Updating (\d+) submodules$", "Updating {0} submodules", dictionary);
			result = ReplacePattern(result, @"^(\d+) submodules$", "{0} submodules", dictionary);
			result = ReplacePattern(result, @"^Open (\d+) worktrees\.\.\.$", "Open {0} worktrees...", dictionary);
			result = ReplacePattern(result, @"^Stash (\d+) File\.\.\.$", "Stash {0} File...", dictionary);
			result = ReplacePattern(result, @"^Stash (\d+) Files\.\.\.$", "Stash {0} Files...", dictionary);
			result = ReplacePattern(result, @"^Stash (\d+) (.+)\.\.\.$", "Stash {0} {1}...", dictionary);
			result = ReplacePattern(result, @"^Stage (\d+) File$", "Stage {0} File", dictionary);
			result = ReplacePattern(result, @"^Stage (\d+) Files$", "Stage {0} Files", dictionary);
			result = ReplacePattern(result, @"^Unstage (\d+) File$", "Unstage {0} File", dictionary);
			result = ReplacePattern(result, @"^Unstage (\d+) Files$", "Unstage {0} Files", dictionary);
			result = ReplacePattern(result, @"^Ignore all files in '(.+)'\.\.\.$", "Ignore all files in '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Ignore '(.+)'$", "Ignore '{0}'", dictionary);
			result = ReplacePattern(result, @"^Ignore All (.+) Files\.\.\.$", "Ignore All {0} Files...", dictionary);
			result = ReplacePattern(result, @"^Track All Files in '(.+)'\.\.\.$", "Track All Files in '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Track '(.+)'$", "Track '{0}'", dictionary);
			result = ReplacePattern(result, @"^Track All Files\.\.\.$", "Track All Files...", dictionary);
			result = ReplacePattern(result, @"^Track All (.+) Files\.\.\.$", "Track All {0} Files...", dictionary);
			result = ReplacePattern(result, @"^Update '(.+)'$", "Update '{0}'", dictionary);
			result = ReplacePattern(result, @"^Move '(.+)'\.\.\.$", "Move '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Moving '(.+)'$", "Moving '{0}'", dictionary);
			result = ReplacePattern(result, @"^Diff in (.+)$", "Diff in {0}", dictionary);
			result = ReplacePattern(result, @"^Merge in (.+)$", "Merge in {0}", dictionary);
			result = ReplacePattern(result, @"^Open In (.+)$", "Open In {0}", dictionary);
			result = ReplacePattern(result, @"^Open in (.+)$", "Open in {0}", dictionary);
			result = ReplacePattern(result, @"^Open '(.+)' in (.+)$", "Open '{0}' in {1}", dictionary);
			result = ReplacePattern(result, @"^Reveal Line in (.+)$", "Reveal Line in {0}", dictionary);
			result = ReplacePattern(result, @"^Create Pull Request on '(.+)'\.\.\.$", "Create Pull Request on '{0}'...", dictionary);
			result = ReplacePattern(result, @"^View on (.+)$", "View on {0}", dictionary);
			result = ReplacePattern(result, @"^Fetch '(.+)'\.\.\.$", "Fetch '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Fetch '(.+)'$", "Fetch '{0}'", dictionary);
			result = ReplacePattern(result, @"^Fetch '(.+)' Automatically$", "Fetch '{0}' Automatically", dictionary);
			result = ReplacePattern(result, @"^Create tag '(.+)'$", "Create tag '{0}'", dictionary);
			result = ReplacePattern(result, @"^Create and push tag '(.+)'$", "Create and push tag '{0}'", dictionary);
			result = ReplacePattern(result, @"^Tag '(.+)' already exists$", "Tag '{0}' already exists", dictionary);
			result = ReplacePattern(result, @"^Creating '(.+)'\.\.\.$", "Creating '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Pushing '(.+)' to '(.+)'\.\.\.$", "Pushing '{0}' to '{1}'...", dictionary);
			result = ReplacePattern(result, @"^Pull '(.+)' into '(.+)'\.\.\.$", "Pull '{0}' into '{1}'...", dictionary);
			result = ReplacePattern(result, @"^Fast-Forward to '(.+)'$", "Fast-Forward to '{0}'", dictionary);
			result = ReplacePattern(result, @"^Pull '(.+)'$", "Pull '{0}'", dictionary);
			result = ReplacePattern(result, @"^Pull '(.+)'\.\.\.$", "Pull '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Finish '(.+)'\.\.\.$", "Finish '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Code Review with (.+)\.\.\.$", "Code Review with {0}...", dictionary);
			result = ReplacePattern(result, @"^Generate commit message with (.+)$", "Generate commit message with {0}", dictionary);
			result = ReplacePattern(result, @"^Resolve Using '(.+)'$", "Resolve Using '{0}'", dictionary);
			result = ReplacePattern(result, @"^Pin '(.+)'$", "Pin '{0}'", dictionary);
			result = ReplacePattern(result, @"^Show '(.+)' commits only$", "Show '{0}' commits only", dictionary);
			result = ReplacePattern(result, @"^Hide '(.+)' in the commit list$", "Hide '{0}' in the commit list", dictionary);
			result = ReplacePattern(result, @"^Show all local branches \((\d+)\)$", "Show all local branches ({0})", dictionary);
			result = ReplacePattern(result, @"^Show all tags \((\d+)\)$", "Show all tags ({0})", dictionary);
			result = ReplacePattern(result, @"^Show all stashes \((\d+)\)$", "Show all stashes ({0})", dictionary);
			result = ReplacePattern(result, @"^Reset '(.+)' to Here\.\.\.$", "Reset '{0}' to Here...", dictionary);
			result = ReplacePattern(result, @"^Rebase '(.+)' to Here\.\.\.$", "Rebase '{0}' to Here...", dictionary);
			result = ReplacePattern(result, @"^Interactively Rebase '(.+)' to Here\.\.\.$", "Interactively Rebase '{0}' to Here...", dictionary);
			result = ReplacePattern(result, @"^Show '(.+)' Details\.\.\.$", "Show '{0}' Details...", dictionary);
			result = ReplacePattern(result, @"^Reset (\d+) Files to$", "Reset {0} Files to", dictionary);
			result = ReplacePattern(result, @"^State at Commit '(.+)'\.\.\.$", "State at Commit '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Push to '(.+)'\.\.\.$", "Push to '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Apply '(.+)'\.\.\.$", "Apply '{0}'...", dictionary);
			result = ReplacePattern(result, @"^Revert '(.+)'$", "Revert '{0}'", dictionary);
			result = ReplacePattern(result, @"^Merge unrelated '(.+)'$", "Merge unrelated '{0}'", dictionary);
			result = ReplacePattern(result, @"^default \((.+)\)$", "default ({0})", dictionary);
			result = ReplacePattern(result, @"^Issue tracker integration for '(.+)' is disabled$", "Issue tracker integration for '{0}' is disabled", dictionary);
			result = ReplacePattern(result, @"^new \((.+)\)$", "new ({0})", dictionary);
			result = ReplacePattern(result, @"^(.+) \(new\)$", "{0} (new)", dictionary);
			result = ReplacePattern(result, @"^Submodule '(.+)' contains unpushed changes$", "Submodule '{0}' contains unpushed changes", dictionary);
			result = ReplacePattern(result, @"^Add submodule '(.+)'$", "Add submodule '{0}'", dictionary);
			result = ReplacePattern(result, @"^Delete submodule '(.+)'$", "Delete submodule '{0}'", dictionary);
			result = ReplacePattern(result, @"^Are you sure you want to delete submodule (.+)\?$", "Are you sure you want to delete submodule {0}?", dictionary);
			result = ReplacePattern(result, @"^Do you want to delete submodule (.+)\?$", "Do you want to delete submodule {0}?", dictionary);
			result = ReplacePattern(result, @"^Can not move submodule to (.+)$", "Can not move submodule to {0}", dictionary);
			result = ReplacePattern(result, @"^Do you want to discard changes in (\d+) submodules\?$", "Do you want to discard changes in {0} submodules?", dictionary);
			result = ReplacePattern(result, @"^(\d+) uncommitted file$", "{0} uncommitted file", dictionary);
			result = ReplacePattern(result, @"^(\d+) uncommitted files$", "{0} uncommitted files", dictionary);
			result = ReplacePattern(result, @"^Discard all changes in '(.+)'\?$", "Discard all changes in '{0}'?", dictionary);
			result = ReplacePattern(result, @"^Cannot find private key: '(.+)'$", "Cannot find private key: '{0}'", dictionary);
			result = ReplacePattern(result, @"^Cannot find public key: '(.+)'$", "Cannot find public key: '{0}'", dictionary);
			result = ReplacePattern(result, @"^Ssh key '(.+)' already exists$", "Ssh key '{0}' already exists", dictionary);
			result = ReplacePattern(result, @"^Enter passphrase for SSH key '(.+)'$", "Enter passphrase for SSH key '{0}'", dictionary);
			result = ReplacePattern(result, @"^Do you want to delete the workspace '(.+)'\?$", "Do you want to delete the workspace '{0}'?", dictionary);
			result = ReplacePattern(result, @"^Do you want to remove (\d+) selected repositories from Fork\?$", "Do you want to remove {0} selected repositories from Fork?", dictionary);
			result = ReplacePattern(result, @"^Are you sure you want to delete worktree (.+)\?$", "Are you sure you want to delete worktree {0}?", dictionary);
			result = ReplacePattern(result, @"^Do you want to delete worktree (.+)\?$", "Do you want to delete worktree {0}?", dictionary);
			result = ReplacePattern(result, @"^Are you sure you want to delete reference to remote '(.+)'\?$", "Are you sure you want to delete reference to remote '{0}'?", dictionary);
			result = ReplacePattern(result, @"^Do you want to delete '(.+)'\?$", "Do you want to delete '{0}'?", dictionary);
			result = ReplacePattern(result, @"^'(.+)' is already a git repository\. Please select another folder\.$", "'{0}' is already a git repository. Please select another folder.", dictionary);
			result = ReplacePattern(result, @"^Do you want to reset (\d+) files to the state they were before the commit\?$", "Do you want to reset {0} files to the state they were before the commit?", dictionary);
			result = ReplacePattern(result, @"^Do you want to reset (\d+) files to the state they are at the commit\?$", "Do you want to reset {0} files to the state they are at the commit?", dictionary);
			result = ReplacePattern(result, @"^Discard Changes in (\d+) Files$", "Discard Changes in {0} Files", dictionary);
			result = ReplacePattern(result, @"^Discard (\d+) Lines$", "Discard {0} Lines", dictionary);
			result = ReplacePattern(result, @"^LFS Fetch (.+)$", "LFS Fetch {0}", dictionary);
			result = ReplacePattern(result, @"^LFS Pull (.+)$", "LFS Pull {0}", dictionary);
			result = ReplacePattern(result, @"^Apply stash '(.+)'$", "Apply stash '{0}'", dictionary);
			result = ReplacePattern(result, @"^Rename stash '(.+)'$", "Rename stash '{0}'", dictionary);
			return result;
		}

		private static string ReplacePattern(string text, string regex, string templateKey, Dictionary<string, string> dictionary)
		{
			Match match = Regex.Match(text, regex);
			if (!match.Success || !dictionary.TryGetValue(templateKey, out string template))
			{
				return text;
			}
			string[] values = new string[match.Groups.Count - 1];
			for (int i = 1; i < match.Groups.Count; i++)
			{
				values[i - 1] = match.Groups[i].Value;
			}
			return string.Format(template, values);
		}
	}
}
