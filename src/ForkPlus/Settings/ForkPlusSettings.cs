using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Settings
{
	public class ForkPlusSettings
	{
		public class RepositoryManagerSettings
		{
			public class Coder
			{
				public static RepositoryManagerSettings Decode(JToken json)
				{
					try
					{
						if (json == null || !json.HasValues)
						{
							return null;
						}
						string[] sourceDirectories = JsonHelper.DecodeStringArray(json["SourceDirectories"] as JArray);
						Category[] array = JsonHelper.DecodeArray(json["Categories"] as JArray, Category.Coder.Decode);
						Repository[] array2 = JsonHelper.DecodeArray(json["Repositories"] as JArray, Repository.Coder.Decode);
						return new RepositoryManagerSettings(scanDepth: json["ScanDepth"]?.Value<int>() ?? DefaultScanDepth, sourceDirectories: sourceDirectories, categories: array ?? new Category[0], repositories: array2 ?? new Repository[0]);
					}
					catch (Exception arg)
					{
						Log.Warn($"Cannot decode RepositoryManagerSettings: '{arg}'");
						return null;
					}
				}

				public static JToken Encode(RepositoryManagerSettings repositoryManager)
				{
					JObject jObject = new JObject();
					try
					{
						JArray value = JsonHelper.EncodeStringArray(repositoryManager.SourceDirectories);
						jObject.Add("SourceDirectories", value);
						JArray value2 = JsonHelper.EncodeArray(repositoryManager.Categories, Category.Coder.Encode);
						jObject.Add("Categories", value2);
						JArray value3 = JsonHelper.EncodeArray(repositoryManager.Repositories, Repository.Coder.Encode);
						jObject.Add("Repositories", value3);
						JValue value4 = new JValue(repositoryManager.ScanDepth);
						jObject.Add("ScanDepth", value4);
					}
					catch (Exception arg)
					{
						Log.Warn($"Cannot encode RepositoryManagerSettings: '{arg}'");
					}
					return jObject;
				}
			}

			[DebuggerDisplay("[{ParentId}]-[{Id}] {Name}")]
			public class Category
			{
				public class Coder
				{
					public static Category Decode(JToken json)
					{
						try
						{
							int id = json["Id"].Value<int>();
							string name = json["Name"].Value<string>();
							int parentId = json["ParentId"].Value<int>();
							return new Category(id, name, parentId);
						}
						catch
						{
							return null;
						}
					}

					public static JToken Encode(Category category)
					{
						return new JObject
						{
							{
								"Id",
								new JValue(category.Id)
							},
							{
								"ParentId",
								new JValue(category.ParentId)
							},
							{
								"Name",
								new JValue(category.Name)
							}
						};
					}
				}

				public int Id { get; }

				public int ParentId { get; set; }

				public string Name { get; set; }

				public Category(int id, string name, int parentId)
				{
					Id = id;
					ParentId = parentId;
					Name = name;
				}
			}

			[DebuggerDisplay("[{ParentId}]-[{Id}] {Name}: {Path}")]
			public class Repository
			{
				public static class Coder
				{
					public static Repository Decode(JToken json)
					{
						if (json == null || !json.HasValues)
						{
							return null;
						}
						int? num = json["Id"]?.Value<int>();
						int? num2 = json["ParentId"]?.Value<int>();
						string text = json["Name"]?.Value<string>();
						string text2 = json["Path"]?.Value<string>();
						string gitDirectoryPath = json["GitDirectoryPath"]?.Value<string>();
						DateTime lastAccessTime = JsonHelper.DecodeDateTime(json["LastAccessTime"], DateTime.MinValue);
						RepositoryColor color = (RepositoryColor)(json["Color"]?.Value<int>() ?? 0);
						if (num.HasValue)
						{
							int valueOrDefault = num.GetValueOrDefault();
							if (num2.HasValue)
							{
								int valueOrDefault2 = num2.GetValueOrDefault();
								if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2))
								{
									return new Repository(valueOrDefault, text, text2, gitDirectoryPath, lastAccessTime, valueOrDefault2, color);
								}
							}
						}
						return null;
					}

					public static JObject Encode(Repository repository)
					{
						if (repository == null)
						{
							return null;
						}
						JObject jObject = new JObject();
						jObject.Add("Id", new JValue(repository.Id));
						jObject.Add("ParentId", new JValue(repository.ParentId));
						jObject.Add("Name", new JValue(repository.Name));
						jObject.Add("Path", new JValue(repository.Path));
						jObject.Add("GitDirectoryPath", new JValue(repository.GitDirectoryPath));
						jObject.Add("LastAccessTime", new JValue(repository.LastAccessTime));
						if (repository.Color != 0)
						{
							jObject.Add("Color", new JValue((long)repository.Color));
						}
						return jObject;
					}
				}

				public int Id { get; }

				public int ParentId { get; set; }

				public string Path { get; }

				public string GitDirectoryPath { get; set; }

				public string Name { get; set; }

				public DateTime LastAccessTime { get; set; }

				public RepositoryColor Color { get; set; }

				public Repository(int id, string name, string normalizedPath, string gitDirectoryPath, DateTime lastAccessTime, int parentId, RepositoryColor color = RepositoryColor.None)
				{
					Id = id;
					ParentId = parentId;
					Name = name;
					Path = normalizedPath;
					GitDirectoryPath = gitDirectoryPath;
					Color = color;
					LastAccessTime = lastAccessTime;
				}
			}

			private static readonly int DefaultScanDepth = 5;

			private string[] _sourceDirectories;

			private Category[] _categories;

			private Repository[] _repositories;

			private int _scanDepth;

			public string[] SourceDirectories
			{
				get
				{
					return _sourceDirectories;
				}
				set
				{
					_sourceDirectories = value;
				}
			}

			public string DefaultSourceDirectory
			{
				get
				{
					return SourceDirectories[0];
				}
				set
				{
					SourceDirectories = new string[1] { value };
				}
			}

			public Category[] Categories
			{
				get
				{
					return _categories;
				}
				private set
				{
					_categories = value;
				}
			}

			public Repository[] Repositories
			{
				get
				{
					return _repositories;
				}
				private set
				{
					_repositories = value;
				}
			}

			public int ScanDepth
			{
				get
				{
					return _scanDepth;
				}
				private set
				{
					_scanDepth = value;
				}
			}

			public static RepositoryManagerSettings CreateDefault()
			{
				return new RepositoryManagerSettings(null, new Category[0], new Repository[0], DefaultScanDepth);
			}

			public RepositoryManagerSettings([Null] string[] sourceDirectories, Category[] categories, Repository[] repositories, int scanDepth)
			{
				SourceDirectories = sourceDirectories ?? new string[1] { Environment.ExpandEnvironmentVariables("%userprofile%") };
				Categories = categories;
				Repositories = repositories;
				ScanDepth = scanDepth;
			}

			public void RemoveAll()
			{
				Repositories = new Repository[0];
				Categories = new Category[0];
			}

			public void RemoveRepositories(Repository[] repositories)
			{
				List<Repository> list = new List<Repository>(Repositories);
				foreach (Repository item in repositories)
				{
					list.Remove(item);
				}
				Repositories = list.ToArray();
			}
		}

		public class WorkspacesSettings
		{
			public class Coder
			{
				[Null]
				public static WorkspacesSettings Decode([Null] JObject jObject)
				{
					try
					{
						if (jObject == null)
						{
							return null;
						}
						Workspace[] array = JsonHelper.DecodeArray(jObject["All"] as JArray, DecodeWorkspace);
						string activeWorkspaceName = jObject["ActiveWorkspace"]?.Value<string>();
						Workspace workspace = IReadOnlyListExtensions.FirstItem(array, (Workspace x) => x.Name == activeWorkspaceName);
						if (workspace == null)
						{
							workspace = array.FirstItem();
						}
						bool showInTitle = jObject["ShowInTitle"]?.Value<bool>() ?? false;
						return new WorkspacesSettings(array, workspace, showInTitle);
					}
					catch (Exception arg)
					{
						Log.Warn($"Cannot decode WorkspacesSettings: '{arg}'");
						return null;
					}
				}

				[Null]
				private static Workspace DecodeWorkspace(JToken json)
				{
					try
					{
						string? name = json["Name"].Value<string>();
						string[] repositories = JsonHelper.DecodeStringArray(json["Repositories"] as JArray) ?? new string[0];
						string activeRepository = json["ActiveRepository"]?.Value<string>();
						return new Workspace(name, repositories, activeRepository);
					}
					catch
					{
						return null;
					}
				}

				public static JToken Encode(WorkspacesSettings workspaces)
				{
					JObject jObject = new JObject();
					try
					{
						jObject.Add("All", JsonHelper.EncodeArray(workspaces.All, Encode));
						jObject.Add("ActiveWorkspace", new JValue(workspaces.ActiveWorkspace.Name));
						jObject.Add("ShowInTitle", new JValue(workspaces.ShowInTitle));
					}
					catch (Exception arg)
					{
						Log.Warn($"Cannot encode RepositoryManagerSettings: '{arg}'");
					}
					return jObject;
				}

				private static JToken Encode(Workspace workspace)
				{
					return new JObject
					{
						{
							"Name",
							new JValue(workspace.Name)
						},
						{
							"Repositories",
							JsonHelper.EncodeStringArray(workspace.Repositories)
						},
						{
							"ActiveRepository",
							new JValue(workspace.ActiveRepository)
						}
					};
				}
			}

			public Workspace[] All { get; private set; }

			public Workspace ActiveWorkspace { get; set; }

			public bool ShowInTitle { get; set; }

			public WorkspacesSettings(string[] repositories, [Null] string activeRepository)
			{
				Workspace[] array = new Workspace[2]
				{
					new Workspace("Home", new string[0], null),
					new Workspace("Work", repositories, activeRepository)
				};
				Update(array, array[1], showInTitle: false);
			}

			public WorkspacesSettings(Workspace[] all, Workspace activeWorkspace, bool showInTitle)
			{
				if (all.Length == 0)
				{
					WorkspacesSettings workspacesSettings = CreateDefault();
					Update(workspacesSettings.All, workspacesSettings.ActiveWorkspace, showInTitle);
				}
				else
				{
					Update(all, activeWorkspace, showInTitle);
				}
			}

			public void Update(Workspace[] workspaces, Workspace activeWorkspace, bool showInTitle)
			{
				Array.Sort(workspaces, (Workspace x, Workspace y) => x.Name.CompareTo(y.Name));
				All = workspaces;
				ActiveWorkspace = activeWorkspace;
				ShowInTitle = showInTitle;
			}

			public static WorkspacesSettings CreateDefault()
			{
				Workspace[] obj = new Workspace[2]
				{
					new Workspace("Home", new string[0], null),
					new Workspace("Work", new string[0], null)
				};
				return new WorkspacesSettings(obj, obj[1], showInTitle: false);
			}
		}

		public class GitMmSettings
		{
			public class Coder
			{
				[Null]
				public static GitMmSettings Decode([Null] JObject jObject)
				{
					try
					{
						if (jObject == null)
						{
							return null;
						}
						return new GitMmSettings(
							JsonHelper.DecodeStringArray(jObject["Workspaces"] as JArray) ?? new string[0],
							jObject["ActiveWorkspace"]?.Value<string>(),
							jObject["ActiveSubrepo"]?.Value<string>(),
							DecodeActiveSubrepos(jObject["ActiveSubrepos"] as JObject),
							DecodeSubrepoOrders(jObject["SubrepoOrders"] as JObject),
							DecodeSubrepoOrders(jObject["VisibleSubrepos"] as JObject),
							jObject["CommandOutputCollapsed"]?.Value<bool>() ?? false,
							jObject["CommandOutputHeight"]?.Value<double>() ?? 150.0,
							JsonHelper.DecodeStringArray(jObject["CommandHistory"] as JArray) ?? new string[0],
							JsonHelper.DecodeStringArray(jObject["UploadLinks"] as JArray) ?? new string[0],
							DecodeSubrepoOrders(jObject["UploadLinksByWorkspace"] as JObject),
							jObject["SyncJobs"]?.Value<string>() ?? "4",
							jObject["StartBranch"]?.Value<string>(),
							jObject["InitUrl"]?.Value<string>(),
							jObject["InitManifest"]?.Value<string>() ?? "dependency.xml",
							jObject["InitBranch"]?.Value<string>() ?? "master",
							jObject["InitGroup"]?.Value<string>() ?? "default",
							DecodeDialogOptions(jObject["DialogOptions"] as JObject));
					}
					catch (Exception arg)
					{
						Log.Warn($"Cannot decode GitMmSettings: '{arg}'");
						return null;
					}
				}

				public static JToken Encode(GitMmSettings settings)
				{
					return new JObject
					{
						{
							"Workspaces",
							JsonHelper.EncodeStringArray(settings.Workspaces)
						},
						{
							"ActiveWorkspace",
							new JValue(settings.ActiveWorkspace)
						},
						{
							"ActiveSubrepo",
							new JValue(settings.ActiveSubrepo)
						},
						{
							"ActiveSubrepos",
							EncodeActiveSubrepos(settings.ActiveSubrepos)
						},
						{
							"SubrepoOrders",
							EncodeSubrepoOrders(settings.SubrepoOrders)
						},
						{
							"VisibleSubrepos",
							EncodeSubrepoOrders(settings.VisibleSubrepos)
						},
						{
							"CommandOutputCollapsed",
							new JValue(settings.CommandOutputCollapsed)
						},
						{
							"CommandOutputHeight",
							new JValue(settings.CommandOutputHeight)
						},
						{
							"CommandHistory",
							JsonHelper.EncodeStringArray(settings.CommandHistory)
						},
						{
							"UploadLinks",
							JsonHelper.EncodeStringArray(settings.UploadLinks)
						},
						{
							"UploadLinksByWorkspace",
							EncodeSubrepoOrders(settings.UploadLinksByWorkspace)
						},
						{
							"SyncJobs",
							new JValue(settings.SyncJobs)
						},
						{
							"StartBranch",
							new JValue(settings.StartBranch)
						},
						{
							"InitUrl",
							new JValue(settings.InitUrl)
						},
						{
							"InitManifest",
							new JValue(settings.InitManifest)
						},
						{
							"InitBranch",
							new JValue(settings.InitBranch)
						},
						{
							"InitGroup",
							new JValue(settings.InitGroup)
						},
						{
							"DialogOptions",
							EncodeDialogOptions(settings.DialogOptions)
						}
					};
				}

				private static Dictionary<string, string> DecodeActiveSubrepos([Null] JObject jObject)
				{
					Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					if (jObject == null)
					{
						return result;
					}
					foreach (JProperty property in jObject.Properties())
					{
						string key = NormalizePath(property.Name);
						string value = NormalizePath(property.Value?.Value<string>());
						if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
						{
							result[key] = value;
						}
					}
					return result;
				}

				private static JObject EncodeActiveSubrepos(Dictionary<string, string> activeSubrepos)
				{
					JObject result = new JObject();
					if (activeSubrepos == null)
					{
						return result;
					}
					foreach (KeyValuePair<string, string> item in activeSubrepos)
					{
						if (!string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
						{
							result.Add(item.Key, new JValue(item.Value));
						}
					}
					return result;
				}

				private static Dictionary<string, string[]> DecodeSubrepoOrders([Null] JObject jObject)
				{
					Dictionary<string, string[]> result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
					if (jObject == null)
					{
						return result;
					}
					foreach (JProperty property in jObject.Properties())
					{
						string workspacePath = NormalizePath(property.Name);
						string[] subrepoPaths = NormalizePaths(JsonHelper.DecodeStringArray(property.Value as JArray));
						if (!string.IsNullOrWhiteSpace(workspacePath) && subrepoPaths.Length > 0)
						{
							result[workspacePath] = subrepoPaths;
						}
					}
					return result;
				}

				private static JObject EncodeSubrepoOrders(Dictionary<string, string[]> subrepoOrders)
				{
					JObject result = new JObject();
					if (subrepoOrders == null)
					{
						return result;
					}
					foreach (KeyValuePair<string, string[]> item in subrepoOrders)
					{
						string workspacePath = NormalizePath(item.Key);
						string[] subrepoPaths = NormalizePaths(item.Value);
						if (!string.IsNullOrWhiteSpace(workspacePath) && subrepoPaths.Length > 0)
						{
							result.Add(workspacePath, JsonHelper.EncodeStringArray(subrepoPaths));
						}
					}
					return result;
				}

				private static Dictionary<string, string> DecodeDialogOptions([Null] JObject jObject)
				{
					Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					if (jObject == null)
					{
						return result;
					}
					foreach (JProperty property in jObject.Properties())
					{
						string key = property.Name;
						string value = property.Value?.Value<string>();
						if (!string.IsNullOrWhiteSpace(key) && value != null)
						{
							result[key] = value;
						}
					}
					return result;
				}

				private static JObject EncodeDialogOptions(Dictionary<string, string> dialogOptions)
				{
					JObject result = new JObject();
					if (dialogOptions == null)
					{
						return result;
					}
					foreach (KeyValuePair<string, string> item in dialogOptions)
					{
						if (!string.IsNullOrWhiteSpace(item.Key) && item.Value != null)
						{
							result.Add(item.Key, new JValue(item.Value));
						}
					}
					return result;
				}
			}

			public string[] Workspaces { get; }

			[Null]
			public string ActiveWorkspace { get; }

			[Null]
			public string ActiveSubrepo { get; }

			public Dictionary<string, string> ActiveSubrepos { get; }

			public Dictionary<string, string[]> SubrepoOrders { get; }

			public Dictionary<string, string[]> VisibleSubrepos { get; }

			public bool CommandOutputCollapsed { get; }

			public double CommandOutputHeight { get; }

			public string[] CommandHistory { get; }

			public string[] UploadLinks { get; }

			public Dictionary<string, string[]> UploadLinksByWorkspace { get; }

			public string SyncJobs { get; }

			[Null]
			public string StartBranch { get; }

			[Null]
			public string InitUrl { get; }

			public string InitManifest { get; }

			public string InitBranch { get; }

			public string InitGroup { get; }

			public Dictionary<string, string> DialogOptions { get; }

			public GitMmSettings(string[] workspaces, [Null] string activeWorkspace, [Null] string activeSubrepo, Dictionary<string, string> activeSubrepos, Dictionary<string, string[]> subrepoOrders, Dictionary<string, string[]> visibleSubrepos, bool commandOutputCollapsed, double commandOutputHeight, string[] commandHistory, string[] uploadLinks, Dictionary<string, string[]> uploadLinksByWorkspace, string syncJobs, [Null] string startBranch, [Null] string initUrl, string initManifest, string initBranch, string initGroup, Dictionary<string, string> dialogOptions)
			{
				Workspaces = NormalizePaths(workspaces);
				ActiveWorkspace = NormalizePath(activeWorkspace);
				ActiveSubrepo = NormalizePath(activeSubrepo);
				ActiveSubrepos = NormalizeActiveSubrepos(activeSubrepos);
				SubrepoOrders = NormalizeSubrepoOrders(subrepoOrders);
				VisibleSubrepos = NormalizeSubrepoOrders(visibleSubrepos);
				CommandOutputCollapsed = commandOutputCollapsed;
				CommandOutputHeight = commandOutputHeight > 0.0 ? commandOutputHeight : 150.0;
				CommandHistory = NormalizeTextList(commandHistory, 20);
				UploadLinks = NormalizeTextList(uploadLinks, 20);
				UploadLinksByWorkspace = NormalizeSubrepoOrders(uploadLinksByWorkspace);
				if (!string.IsNullOrWhiteSpace(activeWorkspace) && !string.IsNullOrWhiteSpace(activeSubrepo))
				{
					ActiveSubrepos[ActiveWorkspace] = ActiveSubrepo;
				}
				SyncJobs = string.IsNullOrWhiteSpace(syncJobs) ? "4" : syncJobs;
				StartBranch = startBranch;
				InitUrl = initUrl;
				InitManifest = string.IsNullOrWhiteSpace(initManifest) ? "dependency.xml" : initManifest;
				InitBranch = string.IsNullOrWhiteSpace(initBranch) ? "master" : initBranch;
				InitGroup = string.IsNullOrWhiteSpace(initGroup) ? "default" : initGroup;
				DialogOptions = NormalizeDialogOptions(dialogOptions);
			}

			[Null]
			public string GetActiveSubrepo(string workspacePath)
			{
				if (string.IsNullOrWhiteSpace(workspacePath))
				{
					return null;
				}
				string normalizedWorkspacePath = NormalizePath(workspacePath);
				return ActiveSubrepos.TryGetValue(normalizedWorkspacePath, out var subrepoPath) ? subrepoPath : null;
			}

			public string[] GetSubrepoOrder(string workspacePath)
			{
				string normalizedWorkspacePath = NormalizePath(workspacePath);
				return normalizedWorkspacePath != null && SubrepoOrders.TryGetValue(normalizedWorkspacePath, out var subrepoOrder) ? subrepoOrder : new string[0];
			}

			[Null]
			public string[] GetVisibleSubrepos(string workspacePath)
			{
				string normalizedWorkspacePath = NormalizePath(workspacePath);
				return normalizedWorkspacePath != null && VisibleSubrepos.TryGetValue(normalizedWorkspacePath, out var visibleSubrepos) ? visibleSubrepos : null;
			}

			public string GetDialogOption(string key, string defaultValue = "")
			{
				return !string.IsNullOrWhiteSpace(key) && DialogOptions.TryGetValue(key, out var value) ? value : defaultValue;
			}

			public string[] GetUploadLinks(string workspacePath)
			{
				string normalizedWorkspacePath = NormalizePath(workspacePath);
				return normalizedWorkspacePath != null && UploadLinksByWorkspace.TryGetValue(normalizedWorkspacePath, out var uploadLinks) ? uploadLinks : new string[0];
			}

			[Null]
			private static string NormalizePath([Null] string path)
			{
				if (string.IsNullOrWhiteSpace(path))
				{
					return null;
				}
				return PathHelper.Normalize(path).TrimEnd('\\', '/');
			}

			private static string[] NormalizePaths(string[] paths)
			{
				if (paths == null)
				{
					return new string[0];
				}
				List<string> result = new List<string>();
				HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (string path in paths)
				{
					string normalizedPath = NormalizePath(path);
					if (!string.IsNullOrWhiteSpace(normalizedPath) && seen.Add(normalizedPath))
					{
						result.Add(normalizedPath);
					}
				}
				return result.ToArray();
			}

			private static Dictionary<string, string> NormalizeActiveSubrepos(Dictionary<string, string> activeSubrepos)
			{
				Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				if (activeSubrepos == null)
				{
					return result;
				}
				foreach (KeyValuePair<string, string> item in activeSubrepos)
				{
					string workspacePath = NormalizePath(item.Key);
					string subrepoPath = NormalizePath(item.Value);
					if (!string.IsNullOrWhiteSpace(workspacePath) && !string.IsNullOrWhiteSpace(subrepoPath))
					{
						result[workspacePath] = subrepoPath;
					}
				}
				return result;
			}

			private static Dictionary<string, string[]> NormalizeSubrepoOrders(Dictionary<string, string[]> subrepoOrders)
			{
				Dictionary<string, string[]> result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
				if (subrepoOrders == null)
				{
					return result;
				}
				foreach (KeyValuePair<string, string[]> item in subrepoOrders)
				{
					string workspacePath = NormalizePath(item.Key);
					string[] subrepoPaths = NormalizePaths(item.Value);
					if (!string.IsNullOrWhiteSpace(workspacePath) && subrepoPaths.Length > 0)
					{
						result[workspacePath] = subrepoPaths;
					}
				}
				return result;
			}

			private static string[] NormalizeTextList(string[] values, int maxCount)
			{
				if (values == null)
				{
					return new string[0];
				}
				List<string> result = new List<string>();
				HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (string value in values)
				{
					if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
					{
						result.Add(value);
						if (result.Count >= maxCount)
						{
							break;
						}
					}
				}
				return result.ToArray();
			}

			private static Dictionary<string, string> NormalizeDialogOptions(Dictionary<string, string> dialogOptions)
			{
				Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				if (dialogOptions == null)
				{
					return result;
				}
				foreach (KeyValuePair<string, string> item in dialogOptions)
				{
					if (!string.IsNullOrWhiteSpace(item.Key) && item.Value != null)
					{
						result[item.Key] = item.Value;
					}
				}
				return result;
			}

			public static GitMmSettings CreateDefault()
			{
				return new GitMmSettings(new string[0], null, null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase), false, 150.0, new string[0], new string[0], new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase), "4", null, null, "dependency.xml", "master", "default", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
			}
		}

		private static readonly Mutex _fsMutex = new Mutex(initiallyOwned: false, "ForkPlusSettingsMutex");

		private static readonly object _padlock = new object();

		private static ForkPlusSettings _default = null;

		private string _guid;

		private ApplicationUpdateType _applicationUpdateType;

		private ActivityManagerViewMode _activityManagerViewMode;

		private ReferenceSortOrder _localBranchSortOrder;

		private ReferenceSortOrder _remoteBranchSortOrder;

		private ReferenceSortOrder _tagSortOrder;

		private bool _fetch_FetchAllRemotes;

		private bool _pull_rebase;

		private bool _pull_stashAndReapply;

		private bool _push_pushAllTags;

		private bool _createTag_push;

		private bool _applyStash_DeleteAfterApply;

		private bool _cherryPick_AppendOriginSha;

		private bool _createBranch_Checkout;

		private bool _checkout_StashAndReapply;

		private bool _checkoutAndSync_StashAndReapply;

		private bool _saveStash_StageNewFiles;

		private bool _interactiveRebase_CreateBackup;

		private bool _interactiveRebase_UpdateRefs;

		private bool _gitFlowFinishFeature_DeleteBranches;

		private bool _gitFlowFinishFeature_Rebase;

		private bool _gitFlowFinishFeature_NoFastForward;

		private bool _gitFlowFinishRelease_DeleteBranches;

		private bool _gitFlowFinishRelease_BackMergeMaster;

		private bool _gitFlowFinishHotfix_DeleteBranches;

		private DiffLayoutMode _commitDiffLayoutMode;

		private DiffLayoutMode _historyDiffLayoutMode;

		private DiffLayoutMode _popupDiffLayoutMode;

		private DiffLayoutMode _revisionDiffLayoutMode;

		private DiffLayoutMode _revisionWindowDiffLayoutMode;

		private bool _diffIgnoreWhitespaces;

		private bool _diffShowHiddenSymbols;

		private bool _diffWordWrap;

		private double _codeEditorFontSize;

		private bool _diffShowChangeMarks;

		private bool _diffShowEntireFile;

		private bool _revisionWindowDiffShowEntireFile;

		private int _diffContextSize;

		private bool _imageDiffHighlightPixels;

		private int _pageGuideLinePosition;

		private int _commitSubjectLowLimit;

		private int _commitSubjectHighLimit;

		private string _commitMessageRegex;

		private bool _openAiLoggedIn;

		private string _aiReviewServiceUrl;

		private string _aiReviewApiKey;

		private bool _aiReviewAutoFetchModels;

		private string[] _aiReviewModels;

		private string _aiReviewSelectedModel;

		private int _aiReviewRetryCount;

		private int _aiReviewTimeoutSeconds;

		private string _aiDevSkillContent;

		private string _aiDevSkillList;

		private string _aiDevSendMode;

		private bool _pushAutomaticallyOnCommit;

		private bool _compactBranchLabels;

		private bool _disableSyntaxHighlighting;

		private int _maxCommitCount;

		private int _layoutScaling;

		private MergeType _mergeType;

		private bool _fetchRemotesAutomatically;

		private bool _fetchAllTags;

		private bool _rebaseAutostash;

		private bool _rebaseUpdateRefs;

		private int _automaticStatusUpdateInterval;

		private CommitSpellCheckingMode _commitSpellCheckingMode;

		private string _referenceSpaceCharacterReplacement;

		private double _commitViewColumnWidth;

		private double _commitViewCombinedListLocationColumnWidth;

		private double _sidebarColumnWidth;

		private double _revisionDetailsChangesColumnWidth;

		private double _revisionDetailsFileTreeColumnWidth;

		private double _aiResultColumnWidth;

		private double _aiReviewFileTreeColumnWidth;

		private double _verticalLayoutRevisionListViewWidth;

		private bool _showWorktrees;

		private bool _updateSubmodulesOnCheckout;

		private WindowLocationState _mainWindowLocationState;

		private WindowLocationState _revisionWindowLocationState;

		private WindowLocationState _sideBySideMergeWindowLocationState;

		private WindowLocationState _blameWindowLocationState;

		private WindowLocationState _historyWindowLocationState;

		private WindowLocationState _aiResultWindowLocationState;

		private double _repositoryManagerTreeViewColumnWidth;

		private ExpandedTreeViewElement[] _repositoryManagerTreeViewExpandedItems;

		private ThemeType _theme;

		private bool _followSystemTheme;

		private string _uiLanguage;

		private MergerLayoutOrientation _mergerOrientation;

		private RevisionListOrientation _revisionListOrientation;

		private RevisionSortOrder _revisionSortOrder;

		private FileListMode _fileListMode;

		[Null]
		private ExternalTool[] _externalMergeTools;

		private MergeTool _mergeTool;

		[Null]
		private ExternalTool[] _externalDiffTools;

		private MergeTool _externalDiffTool;

		private ShellTool _shellTool;

		private bool _showBugtrackerLinks;

		private DateTime _lastUpdateCheck;

		private bool _checkForUpdatesAutomatically;

		private int _updateCheckIntervalHours;

		private string _skippedUpdateVersion;

		private string _gitInstancePath;

		private string _gitMmInstancePath;

		private bool _verboseGitOutput;

		private string[] _sshKeys;

		private string _recentPatchDirectory;

		private bool _disableHardwareAcceleration;

		private bool _migratedToFork2_10_3;

		private bool _seenNewYear2026;

		private RepositoryManagerSettings _repositoryManager;

		private WorkspacesSettings _workspaces;

		private GitMmSettings _gitMm;

		private bool _disableRefreshOnAppActivation;

		private bool _logElapsedTime;

		private bool _logToActiveNotepadProcess;

		private string _squirrelReleaseUpdateChannel;

		public static ForkPlusSettings Default
		{
			get
			{
				lock (_padlock)
				{
					if (_default == null)
					{
						_default = (DesignTimeHelper.IsInDesignMode() ? Decode(new JObject()) : Load());
					}
					return _default;
				}
			}
		}

		public string Guid
		{
			get
			{
				return _guid;
			}
			set
			{
				_guid = value;
			}
		}

		public ApplicationUpdateType ApplicationUpdateType
		{
			get
			{
				return _applicationUpdateType;
			}
			set
			{
				_applicationUpdateType = value;
				switch (_applicationUpdateType)
				{
				case ApplicationUpdateType.Develop:
					SquirrelReleaseUpdateChannel = Consts.ForkPlus.ApplicationUpdate.DevelopUpdateChannel;
					break;
				case ApplicationUpdateType.Stable:
				case ApplicationUpdateType.Off:
					SquirrelReleaseUpdateChannel = Consts.ForkPlus.ApplicationUpdate.StableUpdateChannel;
					break;
				}
			}
		}

		public ActivityManagerViewMode ActivityManagerViewMode
		{
			get
			{
				return _activityManagerViewMode;
			}
			set
			{
				_activityManagerViewMode = value;
			}
		}

		public ReferenceSortOrder LocalBranchSortOrder
		{
			get
			{
				return _localBranchSortOrder;
			}
			set
			{
				_localBranchSortOrder = value;
			}
		}

		public ReferenceSortOrder RemoteBranchSortOrder
		{
			get
			{
				return _remoteBranchSortOrder;
			}
			set
			{
				_remoteBranchSortOrder = value;
			}
		}

		public ReferenceSortOrder TagSortOrder
		{
			get
			{
				return _tagSortOrder;
			}
			set
			{
				_tagSortOrder = value;
			}
		}

		public bool Fetch_FetchAllRemotes
		{
			get
			{
				return _fetch_FetchAllRemotes;
			}
			set
			{
				_fetch_FetchAllRemotes = value;
			}
		}

		public bool Pull_Rebase
		{
			get
			{
				return _pull_rebase;
			}
			set
			{
				_pull_rebase = value;
			}
		}

		public bool Pull_StashAndReapply
		{
			get
			{
				return _pull_stashAndReapply;
			}
			set
			{
				_pull_stashAndReapply = value;
			}
		}

		public bool Push_PushAllTags
		{
			get
			{
				return _push_pushAllTags;
			}
			set
			{
				_push_pushAllTags = value;
			}
		}

		public bool CreateTag_Push
		{
			get
			{
				return _createTag_push;
			}
			set
			{
				_createTag_push = value;
			}
		}

		public bool ApplyStash_DeleteAfterApply
		{
			get
			{
				return _applyStash_DeleteAfterApply;
			}
			set
			{
				_applyStash_DeleteAfterApply = value;
			}
		}

		public bool CherryPick_AppendOriginSha
		{
			get
			{
				return _cherryPick_AppendOriginSha;
			}
			set
			{
				_cherryPick_AppendOriginSha = value;
			}
		}

		public bool CreateBranch_Checkout
		{
			get
			{
				return _createBranch_Checkout;
			}
			set
			{
				_createBranch_Checkout = value;
			}
		}

		public bool Checkout_StashAndReapply
		{
			get
			{
				return _checkout_StashAndReapply;
			}
			set
			{
				_checkout_StashAndReapply = value;
			}
		}

		public bool CheckoutAndSync_StashAndReapply
		{
			get
			{
				return _checkoutAndSync_StashAndReapply;
			}
			set
			{
				_checkoutAndSync_StashAndReapply = value;
			}
		}

		public bool SaveStash_StageNewFiles
		{
			get
			{
				return _saveStash_StageNewFiles;
			}
			set
			{
				_saveStash_StageNewFiles = value;
			}
		}

		public bool InteractiveRebase_CreateBackup
		{
			get
			{
				return _interactiveRebase_CreateBackup;
			}
			set
			{
				_interactiveRebase_CreateBackup = value;
			}
		}

		public bool InteractiveRebase_UpdateRefs
		{
			get
			{
				return _interactiveRebase_UpdateRefs;
			}
			set
			{
				_interactiveRebase_UpdateRefs = value;
			}
		}

		public bool GitFlowFinishFeature_DeleteBranches
		{
			get
			{
				return _gitFlowFinishFeature_DeleteBranches;
			}
			set
			{
				_gitFlowFinishFeature_DeleteBranches = value;
			}
		}

		public bool GitFlowFinishFeature_Rebase
		{
			get
			{
				return _gitFlowFinishFeature_Rebase;
			}
			set
			{
				_gitFlowFinishFeature_Rebase = value;
			}
		}

		public bool GitFlowFinishFeature_NoFastForward
		{
			get
			{
				return _gitFlowFinishFeature_NoFastForward;
			}
			set
			{
				_gitFlowFinishFeature_NoFastForward = value;
			}
		}

		public bool GitFlowFinishRelease_DeleteBranches
		{
			get
			{
				return _gitFlowFinishRelease_DeleteBranches;
			}
			set
			{
				_gitFlowFinishRelease_DeleteBranches = value;
			}
		}

		public bool GitFlowFinishRelease_BackMergeMaster
		{
			get
			{
				return _gitFlowFinishRelease_BackMergeMaster;
			}
			set
			{
				_gitFlowFinishRelease_BackMergeMaster = value;
			}
		}

		public bool GitFlowFinishHotfix_DeleteBranches
		{
			get
			{
				return _gitFlowFinishHotfix_DeleteBranches;
			}
			set
			{
				_gitFlowFinishHotfix_DeleteBranches = value;
			}
		}

		public DiffLayoutMode CommitDiffLayoutMode
		{
			get
			{
				return _commitDiffLayoutMode;
			}
			set
			{
				_commitDiffLayoutMode = value;
			}
		}

		public DiffLayoutMode HistoryDiffLayoutMode
		{
			get
			{
				return _historyDiffLayoutMode;
			}
			set
			{
				_historyDiffLayoutMode = value;
			}
		}

		public DiffLayoutMode PopupDiffLayoutMode
		{
			get
			{
				return _popupDiffLayoutMode;
			}
			set
			{
				_popupDiffLayoutMode = value;
			}
		}

		public DiffLayoutMode RevisionDiffLayoutMode
		{
			get
			{
				return _revisionDiffLayoutMode;
			}
			set
			{
				_revisionDiffLayoutMode = value;
			}
		}

		public DiffLayoutMode RevisionWindowDiffLayoutMode
		{
			get
			{
				return _revisionWindowDiffLayoutMode;
			}
			set
			{
				_revisionWindowDiffLayoutMode = value;
			}
		}

		public bool DiffIgnoreWhitespaces
		{
			get
			{
				return _diffIgnoreWhitespaces;
			}
			set
			{
				_diffIgnoreWhitespaces = value;
			}
		}

		public bool DiffShowHiddenSymbols
		{
			get
			{
				return _diffShowHiddenSymbols;
			}
			set
			{
				_diffShowHiddenSymbols = value;
			}
		}

		public bool DiffWordWrap
		{
			get
			{
				return _diffWordWrap;
			}
			set
			{
				_diffWordWrap = value;
			}
		}

		public double CodeEditorFontSize
		{
			get
			{
				return _codeEditorFontSize;
			}
			set
			{
				_codeEditorFontSize = value;
			}
		}

		public bool DiffShowChangeMarks
		{
			get
			{
				return _diffShowChangeMarks;
			}
			set
			{
				_diffShowChangeMarks = value;
			}
		}

		public bool DiffShowEntireFile
		{
			get
			{
				return _diffShowEntireFile;
			}
			set
			{
				_diffShowEntireFile = value;
			}
		}

		public bool RevisionWindowDiffShowEntireFile
		{
			get
			{
				return _revisionWindowDiffShowEntireFile;
			}
			set
			{
				_revisionWindowDiffShowEntireFile = value;
			}
		}

		public int DiffContextSize
		{
			get
			{
				return Math.Max(_diffContextSize, 1);
			}
			set
			{
				_diffContextSize = value;
			}
		}

		public bool ImageDiffHighlightPixels
		{
			get
			{
				return _imageDiffHighlightPixels;
			}
			set
			{
				_imageDiffHighlightPixels = value;
			}
		}

		public int PageGuideLinePosition
		{
			get
			{
				return _pageGuideLinePosition;
			}
			set
			{
				_pageGuideLinePosition = value;
			}
		}

		public int CommitSubjectLowLimit
		{
			get
			{
				return _commitSubjectLowLimit;
			}
			set
			{
				_commitSubjectLowLimit = value;
			}
		}

		public int CommitSubjectHighLimit
		{
			get
			{
				return _commitSubjectHighLimit;
			}
			set
			{
				_commitSubjectHighLimit = value;
			}
		}

		public string CommitMessageRegex
		{
			get
			{
				return _commitMessageRegex;
			}
			set
			{
				_commitMessageRegex = value;
			}
		}

		public bool OpenAiLoggedIn
		{
			get
			{
				return _openAiLoggedIn;
			}
			set
			{
				_openAiLoggedIn = value;
			}
		}

		public string AiReviewServiceUrl
		{
			get
			{
				return _aiReviewServiceUrl;
			}
			set
			{
				_aiReviewServiceUrl = value;
			}
		}

		public string AiReviewApiKey
		{
			get
			{
				return _aiReviewApiKey;
			}
			set
			{
				_aiReviewApiKey = value;
			}
		}

		public bool AiReviewAutoFetchModels
		{
			get
			{
				return _aiReviewAutoFetchModels;
			}
			set
			{
				_aiReviewAutoFetchModels = value;
			}
		}

		public string[] AiReviewModels
		{
			get
			{
				return _aiReviewModels ?? new string[0];
			}
			set
			{
				_aiReviewModels = value ?? new string[0];
			}
		}

		public string AiReviewSelectedModel
		{
			get
			{
				return _aiReviewSelectedModel;
			}
			set
			{
				_aiReviewSelectedModel = value;
			}
		}

		public int AiReviewRetryCount
		{
			get
			{
				return _aiReviewRetryCount;
			}
			set
			{
				_aiReviewRetryCount = value;
			}
		}

		public int AiReviewTimeoutSeconds
		{
			get
			{
				return _aiReviewTimeoutSeconds;
			}
			set
			{
				_aiReviewTimeoutSeconds = value;
			}
		}

		public string AiDevSkillContent
		{
			get
			{
				return _aiDevSkillContent;
			}
			set
			{
				_aiDevSkillContent = value;
			}
		}

		public string AiDevSkillList
		{
			get
			{
				return _aiDevSkillList;
			}
			set
			{
				_aiDevSkillList = value;
			}
		}

		public string AiDevSendMode
		{
			get
			{
				return _aiDevSendMode;
			}
			set
			{
				_aiDevSendMode = value;
			}
		}

		public bool PushAutomaticallyOnCommit
		{
			get
			{
				return _pushAutomaticallyOnCommit;
			}
			set
			{
				_pushAutomaticallyOnCommit = value;
			}
		}

		public bool CompactBranchLabels
		{
			get
			{
				return _compactBranchLabels;
			}
			set
			{
				_compactBranchLabels = value;
			}
		}

		public bool DisableSyntaxHighlighting
		{
			get
			{
				return _disableSyntaxHighlighting;
			}
			set
			{
				_disableSyntaxHighlighting = value;
			}
		}

		public int MaxCommitCount
		{
			get
			{
				return _maxCommitCount;
			}
			set
			{
				_maxCommitCount = value;
			}
		}

		public int MinPagesCount => (_maxCommitCount - 1) / 10000 + 1;

		public int LayoutScaling
		{
			get
			{
				return _layoutScaling;
			}
			set
			{
				_layoutScaling = value;
			}
		}

		public MergeType MergeType
		{
			get
			{
				return _mergeType;
			}
			set
			{
				_mergeType = value;
			}
		}

		public bool FetchRemotesAutomatically
		{
			get
			{
				return _fetchRemotesAutomatically;
			}
			set
			{
				_fetchRemotesAutomatically = value;
			}
		}

		public bool FetchAllTags
		{
			get
			{
				return _fetchAllTags;
			}
			set
			{
				_fetchAllTags = value;
			}
		}

		public bool RebaseAutostash
		{
			get
			{
				return _rebaseAutostash;
			}
			set
			{
				_rebaseAutostash = value;
			}
		}

		public bool RebaseUpdateRefs
		{
			get
			{
				return _rebaseUpdateRefs;
			}
			set
			{
				_rebaseUpdateRefs = value;
			}
		}

		public int AutomaticStatusUpdateInterval
		{
			get
			{
				return _automaticStatusUpdateInterval;
			}
			set
			{
				_automaticStatusUpdateInterval = value;
			}
		}

		public CommitSpellCheckingMode CommitSpellCheckingMode
		{
			get
			{
				return _commitSpellCheckingMode;
			}
			set
			{
				_commitSpellCheckingMode = value;
			}
		}

		public string ReferenceSpaceCharacterReplacement
		{
			get
			{
				return _referenceSpaceCharacterReplacement;
			}
			set
			{
				_referenceSpaceCharacterReplacement = value;
			}
		}

		public double CommitViewColumnWidth
		{
			get
			{
				return _commitViewColumnWidth;
			}
			set
			{
				_commitViewColumnWidth = value;
			}
		}

		public double CommitViewCombinedListLocationColumnWidth
		{
			get
			{
				return _commitViewCombinedListLocationColumnWidth;
			}
			set
			{
				_commitViewCombinedListLocationColumnWidth = value;
			}
		}

		public double SidebarColumnWidth
		{
			get
			{
				return _sidebarColumnWidth;
			}
			set
			{
				_sidebarColumnWidth = value;
			}
		}

		public double RevisionDetailsChangesColumnWidth
		{
			get
			{
				return _revisionDetailsChangesColumnWidth;
			}
			set
			{
				_revisionDetailsChangesColumnWidth = value;
			}
		}

		public double RevisionDetailsFileTreeColumnWidth
		{
			get
			{
				return _revisionDetailsFileTreeColumnWidth;
			}
			set
			{
				_revisionDetailsFileTreeColumnWidth = value;
			}
		}

		public double AiResultColumnWidth
		{
			get
			{
				return _aiResultColumnWidth;
			}
			set
			{
				_aiResultColumnWidth = value;
			}
		}

		public double AiReviewFileTreeColumnWidth
		{
			get
			{
				return _aiReviewFileTreeColumnWidth;
			}
			set
			{
				_aiReviewFileTreeColumnWidth = value;
			}
		}

		public double VerticalLayoutRevisionListViewWidth
		{
			get
			{
				return _verticalLayoutRevisionListViewWidth;
			}
			set
			{
				_verticalLayoutRevisionListViewWidth = value;
			}
		}

		public bool ShowWorktrees
		{
			get
			{
				return _showWorktrees;
			}
			set
			{
				_showWorktrees = value;
			}
		}

		public bool UpdateSubmodulesOnCheckout
		{
			get
			{
				return _updateSubmodulesOnCheckout;
			}
			set
			{
				_updateSubmodulesOnCheckout = value;
			}
		}

		public WindowLocationState MainWindowLocationState
		{
			get
			{
				return _mainWindowLocationState ?? new WindowLocationState(100.0, 100.0, 1000.0, 600.0, WindowState.Normal);
			}
			set
			{
				_mainWindowLocationState = value;
			}
		}

		public WindowLocationState RevisionWindowLocationState
		{
			get
			{
				return _revisionWindowLocationState ?? new WindowLocationState(100.0, 100.0, 1080.0, 720.0, WindowState.Normal);
			}
			set
			{
				_revisionWindowLocationState = value;
			}
		}

		public WindowLocationState SideBySideMergeWindowLocationState
		{
			get
			{
				return _sideBySideMergeWindowLocationState ?? new WindowLocationState(100.0, 100.0, 1020.0, 720.0, WindowState.Normal);
			}
			set
			{
				_sideBySideMergeWindowLocationState = value;
			}
		}

		public WindowLocationState BlameWindowLocationState
		{
			get
			{
				return _blameWindowLocationState ?? new WindowLocationState(100.0, 100.0, 1080.0, 720.0, WindowState.Normal);
			}
			set
			{
				_blameWindowLocationState = value;
			}
		}

		public WindowLocationState HistoryWindowLocationState
		{
			get
			{
				return _historyWindowLocationState ?? new WindowLocationState(100.0, 100.0, 1080.0, 720.0, WindowState.Normal);
			}
			set
			{
				_historyWindowLocationState = value;
			}
		}

		public WindowLocationState AiResultWindowLocationState
		{
			get
			{
				return _aiResultWindowLocationState ?? new WindowLocationState(100.0, 100.0, 1080.0, 720.0, WindowState.Normal);
			}
			set
			{
				_aiResultWindowLocationState = value;
			}
		}

		public double RepositoryManagerTreeViewColumnWidth
		{
			get
			{
				return _repositoryManagerTreeViewColumnWidth;
			}
			set
			{
				_repositoryManagerTreeViewColumnWidth = value;
			}
		}

		[Null]
		public ExpandedTreeViewElement[] RepositoryManagerTreeViewExpandedItems
		{
			get
			{
				return _repositoryManagerTreeViewExpandedItems;
			}
			set
			{
				_repositoryManagerTreeViewExpandedItems = value;
			}
		}

		public ThemeType Theme
		{
			get
			{
				return _theme;
			}
			set
			{
				_theme = value;
			}
		}

		public bool FollowSystemTheme
		{
			get
			{
				return _followSystemTheme;
			}
			set
			{
				_followSystemTheme = value;
			}
		}

		public string UiLanguage
		{
			get
			{
				return _uiLanguage;
			}
			set
			{
				_uiLanguage = value;
			}
		}

		public MergerLayoutOrientation MergerLayoutOrientation
		{
			get
			{
				return _mergerOrientation;
			}
			set
			{
				_mergerOrientation = value;
			}
		}

		public RevisionListOrientation RevisionListOrientation
		{
			get
			{
				return _revisionListOrientation;
			}
			set
			{
				_revisionListOrientation = value;
			}
		}

		public RevisionSortOrder RevisionSortOrder
		{
			get
			{
				return _revisionSortOrder;
			}
			set
			{
				_revisionSortOrder = value;
			}
		}

		public FileListMode FileListMode
		{
			get
			{
				return _fileListMode;
			}
			set
			{
				_fileListMode = value;
			}
		}

		public ExternalTool[] ExternalMergeTools
		{
			get
			{
				return _externalMergeTools;
			}
			set
			{
				_externalMergeTools = value;
			}
		}

		public MergeTool MergeTool
		{
			get
			{
				return _mergeTool;
			}
			set
			{
				_mergeTool = value;
			}
		}

		[Null]
		public ExternalTool[] ExternalDiffTools
		{
			get
			{
				return _externalDiffTools;
			}
			set
			{
				_externalDiffTools = value;
			}
		}

		public MergeTool ExternalDiffTool
		{
			get
			{
				return _externalDiffTool;
			}
			set
			{
				_externalDiffTool = value;
			}
		}

		public ShellTool ShellTool
		{
			get
			{
				return _shellTool;
			}
			set
			{
				_shellTool = value;
			}
		}

		public bool ShowBugtrackerLinks
		{
			get
			{
				return _showBugtrackerLinks;
			}
			set
			{
				_showBugtrackerLinks = value;
			}
		}

		public DateTime LastUpdateCheck
		{
			get
			{
				return _lastUpdateCheck;
			}
			set
			{
				_lastUpdateCheck = value;
			}
		}

		public bool CheckForUpdatesAutomatically
		{
			get
			{
				return _checkForUpdatesAutomatically;
			}
			set
			{
				_checkForUpdatesAutomatically = value;
			}
		}

		public int UpdateCheckIntervalHours
		{
			get
			{
				return _updateCheckIntervalHours;
			}
			set
			{
				_updateCheckIntervalHours = value;
			}
		}

		public string SkippedUpdateVersion
		{
			get
			{
				return _skippedUpdateVersion;
			}
			set
			{
				_skippedUpdateVersion = value;
			}
		}

		public string GitInstancePath
		{
			get
			{
				return _gitInstancePath;
			}
			set
			{
				_gitInstancePath = value;
			}
		}

		public string GitMmInstancePath
		{
			get
			{
				return _gitMmInstancePath;
			}
			set
			{
				_gitMmInstancePath = value;
			}
		}

		public bool VerboseGitOutput
		{
			get
			{
				return _verboseGitOutput;
			}
			set
			{
				_verboseGitOutput = value;
			}
		}

		public string[] SshKeys
		{
			get
			{
				return _sshKeys;
			}
			set
			{
				_sshKeys = value;
			}
		}

		public string RecentPatchDirectory
		{
			get
			{
				return _recentPatchDirectory;
			}
			set
			{
				_recentPatchDirectory = value;
			}
		}

		public bool DisableHardwareAcceleration
		{
			get
			{
				return _disableHardwareAcceleration;
			}
			set
			{
				_disableHardwareAcceleration = value;
			}
		}

		public bool MigratedToFork2_10_3
		{
			get
			{
				return _migratedToFork2_10_3;
			}
			set
			{
				_migratedToFork2_10_3 = value;
			}
		}

		public bool SeenNewYear2026
		{
			get
			{
				return _seenNewYear2026;
			}
			set
			{
				_seenNewYear2026 = value;
			}
		}

		public RepositoryManagerSettings RepositoryManager
		{
			get
			{
				return _repositoryManager ?? RepositoryManagerSettings.CreateDefault();
			}
			set
			{
				_repositoryManager = value;
			}
		}

		public WorkspacesSettings Workspaces
		{
			get
			{
				return _workspaces ?? WorkspacesSettings.CreateDefault();
			}
			set
			{
				_workspaces = value;
			}
		}

		public GitMmSettings GitMm
		{
			get
			{
				return _gitMm ?? GitMmSettings.CreateDefault();
			}
			set
			{
				_gitMm = value;
			}
		}

		public bool DisableRefreshOnAppActivation
		{
			get
			{
				return _disableRefreshOnAppActivation;
			}
			set
			{
				_disableRefreshOnAppActivation = value;
			}
		}

		public bool LogElapsedTime
		{
			get
			{
				return _logElapsedTime;
			}
			set
			{
				_logElapsedTime = value;
			}
		}

		public bool LogToActiveNotepadProcess
		{
			get
			{
				return _logToActiveNotepadProcess;
			}
			set
			{
				_logToActiveNotepadProcess = value;
			}
		}

		public string SquirrelReleaseUpdateChannel
		{
			get
			{
				return _squirrelReleaseUpdateChannel;
			}
			set
			{
				_squirrelReleaseUpdateChannel = value;
			}
		}

		private ForkPlusSettings()
		{
		}

		public static ForkPlusSettings Decode(JObject json)
		{
			json = json ?? new JObject();
			string guid = json["Guid"]?.Value<string>() ?? null;
			ApplicationUpdateType applicationUpdateType = (ApplicationUpdateType)(json["ApplicationUpdateType"]?.Value<int>() ?? 0);
			ActivityManagerViewMode activityManagerViewMode = (ActivityManagerViewMode)(json["ActivityManagerViewMode"]?.Value<int>() ?? 1);
			ReferenceSortOrder localBranchSortOrder = (ReferenceSortOrder)(json["LocalBranchSortOrder"]?.Value<int>() ?? 1);
			ReferenceSortOrder remoteBranchSortOrder = (ReferenceSortOrder)(json["RemoteBranchSortOrder"]?.Value<int>() ?? 1);
			ReferenceSortOrder tagSortOrder = (ReferenceSortOrder)(json["TagSortOrder"]?.Value<int>() ?? 2);
			bool createBranch_Checkout = json["CreateBranch_Checkout"]?.Value<bool>() ?? false;
			bool checkout_StashAndReapply = json["Checkout_StashAndReapply"]?.Value<bool>() ?? false;
			bool checkoutAndSync_StashAndReapply = json["CheckoutAndSync_StashAndReapply"]?.Value<bool>() ?? false;
			bool cherryPick_AppendOriginSha = json["CherryPick_AppendOriginSha"]?.Value<bool>() ?? false;
			bool fetch_FetchAllRemotes = json["Fetch_FetchAllRemotes"]?.Value<bool>() ?? false;
			bool pull_Rebase = json["Pull_Rebase"]?.Value<bool>() ?? false;
			bool pull_StashAndReapply = json["Pull_StashAndReapply"]?.Value<bool>() ?? false;
			bool push_PushAllTags = json["Push_PushAllTags"]?.Value<bool>() ?? false;
			bool createTag_Push = json["CreateTag_Push"]?.Value<bool>() ?? false;
			bool saveStash_StageNewFiles = json["SaveStash_StageNewFiles"]?.Value<bool>() ?? false;
			bool applyStash_DeleteAfterApply = json["ApplyStash_DeleteAfterApply"]?.Value<bool>() ?? false;
			bool interactiveRebase_CreateBackup = json["InteractiveRebase_CreateBackup"]?.Value<bool>() ?? false;
			bool interactiveRebase_UpdateRefs = json["InteractiveRebase_UpdateRefs"]?.Value<bool>() ?? false;
			bool gitFlowFinishFeature_DeleteBranches = json["GitFlowFinishFeature_DeleteBranches"]?.Value<bool>() ?? true;
			bool gitFlowFinishFeature_Rebase = json["GitFlowFinishFeature_Rebase"]?.Value<bool>() ?? false;
			bool gitFlowFinishFeature_NoFastForward = json["GitFlowFinishFeature_NoFastForward"]?.Value<bool>() ?? false;
			bool gitFlowFinishRelease_DeleteBranches = json["GitFlowFinishRelease_DeleteBranches"]?.Value<bool>() ?? true;
			bool gitFlowFinishRelease_BackMergeMaster = json["GitFlowFinishRelease_BackMergeMaster"]?.Value<bool>() ?? true;
			bool gitFlowFinishHotfix_DeleteBranches = json["GitFlowFinishHotfix_DeleteBranches"]?.Value<bool>() ?? true;
			DiffLayoutMode commitDiffLayoutMode = (DiffLayoutMode)(json["CommitDiffLayoutMode"]?.Value<int>() ?? 0);
			DiffLayoutMode historyDiffLayoutMode = (DiffLayoutMode)(json["HistoryDiffLayoutMode"]?.Value<int>() ?? 0);
			DiffLayoutMode popupDiffLayoutMode = (DiffLayoutMode)(json["PopupDiffLayoutMode"]?.Value<int>() ?? 1);
			DiffLayoutMode revisionDiffLayoutMode = (DiffLayoutMode)(json["RevisionDiffLayoutMode"]?.Value<int>() ?? json["DiffLayoutMode"]?.Value<int>() ?? 0);
			DiffLayoutMode revisionWindowDiffLayoutMode = (DiffLayoutMode)(json["RevisionWindowDiffLayoutMode"]?.Value<int>() ?? 0);
			bool diffIgnoreWhitespaces = json["DiffIgnoreWhitespaces"]?.Value<bool>() ?? false;
			bool diffShowHiddenSymbols = json["DiffShowHiddenSymbols"]?.Value<bool>() ?? false;
			bool diffWordWrap = json["DiffWordWrap"]?.Value<bool>() ?? false;
			double codeEditorFontSize = json["CodeEditorFontSize"]?.Value<double>() ?? 13.0;
			bool diffShowChangeMarks = json["DiffShowChangeMarks"]?.Value<bool>() ?? false;
			bool diffShowEntireFile = json["DiffShowEntireFile"]?.Value<bool>() ?? false;
			bool revisionWindowDiffShowEntireFile = json["RevisionWindowDiffShowEntireFile"]?.Value<bool>() ?? false;
			int diffContextSize = json["DiffContextSize"]?.Value<int>() ?? 3;
			bool imageDiffHighlightPixels = json["ImageDiffHighlightPixels"]?.Value<bool>() ?? false;
			int pageGuideLinePosition = json["PageGuideLinePosition"]?.Value<int>() ?? 72;
			int commitSubjectLowLimit = json["CommitSubjectLowLimit"]?.Value<int>() ?? 50;
			int commitSubjectHighLimit = json["CommitSubjectHighLimit"]?.Value<int>() ?? 70;
			string commitMessageRegex = json["CommitMessageRegex"]?.Value<string>() ?? "";
			bool openAiLoggedIn = json["OpenAiLoggedIn"]?.Value<bool>() ?? false;
			string aiReviewServiceUrl = json["AiReviewServiceUrl"]?.Value<string>() ?? "https://api.openai.com";
			string aiReviewApiKey = json["AiReviewApiKey"]?.Value<string>() ?? "";
			bool aiReviewAutoFetchModels = json["AiReviewAutoFetchModels"]?.Value<bool>() ?? true;
			string[] aiReviewModels = JsonHelper.DecodeStringArray(json["AiReviewModels"] as JArray) ?? new string[0];
			string aiReviewSelectedModel = json["AiReviewSelectedModel"]?.Value<string>() ?? "";
			int aiReviewRetryCount = json["AiReviewRetryCount"]?.Value<int>() ?? 3;
			int aiReviewTimeoutSeconds = json["AiReviewTimeoutSeconds"]?.Value<int>() ?? 300;
			string aiDevSkillContent = json["AiDevSkillContent"]?.Value<string>() ?? "";
			string aiDevSkillList = json["AiDevSkillList"]?.Value<string>() ?? "";
			string aiDevSendMode = json["AiDevSendMode"]?.Value<string>() ?? "Enter";
			bool pushAutomaticallyOnCommit = json["PushAutomaticallyOnCommit"]?.Value<bool>() ?? false;
			bool compactBranchLabels = json["CompactBranchLabels"]?.Value<bool>() ?? true;
			bool disableSyntaxHighlighting = json["DisableSyntaxHighlighting"]?.Value<bool>() ?? false;
			int maxCommitCount = json["MaxCommitCount"]?.Value<int>() ?? 50000;
			int layoutScaling = json["LayoutScaling"]?.Value<int>() ?? 100;
			MergeType mergeType = (MergeType)(json["MergeType"]?.Value<int>() ?? 0);
			bool fetchRemotesAutomatically = json["FetchRemotesAutomatically"]?.Value<bool>() ?? true;
			bool fetchAllTags = json["FetchAllTags"]?.Value<bool>() ?? false;
			bool rebaseAutostash = json["RebaseAutostash"]?.Value<bool>() ?? false;
			bool rebaseUpdateRefs = json["RebaseUpdateRefs"]?.Value<bool>() ?? false;
			int automaticStatusUpdateInterval = json["AutomaticStatusUpdateInterval"]?.Value<int>() ?? 60;
			CommitSpellCheckingMode commitSpellCheckingMode = (CommitSpellCheckingMode)(json["CommitSpellCheckingMode"]?.Value<int>() ?? 0);
			double commitViewColumnWidth = json["CommitViewColumnWidth"]?.Value<double>() ?? 250.0;
			double commitViewCombinedListLocationColumnWidth = json["CommitViewCombinedListLocationColumnWidth"]?.Value<double>() ?? 120.0;
			string referenceSpaceCharacterReplacement = CustomDecoders.DecodeReferenceSpaceCharacterReplacement(json["ReferenceSpaceCharacterReplacement"]);
			double sidebarColumnWidth = json["SidebarColumnWidth"]?.Value<double>() ?? json["SideBarColumnWidth"]?.Value<double>() ?? 300.0;
			double revisionDetailsChangesColumnWidth = json["RevisionDetailsChangesColumnWidth"]?.Value<double>() ?? 280.0;
			double revisionDetailsFileTreeColumnWidth = json["RevisionDetailsFileTreeColumnWidth"]?.Value<double>() ?? 220.0;
			double aiResultColumnWidth = json["AiResultColumnWidth"]?.Value<double>() ?? 350.0;
			double aiReviewFileTreeColumnWidth = json["AiReviewFileTreeColumnWidth"]?.Value<double>() ?? 320.0;
			double verticalLayoutRevisionListViewWidth = json["VerticalLayoutRevisionListViewWidth"]?.Value<double>() ?? 350.0;
			bool showWorktrees = json["ShowWorktrees"]?.Value<bool>() ?? false;
			bool updateSubmodulesOnCheckout = json["UpdateSubmodulesOnCheckout"]?.Value<bool>() ?? true;
			WindowLocationState mainWindowLocationState = CustomDecoders.DecodeWindowLocationState(json["MainWindowLocationState"] as JObject) ?? new WindowLocationState(100.0, 100.0, 1000.0, 600.0, WindowState.Normal);
			WindowLocationState revisionWindowLocationState = CustomDecoders.DecodeWindowLocationState(json["RevisionWindowLocationState"] as JObject) ?? new WindowLocationState(100.0, 100.0, 1000.0, 600.0, WindowState.Normal);
			WindowLocationState sideBySideMergeWindowLocationState = CustomDecoders.DecodeWindowLocationState(json["SideBySideMergeWindowLocationState"] as JObject) ?? new WindowLocationState(100.0, 100.0, 1020.0, 720.0, WindowState.Normal);
			WindowLocationState blameWindowLocationState = CustomDecoders.DecodeWindowLocationState(json["BlameWindowLocationState"] as JObject) ?? new WindowLocationState(100.0, 100.0, 1020.0, 720.0, WindowState.Normal);
			WindowLocationState historyWindowLocationState = CustomDecoders.DecodeWindowLocationState(json["HistoryWindowLocationState"] as JObject) ?? new WindowLocationState(100.0, 100.0, 1020.0, 720.0, WindowState.Normal);
			WindowLocationState aiResultWindowLocationState = CustomDecoders.DecodeWindowLocationState(json["AiResultWindowLocationState"] as JObject) ?? new WindowLocationState(100.0, 100.0, 1080.0, 720.0, WindowState.Normal);
			ExpandedTreeViewElement[] repositoryManagerTreeViewExpandedItems = ExpandedTreeViewElement.Coder.DecodeExpandedTreeViewElementArray(json["RepositoryManagerTreeViewExpandedItems"] as JArray) ?? ExpandedTreeViewElement.Coder.Decode(json["RepositoryManagerTreeViewExpandedItems"] as JObject)?.Children;
			double repositoryManagerTreeViewColumnWidth = json["RepositoryManagerTreeViewColumnWidth"]?.Value<double>() ?? 350.0;
			ExternalTool[] externalMergeTools = JsonHelper.DecodeArray(json["ExternalMergeTools"] as JArray, ExternalTool.Coder.Decode) ?? TryImportOldTool(CustomDecoders.Decode(json["MergeTool"] as JObject), isDiffTool: false) ?? new ExternalTool[0];
			MergeTool mergeTool = CustomDecoders.Decode(json["MergeTool"] as JObject) ?? new MergeTool.Custom(string.Empty, string.Empty);
			ExternalTool[] externalDiffTools = JsonHelper.DecodeArray(json["ExternalDiffTools"] as JArray, ExternalTool.Coder.Decode) ?? TryImportOldTool(CustomDecoders.Decode(json["ExternalDiffTool"] as JObject), isDiffTool: true) ?? new ExternalTool[0];
			MergeTool externalDiffTool = CustomDecoders.Decode(json["ExternalDiffTool"] as JObject) ?? new MergeTool.Custom(string.Empty, string.Empty);
			ShellTool shellTool = CustomDecoders.DecodeShellTool(json["ShellTool"] as JObject) ?? new ShellTool.Default();
			bool showBugtrackerLinks = json["ShowBugtrackerLinks"]?.Value<bool>() ?? true;
			RevisionSortOrder revisionSortOrder = (RevisionSortOrder)(json["RevisionSortOrder"]?.Value<int>() ?? 1);
			FileListMode fileListMode = (FileListMode)(json["FileListMode"]?.Value<int>() ?? 1);
			ThemeType theme = (ThemeType)(json["Theme"]?.Value<int>() ?? 0);
			bool followSystemTheme = json["FollowSystemTheme"]?.Value<bool>() ?? true;
			string uiLanguage = json["UiLanguage"]?.Value<string>() ?? "zh-Hans";
			RevisionListOrientation revisionListOrientation = (RevisionListOrientation)(json["RevisionListOrientation"]?.Value<int>() ?? 1);
			MergerLayoutOrientation mergerLayoutOrientation = (MergerLayoutOrientation)(json["MergerLayoutOrientation"]?.Value<int>() ?? 0);
			DateTime lastUpdateCheck = json["LastUpdateCheck"]?.Value<DateTime>() ?? DateTime.Today.AddMonths(-1);
			bool checkForUpdatesAutomatically = json["CheckForUpdatesAutomatically"]?.Value<bool>() ?? true;
			int updateCheckIntervalHours = json["UpdateCheckIntervalHours"]?.Value<int>() ?? 24;
			string skippedUpdateVersion = json["SkippedUpdateVersion"]?.Value<string>() ?? "";
			string gitInstancePath = json["GitInstancePath"]?.Value<string>();
			string gitMmInstancePath = json["GitMmInstancePath"]?.Value<string>();
			bool verboseGitOutput = json["VerboseGitOutput"]?.Value<bool>() ?? false;
			string[] sshKeys = JsonHelper.DecodeStringArray(json["SshKeys"] as JArray) ?? new string[0];
			string recentPatchDirectory = json["RecentPatchDirectory"]?.Value<string>();
			bool disableHardwareAcceleration = json["DisableHardwareAcceleration"]?.Value<bool>() ?? false;
			bool migratedToFork2_10_ = json["MigratedToFork2_10_3"]?.Value<bool>() ?? false;
			bool seenNewYear = json["SeenNewYear2026"]?.Value<bool>() ?? false;
			RepositoryManagerSettings repositoryManager = RepositoryManagerSettings.Coder.Decode(json["RepositoryManager"]) ?? RepositoryManagerSettings.CreateDefault();
			WorkspacesSettings workspaces = WorkspacesSettings.Coder.Decode(json["Workspaces"] as JObject) ?? WorkspacesSettings.CreateDefault();
			GitMmSettings gitMm = GitMmSettings.Coder.Decode(json["GitMm"] as JObject) ?? GitMmSettings.CreateDefault();
			return new ForkPlusSettings
			{
				Guid = guid,
				ApplicationUpdateType = applicationUpdateType,
				ActivityManagerViewMode = activityManagerViewMode,
				LocalBranchSortOrder = localBranchSortOrder,
				RemoteBranchSortOrder = remoteBranchSortOrder,
				TagSortOrder = tagSortOrder,
				CreateBranch_Checkout = createBranch_Checkout,
				Checkout_StashAndReapply = checkout_StashAndReapply,
				CheckoutAndSync_StashAndReapply = checkoutAndSync_StashAndReapply,
				CherryPick_AppendOriginSha = cherryPick_AppendOriginSha,
				Fetch_FetchAllRemotes = fetch_FetchAllRemotes,
				Pull_Rebase = pull_Rebase,
				Pull_StashAndReapply = pull_StashAndReapply,
				Push_PushAllTags = push_PushAllTags,
				CreateTag_Push = createTag_Push,
				SaveStash_StageNewFiles = saveStash_StageNewFiles,
				ApplyStash_DeleteAfterApply = applyStash_DeleteAfterApply,
				InteractiveRebase_CreateBackup = interactiveRebase_CreateBackup,
				InteractiveRebase_UpdateRefs = interactiveRebase_UpdateRefs,
				GitFlowFinishFeature_DeleteBranches = gitFlowFinishFeature_DeleteBranches,
				GitFlowFinishFeature_Rebase = gitFlowFinishFeature_Rebase,
				GitFlowFinishFeature_NoFastForward = gitFlowFinishFeature_NoFastForward,
				GitFlowFinishRelease_DeleteBranches = gitFlowFinishRelease_DeleteBranches,
				GitFlowFinishRelease_BackMergeMaster = gitFlowFinishRelease_BackMergeMaster,
				GitFlowFinishHotfix_DeleteBranches = gitFlowFinishHotfix_DeleteBranches,
				CommitDiffLayoutMode = commitDiffLayoutMode,
				HistoryDiffLayoutMode = historyDiffLayoutMode,
				PopupDiffLayoutMode = popupDiffLayoutMode,
				RevisionDiffLayoutMode = revisionDiffLayoutMode,
				RevisionWindowDiffLayoutMode = revisionWindowDiffLayoutMode,
				DiffIgnoreWhitespaces = diffIgnoreWhitespaces,
				DiffShowHiddenSymbols = diffShowHiddenSymbols,
				DiffWordWrap = diffWordWrap,
				CodeEditorFontSize = codeEditorFontSize,
				DiffShowChangeMarks = diffShowChangeMarks,
				DiffShowEntireFile = diffShowEntireFile,
				RevisionWindowDiffShowEntireFile = revisionWindowDiffShowEntireFile,
				DiffContextSize = diffContextSize,
				ImageDiffHighlightPixels = imageDiffHighlightPixels,
				PageGuideLinePosition = pageGuideLinePosition,
				CommitSubjectLowLimit = commitSubjectLowLimit,
				CommitSubjectHighLimit = commitSubjectHighLimit,
				CommitMessageRegex = commitMessageRegex,
				OpenAiLoggedIn = openAiLoggedIn,
				AiReviewServiceUrl = aiReviewServiceUrl,
				AiReviewApiKey = aiReviewApiKey,
				AiReviewAutoFetchModels = aiReviewAutoFetchModels,
				AiReviewModels = aiReviewModels,
				AiReviewSelectedModel = aiReviewSelectedModel,
				AiReviewRetryCount = aiReviewRetryCount,
				AiReviewTimeoutSeconds = aiReviewTimeoutSeconds,
				AiDevSkillContent = aiDevSkillContent,
				AiDevSkillList = aiDevSkillList,
				AiDevSendMode = aiDevSendMode,
				PushAutomaticallyOnCommit = pushAutomaticallyOnCommit,
				CompactBranchLabels = compactBranchLabels,
				DisableSyntaxHighlighting = disableSyntaxHighlighting,
				MaxCommitCount = maxCommitCount,
				LayoutScaling = layoutScaling,
				MergeType = mergeType,
				FetchRemotesAutomatically = fetchRemotesAutomatically,
				FetchAllTags = fetchAllTags,
				RebaseAutostash = rebaseAutostash,
				RebaseUpdateRefs = rebaseUpdateRefs,
				AutomaticStatusUpdateInterval = automaticStatusUpdateInterval,
				CommitSpellCheckingMode = commitSpellCheckingMode,
				ReferenceSpaceCharacterReplacement = referenceSpaceCharacterReplacement,
				CommitViewColumnWidth = commitViewColumnWidth,
				CommitViewCombinedListLocationColumnWidth = commitViewCombinedListLocationColumnWidth,
				SidebarColumnWidth = sidebarColumnWidth,
				RevisionDetailsChangesColumnWidth = revisionDetailsChangesColumnWidth,
				RevisionDetailsFileTreeColumnWidth = revisionDetailsFileTreeColumnWidth,
				AiResultColumnWidth = aiResultColumnWidth,
				AiReviewFileTreeColumnWidth = aiReviewFileTreeColumnWidth,
				VerticalLayoutRevisionListViewWidth = verticalLayoutRevisionListViewWidth,
				ShowWorktrees = showWorktrees,
				UpdateSubmodulesOnCheckout = updateSubmodulesOnCheckout,
				MainWindowLocationState = mainWindowLocationState,
				RevisionWindowLocationState = revisionWindowLocationState,
				SideBySideMergeWindowLocationState = sideBySideMergeWindowLocationState,
				BlameWindowLocationState = blameWindowLocationState,
				HistoryWindowLocationState = historyWindowLocationState,
				AiResultWindowLocationState = aiResultWindowLocationState,
				RepositoryManagerTreeViewExpandedItems = repositoryManagerTreeViewExpandedItems,
				RepositoryManagerTreeViewColumnWidth = repositoryManagerTreeViewColumnWidth,
				RevisionSortOrder = revisionSortOrder,
				FileListMode = fileListMode,
				ExternalMergeTools = externalMergeTools,
				MergeTool = mergeTool,
				ExternalDiffTools = externalDiffTools,
				ExternalDiffTool = externalDiffTool,
				ShellTool = shellTool,
				ShowBugtrackerLinks = showBugtrackerLinks,
				Theme = theme,
				FollowSystemTheme = followSystemTheme,
				UiLanguage = uiLanguage,
				RevisionListOrientation = revisionListOrientation,
				MergerLayoutOrientation = mergerLayoutOrientation,
				LastUpdateCheck = lastUpdateCheck,
				CheckForUpdatesAutomatically = checkForUpdatesAutomatically,
				UpdateCheckIntervalHours = updateCheckIntervalHours,
				SkippedUpdateVersion = skippedUpdateVersion,
				GitInstancePath = gitInstancePath,
				GitMmInstancePath = gitMmInstancePath,
				VerboseGitOutput = verboseGitOutput,
				SshKeys = sshKeys,
				RecentPatchDirectory = recentPatchDirectory,
				DisableHardwareAcceleration = disableHardwareAcceleration,
				MigratedToFork2_10_3 = migratedToFork2_10_,
				SeenNewYear2026 = seenNewYear,
				RepositoryManager = repositoryManager,
				Workspaces = workspaces,
				GitMm = gitMm,
				DisableRefreshOnAppActivation = false,
				LogElapsedTime = false,
				LogToActiveNotepadProcess = false
			};
		}

		[Null]
		private static ExternalTool[] TryImportOldTool([Null] MergeTool tool, bool isDiffTool)
		{
			if (tool == null)
			{
				return null;
			}
			string name = null;
			string path = null;
			string arguments = null;
			bool? isPrimary = null;
			bool? isVisible = null;
			ToolType type;
			switch (tool.Type)
			{
			case "Custom":
				type = ToolType.Custom;
				path = tool.ApplicationPath;
				arguments = (isDiffTool ? tool.DiffArguments : tool.Arguments);
				if (path == string.Empty || arguments == string.Empty)
				{
					return null;
				}
				try
				{
					name = Path.GetFileNameWithoutExtension(path);
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to import custom tool '" + path + "'", ex);
					return null;
				}
				isPrimary = true;
				return new ExternalTool[1]
				{
					new ExternalTool(type, name, path, arguments, isPrimary, isVisible)
				};
			case "AraxisMerge":
				type = ToolType.AraxisMerge;
				break;
			case "BeyondCompare":
				type = ToolType.BeyondCompare;
				break;
			case "KDiff3":
				type = ToolType.KDiff3;
				break;
			case "P4Merge":
				type = ToolType.P4Merge;
				break;
			case "VSCode":
				type = ToolType.VSCode;
				break;
			case "VisualStudio":
				type = ToolType.VisualStudio;
				break;
			case "UnityYAMLMerge":
				type = ToolType.Unity3d;
				break;
			case "WinMerge":
				type = ToolType.WinMerge;
				break;
			default:
				return null;
			}
			ToolDefinition? toolDefinition = (isDiffTool ? ExternalToolManager.DiffToolDefinitions : ExternalToolManager.MergeToolDefinitions).FirstItemStruct((ToolDefinition x) => x.Type == type);
			if (toolDefinition.HasValue)
			{
				ToolDefinition valueOrDefault = toolDefinition.GetValueOrDefault();
				if (ExternalToolManager.GetPredefinedToolPath(valueOrDefault) != null)
				{
					isPrimary = true;
				}
				else if (File.Exists(tool.ApplicationPath))
				{
					path = tool.ApplicationPath;
					isPrimary = true;
				}
				return new ExternalTool[1]
				{
					new ExternalTool(type, name, path, arguments, isPrimary, isVisible)
				};
			}
			Log.Warn($"Failed to import predefined tool '{type}'");
			return null;
		}

		public static JObject Encode(ForkPlusSettings target)
		{
			return new JObject
			{
				{
					"Guid",
					new JValue(target.Guid)
				},
				{
					"ApplicationUpdateType",
					new JValue((long)target.ApplicationUpdateType)
				},
				{
					"ActivityManagerViewMode",
					new JValue((long)target.ActivityManagerViewMode)
				},
				{
					"LocalBranchSortOrder",
					new JValue((long)target.LocalBranchSortOrder)
				},
				{
					"RemoteBranchSortOrder",
					new JValue((long)target.RemoteBranchSortOrder)
				},
				{
					"TagSortOrder",
					new JValue((long)target.TagSortOrder)
				},
				{
					"Fetch_FetchAllRemotes",
					new JValue(target.Fetch_FetchAllRemotes)
				},
				{
					"Pull_Rebase",
					new JValue(target.Pull_Rebase)
				},
				{
					"Pull_StashAndReapply",
					new JValue(target.Pull_StashAndReapply)
				},
				{
					"Push_PushAllTags",
					new JValue(target.Push_PushAllTags)
				},
				{
					"CreateTag_Push",
					new JValue(target.CreateTag_Push)
				},
				{
					"CreateBranch_Checkout",
					new JValue(target.CreateBranch_Checkout)
				},
				{
					"Checkout_StashAndReapply",
					new JValue(target.Checkout_StashAndReapply)
				},
				{
					"CheckoutAndSync_StashAndReapply",
					new JValue(target.CheckoutAndSync_StashAndReapply)
				},
				{
					"SaveStash_StageNewFiles",
					new JValue(target.SaveStash_StageNewFiles)
				},
				{
					"ApplyStash_DeleteAfterApply",
					new JValue(target.ApplyStash_DeleteAfterApply)
				},
				{
					"InteractiveRebase_CreateBackup",
					new JValue(target.InteractiveRebase_CreateBackup)
				},
				{
					"InteractiveRebase_UpdateRefs",
					new JValue(target.InteractiveRebase_UpdateRefs)
				},
				{
					"GitFlowFinishFeature_DeleteBranches",
					new JValue(target.GitFlowFinishFeature_DeleteBranches)
				},
				{
					"GitFlowFinishFeature_Rebase",
					new JValue(target.GitFlowFinishFeature_Rebase)
				},
				{
					"GitFlowFinishFeature_NoFastForward",
					new JValue(target.GitFlowFinishFeature_NoFastForward)
				},
				{
					"GitFlowFinishRelease_DeleteBranches",
					new JValue(target.GitFlowFinishRelease_DeleteBranches)
				},
				{
					"GitFlowFinishRelease_BackMergeMaster",
					new JValue(target.GitFlowFinishRelease_BackMergeMaster)
				},
				{
					"GitFlowFinishHotfix_DeleteBranches",
					new JValue(target.GitFlowFinishHotfix_DeleteBranches)
				},
				{
					"RevisionDiffLayoutMode",
					new JValue((long)target.RevisionDiffLayoutMode)
				},
				{
					"RevisionWindowDiffLayoutMode",
					new JValue((long)target.RevisionWindowDiffLayoutMode)
				},
				{
					"PopupDiffLayoutMode",
					new JValue((long)target.PopupDiffLayoutMode)
				},
				{
					"CommitDiffLayoutMode",
					new JValue((long)target.CommitDiffLayoutMode)
				},
				{
					"HistoryDiffLayoutMode",
					new JValue((long)target.HistoryDiffLayoutMode)
				},
				{
					"DiffIgnoreWhitespaces",
					new JValue(target.DiffIgnoreWhitespaces)
				},
				{
					"DiffShowHiddenSymbols",
					new JValue(target.DiffShowHiddenSymbols)
				},
				{
					"DiffWordWrap",
					new JValue(target.DiffWordWrap)
				},
				{
					"CodeEditorFontSize",
					new JValue(target.CodeEditorFontSize)
				},
				{
					"DiffShowChangeMarks",
					new JValue(target.DiffShowChangeMarks)
				},
				{
					"DiffShowEntireFile",
					new JValue(target.DiffShowEntireFile)
				},
				{
					"RevisionWindowDiffShowEntireFile",
					new JValue(target.RevisionWindowDiffShowEntireFile)
				},
				{
					"DiffContextSize",
					new JValue(target.DiffContextSize)
				},
				{
					"ImageDiffHighlightPixels",
					new JValue(target.ImageDiffHighlightPixels)
				},
				{
					"PageGuideLinePosition",
					new JValue(target.PageGuideLinePosition)
				},
				{
					"CommitSubjectLowLimit",
					new JValue(target.CommitSubjectLowLimit)
				},
				{
					"CommitSubjectHighLimit",
					new JValue(target.CommitSubjectHighLimit)
				},
				{
					"CommitMessageRegex",
					new JValue(target.CommitMessageRegex)
				},
				{
					"OpenAiLoggedIn",
					new JValue(target.OpenAiLoggedIn)
				},
				{
					"AiReviewServiceUrl",
					new JValue(target.AiReviewServiceUrl)
				},
				{
					"AiReviewApiKey",
					new JValue(target.AiReviewApiKey)
				},
				{
					"AiReviewAutoFetchModels",
					new JValue(target.AiReviewAutoFetchModels)
				},
				{
					"AiReviewModels",
					JsonHelper.EncodeStringArray(target.AiReviewModels)
				},
				{
					"AiReviewSelectedModel",
					new JValue(target.AiReviewSelectedModel)
				},
				{
					"AiReviewRetryCount",
					new JValue(target.AiReviewRetryCount)
				},
				{
					"AiReviewTimeoutSeconds",
					new JValue(target.AiReviewTimeoutSeconds)
				},
			{
				"AiDevSkillContent",
				new JValue(target.AiDevSkillContent)
			},
			{
				"AiDevSkillList",
				new JValue(target.AiDevSkillList)
			},
			{
				"AiDevSendMode",
				new JValue(target.AiDevSendMode)
			},
			{
				"PushAutomaticallyOnCommit",
				new JValue(target.PushAutomaticallyOnCommit)
			},
				{
					"CompactBranchLabels",
					new JValue(target.CompactBranchLabels)
				},
				{
				"DisableSyntaxHighlighting",
				new JValue(target.DisableSyntaxHighlighting)
			},
			{
				"MaxCommitCount",
				new JValue(target.MaxCommitCount)
			},
				{
					"LayoutScaling",
					new JValue(target.LayoutScaling)
				},
				{
					"MergeType",
					new JValue((long)target.MergeType)
				},
				{
					"FetchRemotesAutomatically",
					new JValue(target.FetchRemotesAutomatically)
				},
				{
					"FetchAllTags",
					new JValue(target.FetchAllTags)
				},
				{
					"RebaseAutostash",
					new JValue(target.RebaseAutostash)
				},
				{
					"RebaseUpdateRefs",
					new JValue(target.RebaseUpdateRefs)
				},
				{
					"AutomaticStatusUpdateInterval",
					new JValue(target.AutomaticStatusUpdateInterval)
				},
				{
					"CommitSpellCheckingMode",
					new JValue((long)target.CommitSpellCheckingMode)
				},
				{
					"ReferenceSpaceCharacterReplacement",
					new JValue(target.ReferenceSpaceCharacterReplacement)
				},
				{
					"CommitViewColumnWidth",
					new JValue(target.CommitViewColumnWidth)
				},
				{
					"CommitViewCombinedListLocationColumnWidth",
					new JValue(target.CommitViewCombinedListLocationColumnWidth)
				},
				{
					"SidebarColumnWidth",
					new JValue(target.SidebarColumnWidth)
				},
				{
					"RevisionDetailsChangesColumnWidth",
					new JValue(target.RevisionDetailsChangesColumnWidth)
				},
				{
					"RevisionDetailsFileTreeColumnWidth",
					new JValue(target.RevisionDetailsFileTreeColumnWidth)
				},
				{
					"AiResultColumnWidth",
					new JValue(target.AiResultColumnWidth)
				},
				{
					"AiReviewFileTreeColumnWidth",
					new JValue(target.AiReviewFileTreeColumnWidth)
				},
				{
					"VerticalLayoutRevisionListViewWidth",
					new JValue(target.VerticalLayoutRevisionListViewWidth)
				},
				{
					"ShowWorktrees",
					new JValue(target.ShowWorktrees)
				},
				{
					"UpdateSubmodulesOnCheckout",
					new JValue(target.UpdateSubmodulesOnCheckout)
				},
				{
					"MainWindowLocationState",
					CustomDecoders.Encode(target.MainWindowLocationState)
				},
				{
					"RevisionWindowLocationState",
					CustomDecoders.Encode(target.RevisionWindowLocationState)
				},
				{
					"SideBySideMergeWindowLocationState",
					CustomDecoders.Encode(target.SideBySideMergeWindowLocationState)
				},
				{
					"BlameWindowLocationState",
					CustomDecoders.Encode(target.BlameWindowLocationState)
				},
				{
					"HistoryWindowLocationState",
					CustomDecoders.Encode(target.HistoryWindowLocationState)
				},
				{
					"AiResultWindowLocationState",
					CustomDecoders.Encode(target.AiResultWindowLocationState)
				},
				{
					"RepositoryManagerTreeViewExpandedItems",
					ExpandedTreeViewElement.Coder.EncodeExpandedTreeViewElementArray(target.RepositoryManagerTreeViewExpandedItems)
				},
				{
					"RepositoryManagerTreeViewColumnWidth",
					new JValue(target.RepositoryManagerTreeViewColumnWidth)
				},
				{
					"RevisionSortOrder",
					new JValue((long)target.RevisionSortOrder)
				},
				{
					"FileListMode",
					new JValue((long)target.FileListMode)
				},
				{
					"ExternalMergeTools",
					JsonHelper.EncodeArray(target.ExternalMergeTools, ExternalTool.Coder.Encode)
				},
				{
					"MergeTool",
					CustomDecoders.Encode(target.MergeTool)
				},
				{
					"ExternalDiffTools",
					JsonHelper.EncodeArray(target.ExternalDiffTools, ExternalTool.Coder.Encode)
				},
				{
					"ExternalDiffTool",
					CustomDecoders.Encode(target.ExternalDiffTool)
				},
				{
					"ShellTool",
					CustomDecoders.EncodeShellTool(target.ShellTool)
				},
				{
					"Theme",
					new JValue((long)target.Theme)
				},
				{
					"FollowSystemTheme",
					new JValue(target.FollowSystemTheme)
				},
				{
					"UiLanguage",
					new JValue(target.UiLanguage)
				},
				{
					"RevisionListOrientation",
					new JValue((long)target.RevisionListOrientation)
				},
				{
					"MergerLayoutOrientation",
					new JValue((long)target.MergerLayoutOrientation)
				},
				{
					"LastUpdateCheck",
					new JValue(target.LastUpdateCheck)
				},
				{
					"CheckForUpdatesAutomatically",
					new JValue(target.CheckForUpdatesAutomatically)
				},
				{
					"UpdateCheckIntervalHours",
					new JValue(target.UpdateCheckIntervalHours)
				},
				{
					"SkippedUpdateVersion",
					new JValue(target.SkippedUpdateVersion)
				},
				{
					"GitInstancePath",
					new JValue(target.GitInstancePath)
				},
				{
					"GitMmInstancePath",
					new JValue(target.GitMmInstancePath)
				},
				{
					"VerboseGitOutput",
					new JValue(target.VerboseGitOutput)
				},
				{
					"SshKeys",
					JsonHelper.EncodeStringArray(target.SshKeys)
				},
				{
					"RecentPatchDirectory",
					new JValue(target.RecentPatchDirectory)
				},
				{
					"DisableHardwareAcceleration",
					new JValue(target.DisableHardwareAcceleration)
				},
				{
					"MigratedToFork2_10_3",
					new JValue(target.MigratedToFork2_10_3)
				},
				{
					"SeenNewYear2026",
					new JValue(target.SeenNewYear2026)
				},
				{
					"RepositoryManager",
					RepositoryManagerSettings.Coder.Encode(target.RepositoryManager)
				},
				{
					"Workspaces",
					WorkspacesSettings.Coder.Encode(target.Workspaces)
				},
				{
					"GitMm",
					GitMmSettings.Coder.Encode(target.GitMm)
				}
			};
		}

		public static ForkPlusSettings Load()
		{
			if (DesignTimeHelper.IsInDesignMode())
			{
				Log.Info("Use design-time settings");
				return Decode(new JObject());
			}
			string path = Path.Combine(App.ForkDirectoryPath, "settings.json");
			try
			{
				if (File.Exists(path))
				{
					_fsMutex.WaitOne();
					string value;
					try
					{
						value = File.ReadAllText(path);
					}
					catch
					{
						return Decode(new JObject());
					}
					finally
					{
						_fsMutex.ReleaseMutex();
					}
					return Decode(JsonConvert.DeserializeObject(value) as JObject);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to load settings", ex);
			}
			Log.Info("Use default settings");
			return Decode(new JObject());
		}

		public void Save()
		{
			if (DesignTimeHelper.IsInDesignMode())
			{
				return;
			}
			Log.Debug("Saving settings");
			try
			{
				string content = Encode(this).ToString(Formatting.Indented);
				Directory.CreateDirectory(App.ForkDirectoryPath);
				_fsMutex.WaitOne();
				try
				{
					FileHelper.AtomicWrite(Path.Combine(App.ForkDirectoryPath, "settings.json"), content);
				}
				catch (Exception ex)
				{
					Log.Error("Failed to save settings", ex);
				}
				finally
				{
					_fsMutex.ReleaseMutex();
				}
			}
			catch (Exception ex2)
			{
				Log.Error("Failed to save settings", ex2);
			}
		}
	}
}
