using System.IO;
using System.Net;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.UI.CustomCommands
{
	public class CustomCommandEnvironment
	{
		public abstract class Parameter
		{
		}

		public class DummyParameter : Parameter
		{
		}

		public class ReferenceParameter : Parameter
		{
			[Null]
			public Reference Reference { get; }

			public ReferenceParameter([Null] Reference reference)
			{
				Reference = reference;
			}
		}

		public class RepositoryFileParameter : Parameter
		{
			public string Filepath { get; }

			public Sha? Sha { get; }

			public RepositoryFileParameter(string filepath, Sha? sha)
			{
				Filepath = filepath;
				Sha = sha;
			}
		}

		public class RevisionParameter : Parameter
		{
			public Sha Sha { get; }

			public RevisionParameter(Sha sha)
			{
				Sha = sha;
			}
		}

		public class SubmoduleParameter : Parameter
		{
			public Submodule Submodule { get; }

			public SubmoduleParameter(Submodule submodule)
			{
				Submodule = submodule;
			}
		}

		public class TextParameter : Parameter
		{
			public string Text { get; }

			public TextParameter(string text)
			{
				Text = text;
			}
		}

		public class OptionalTextParameter : Parameter
		{
			public string Value { get; }

			public OptionalTextParameter(string value)
			{
				Value = value;
			}
		}

		public class PathParameter : Parameter
		{
			public string Path { get; }

			public PathParameter(string path)
			{
				Path = path;
			}
		}

		public GitModule GitModule { get; }

		public Parameter[] Parameters { get; }

		public CustomCommandEnvironment(GitModule gitModule)
			: this(gitModule, new DummyParameter[1]
			{
				new DummyParameter()
			})
		{
		}

		public CustomCommandEnvironment(GitModule gitModule, string filepath, Sha? sha)
			: this(gitModule, new RepositoryFileParameter[1]
			{
				new RepositoryFileParameter(filepath, sha)
			})
		{
		}

		public CustomCommandEnvironment(GitModule gitModule, Reference reference)
			: this(gitModule, new ReferenceParameter[1]
			{
				new ReferenceParameter(reference)
			})
		{
		}

		public CustomCommandEnvironment(GitModule gitModule, Sha sha)
			: this(gitModule, new RevisionParameter[1]
			{
				new RevisionParameter(sha)
			})
		{
		}

		public CustomCommandEnvironment(GitModule gitModule, Parameter[] parameters)
		{
			GitModule = gitModule;
			Parameters = parameters;
		}

		public virtual string ReplaceVariablesWithValues(string stringToReplace, bool urlEncode = false)
		{
			string target = stringToReplace;
			target = Replace(target, "${repo}", GitModule.Path, urlEncode);
			target = Replace(target, "${repo:path}", GitModule.Path, urlEncode);
			target = Replace(target, "$path", GitModule.Path, urlEncode);
			target = Replace(target, "${repo:name}", GitModule.RepositoryName, urlEncode);
			target = Replace(target, "$repository", GitModule.RepositoryName, urlEncode);
			target = Replace(target, "$reponame", GitModule.RepositoryName, urlEncode);
			for (int i = 0; i < Parameters.Length; i++)
			{
				int num = i;
				Parameter parameter = Parameters[i];
				if (!(parameter is RepositoryFileParameter repositoryFileParameter))
				{
					if (!(parameter is ReferenceParameter { Reference: var reference }))
					{
						if (!(parameter is RevisionParameter revisionParameter))
						{
							if (!(parameter is SubmoduleParameter submoduleParameter))
							{
								if (!(parameter is TextParameter textParameter))
								{
									if (!(parameter is OptionalTextParameter optionalTextParameter))
									{
										if (!(parameter is PathParameter pathParameter))
										{
											if (parameter is DummyParameter)
											{
											}
										}
										else
										{
											target = target.Replace($"${num}{{path}}", pathParameter.Path);
											string readableFileName = PathHelper.GetReadableFileName(pathParameter.Path);
											target = target.Replace($"${num}{{path:name}}", readableFileName);
										}
									}
									else
									{
										target = target.Replace($"${num}{{value}}", optionalTextParameter.Value);
									}
								}
								else
								{
									target = target.Replace($"${num}{{text}}", textParameter.Text);
								}
							}
							else
							{
								target = target.Replace("${submodule}", submoduleParameter.Submodule.Path);
								target = target.Replace("$path", submoduleParameter.Submodule.Path);
								target = target.Replace("$name", submoduleParameter.Submodule.Path);
							}
						}
						else
						{
							target = Replace(target, "${sha}", revisionParameter.Sha.ToString(), urlEncode);
							target = Replace(target, "$SHA", revisionParameter.Sha.ToString(), urlEncode);
							target = Replace(target, "${sha:abbr}", revisionParameter.Sha.ToAbbreviatedString(), urlEncode);
							target = Replace(target, "$sha", revisionParameter.Sha.ToAbbreviatedString(), urlEncode);
						}
					}
					else
					{
						if (reference == null)
						{
							continue;
						}
						if (num == 0)
						{
							target = Replace(target, "${sha}", reference.Sha.ToString(), urlEncode);
							target = Replace(target, "$SHA", reference.Sha.ToString(), urlEncode);
							target = Replace(target, "${sha:abbr}", reference.Sha.ToAbbreviatedString(), urlEncode);
							target = Replace(target, "$sha", reference.Sha.ToAbbreviatedString(), urlEncode);
							target = Replace(target, "${ref}", reference.Name, urlEncode);
							target = Replace(target, "$name", reference.Name, urlEncode);
							target = Replace(target, "${ref:full}", reference.FullReference, urlEncode);
							target = Replace(target, "$fullreference", reference.FullReference, urlEncode);
							if (reference is RemoteBranch remoteBranch)
							{
								target = Replace(target, "${ref:short}", remoteBranch.ShortName, urlEncode);
								target = Replace(target, "$shortname", remoteBranch.ShortName, urlEncode);
							}
							else
							{
								target = Replace(target, "${ref:short}", reference.Name, urlEncode);
								target = Replace(target, "$shortname", reference.Name, urlEncode);
							}
						}
						target = Replace(target, $"${num}{{sha}}", reference.Sha.ToString(), urlEncode);
						target = Replace(target, $"${num}{{sha:abbr}}", reference.Sha.ToAbbreviatedString(), urlEncode);
						target = Replace(target, $"${num}{{ref}}", reference.Name, urlEncode);
						target = Replace(target, $"${num}{{ref:full}}", reference.FullReference, urlEncode);
						target = ((!(reference is RemoteBranch remoteBranch2)) ? Replace(target, $"${num}{{ref:short}}", reference.Name, urlEncode) : Replace(target, $"${num}{{ref:short}}", remoteBranch2.ShortName, urlEncode));
					}
				}
				else
				{
					target = target.Replace("${file}", repositoryFileParameter.Filepath);
					target = target.Replace("$filepath", repositoryFileParameter.Filepath);
					target = target.Replace("${file:name}", Path.GetFileName(repositoryFileParameter.Filepath));
					target = target.Replace("$filename", Path.GetFileName(repositoryFileParameter.Filepath));
					Sha? sha = repositoryFileParameter.Sha;
					if (sha.HasValue)
					{
						Sha valueOrDefault = sha.GetValueOrDefault();
						target = Replace(target, "${sha}", valueOrDefault.ToString(), urlEncode);
						target = Replace(target, "$SHA", valueOrDefault.ToString(), urlEncode);
						target = Replace(target, "${sha:abbr}", valueOrDefault.ToAbbreviatedString(), urlEncode);
						target = Replace(target, "$sha", valueOrDefault.ToAbbreviatedString(), urlEncode);
					}
				}
			}
			// Phase 0.2c：App.GitPath/ShellPath → ServiceLocator.GitEnvironment（App 留在 WPF 主工程）
			string gitPath = ServiceLocator.GitEnvironment?.GitPath;
			string shellPath = ServiceLocator.GitEnvironment?.ShellPath;
			target = Replace(target, "${git}", gitPath, urlEncode);
			target = Replace(target, "$git", gitPath, urlEncode);
			target = Replace(target, "${sh}", shellPath, urlEncode);
			return Replace(target, "$sh", shellPath, urlEncode);
		}

		protected string Replace(string target, string oldValue, string newValue, bool urlEncode)
		{
			if (urlEncode)
			{
				return target.Replace(oldValue, WebUtility.UrlEncode(newValue));
			}
			return target.Replace(oldValue, newValue);
		}
	}
}
