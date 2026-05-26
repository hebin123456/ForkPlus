using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
	private static int Main(string[] args)
	{
		var failures = new List<string>();
		CompareOid(failures);
		CompareParsePatch(failures);
		CompareTreemap(failures);
		CompareMarkdown(failures);
		CompareDecodeImage(failures);
		CompareHighlight(failures);
		string[] repositoryPaths = args.Length > 0 ? args : CreateFixtureRepositories();
		if (repositoryPaths.Length != 0)
		{
			foreach (string repositoryPath in repositoryPaths)
			{
				Console.WriteLine("Comparing repository: " + repositoryPath);
				CompareGitRepository(repositoryPath, failures);
				BenchmarkCommits(repositoryPath);
				BenchmarkRefreshHotspots(repositoryPath);
			}
		}
		else
		{
			Console.WriteLine("No git repository found for repository-level checks.");
		}
		if (failures.Count == 0)
		{
			Console.WriteLine("Biturbo compatibility checks passed.");
			return 0;
		}
		Console.WriteLine("Biturbo compatibility differences:");
		foreach (string failure in failures)
		{
			Console.WriteLine("- " + failure);
		}
		return 1;
	}

	private static string FindRepositoryPath()
	{
		string current = AppContext.BaseDirectory;
		for (int i = 0; i < 12; i++)
		{
			if (Directory.Exists(Path.Combine(current, ".git")))
			{
				return current;
			}
			DirectoryInfo parent = Directory.GetParent(current);
			if (parent == null)
			{
				break;
			}
			current = parent.FullName;
		}
		current = Environment.CurrentDirectory;
		for (int i = 0; i < 12; i++)
		{
			if (Directory.Exists(Path.Combine(current, ".git")))
			{
				return current;
			}
			DirectoryInfo parent = Directory.GetParent(current);
			if (parent == null)
			{
				break;
			}
			current = parent.FullName;
		}
		return null;
	}

	private static void CompareGitRepository(string repositoryPath, List<string> failures)
	{
		string gitDir = ResolveGitDir(repositoryPath);
		CompareHead(gitDir, failures);
		CompareReferences(gitDir, failures);
		CompareGitConfig(Path.Combine(gitDir, "config"), failures);
		CompareRevisionHeaders(repositoryPath, gitDir, failures);
		CompareCommits(gitDir, failures);
		CompareRefreshHotspots(gitDir, failures);
		CompareTree(gitDir, failures);
		CompareTag(gitDir, failures);
		CompareStashes(gitDir, failures);
		CompareSearch(gitDir, failures);
	}

	private static string ResolveGitDir(string repositoryPath)
	{
		string dotGit = Path.Combine(repositoryPath, ".git");
		if (Directory.Exists(dotGit))
		{
			return dotGit;
		}
		if (File.Exists(dotGit))
		{
			string text = File.ReadAllText(dotGit).Trim();
			const string prefix = "gitdir:";
			if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				string gitDir = text.Substring(prefix.Length).Trim();
				if (!Path.IsPathRooted(gitDir))
				{
					gitDir = Path.GetFullPath(Path.Combine(repositoryPath, gitDir));
				}
				return gitDir;
			}
		}
		return dotGit;
	}

	private static void BenchmarkCommits(string repositoryPath)
	{
		string gitDir = ResolveGitDir(repositoryPath);
		BtOid[] tips = SampleOids(gitDir, 1);
		if (tips.Length == 0)
		{
			return;
		}
		TimeCommits("old topo", Original.bt_new_commit_graph_cache, Original.bt_release_commit_graph_cache, Original.bt_new_cancellation_token, Original.bt_release_cancellation_token, Original.bt_get_commits, Original.bt_release_commit_storage, gitDir, tips, dateOrder: false);
		TimeCommits("new topo", New.bt_new_commit_graph_cache, New.bt_release_commit_graph_cache, New.bt_new_cancellation_token, New.bt_release_cancellation_token, New.bt_get_commits, New.bt_release_commit_storage, gitDir, tips, dateOrder: false);
		TimeCommits("old date", Original.bt_new_commit_graph_cache, Original.bt_release_commit_graph_cache, Original.bt_new_cancellation_token, Original.bt_release_cancellation_token, Original.bt_get_commits, Original.bt_release_commit_storage, gitDir, tips, dateOrder: true);
		TimeCommits("new date", New.bt_new_commit_graph_cache, New.bt_release_commit_graph_cache, New.bt_new_cancellation_token, New.bt_release_cancellation_token, New.bt_get_commits, New.bt_release_commit_storage, gitDir, tips, dateOrder: true);
		BtOid[] allRefs = ReferenceOids(gitDir, 2000);
		if (allRefs.Length > 1)
		{
			TimeCommits("old allrefs topo", Original.bt_new_commit_graph_cache, Original.bt_release_commit_graph_cache, Original.bt_new_cancellation_token, Original.bt_release_cancellation_token, Original.bt_get_commits, Original.bt_release_commit_storage, gitDir, allRefs, dateOrder: false);
			TimeCommits("new allrefs topo", New.bt_new_commit_graph_cache, New.bt_release_commit_graph_cache, New.bt_new_cancellation_token, New.bt_release_cancellation_token, New.bt_get_commits, New.bt_release_commit_storage, gitDir, allRefs, dateOrder: false);
		}
	}

	private static void BenchmarkRefreshHotspots(string repositoryPath)
	{
		string gitDir = ResolveGitDir(repositoryPath);
		BtOid[] refs = ReferenceOids(gitDir, 200);
		if (refs.Length == 0)
		{
			return;
		}
		TimeCommitterTimes("old committer times", Original.bt_new_commit_graph_cache, Original.bt_release_commit_graph_cache, Original.bt_get_committer_times, Original.bt_release_committer_times, gitDir, refs);
		TimeCommitterTimes("new committer times", New.bt_new_commit_graph_cache, New.bt_release_commit_graph_cache, New.bt_get_committer_times, New.bt_release_committer_times, gitDir, refs);
		BtOidPair[] pairs = CreatePairs(refs, 40);
		if (pairs.Length != 0)
		{
			TimeBehindAhead("old behind/ahead", Original.bt_new_commit_graph_cache, Original.bt_release_commit_graph_cache, Original.bt_get_behind_ahead_counts, Original.bt_release_behind_ahead_counts, gitDir, pairs);
			TimeBehindAhead("new behind/ahead", New.bt_new_commit_graph_cache, New.bt_release_commit_graph_cache, New.bt_get_behind_ahead_counts, New.bt_release_behind_ahead_counts, gitDir, pairs);
			TimeBehindAheadAfterCommits("old warm behind/ahead", Original.bt_new_commit_graph_cache, Original.bt_release_commit_graph_cache, Original.bt_new_cancellation_token, Original.bt_release_cancellation_token, Original.bt_get_commits, Original.bt_release_commit_storage, Original.bt_get_behind_ahead_counts, Original.bt_release_behind_ahead_counts, gitDir, refs, pairs);
			TimeBehindAheadAfterCommits("new warm behind/ahead", New.bt_new_commit_graph_cache, New.bt_release_commit_graph_cache, New.bt_new_cancellation_token, New.bt_release_cancellation_token, New.bt_get_commits, New.bt_release_commit_storage, New.bt_get_behind_ahead_counts, New.bt_release_behind_ahead_counts, gitDir, refs, pairs);
		}
	}

	private static void TimeCommits(string label, NewCacheDelegate newCache, ReleaseCacheDelegate releaseCache, NewTokenDelegate newToken, ReleaseTokenDelegate releaseToken, GetCommitsDelegate getCommits, ReleaseCommitStorageDelegate releaseStorage, string gitDir, BtOid[] tips, bool dateOrder)
	{
		BtCommitGraphCache cache = newCache("bench");
		BtCancellationToken token = newToken();
		BtCommitStorage storage = default;
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		BtResult result = getCommits(gitDir, tips, tips.Length, dateOrder, 10000, 0, 5, Array.Empty<BtOid>(), 0, ref cache, ref token, ref storage);
		stopwatch.Stop();
		long count = storage.indexes_len;
		if (result == BtResult.Ok)
		{
			releaseStorage(ref storage);
		}
		releaseToken(ref token);
		releaseCache(ref cache);
		Console.WriteLine($"{label}: {result}, {count} commits, {stopwatch.ElapsedMilliseconds} ms");
	}

	private static void TimeCommitterTimes(string label, NewCacheDelegate newCache, ReleaseCacheDelegate releaseCache, GetCommitterTimesDelegate getCommitterTimes, ReleaseCommitterTimesDelegate releaseCommitterTimes, string gitDir, BtOid[] oids)
	{
		BtCommitGraphCache cache = newCache("bench");
		BtCommitterTimes resultData = default;
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		BtResult result = getCommitterTimes(gitDir, oids, oids.Length, ref cache, ref resultData);
		stopwatch.Stop();
		long count = resultData.times_len;
		if (result == BtResult.Ok)
		{
			releaseCommitterTimes(ref resultData);
		}
		releaseCache(ref cache);
		Console.WriteLine($"{label}: {result}, {count} refs, {stopwatch.ElapsedMilliseconds} ms");
	}

	private static void TimeBehindAhead(string label, NewCacheDelegate newCache, ReleaseCacheDelegate releaseCache, GetBehindAheadCountsDelegate getBehindAheadCounts, ReleaseBehindAheadCountsDelegate releaseBehindAheadCounts, string gitDir, BtOidPair[] pairs)
	{
		BtCommitGraphCache cache = newCache("bench");
		BtBehindAheadCounts resultData = default;
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		BtResult result = getBehindAheadCounts(gitDir, pairs, pairs.Length, ref cache, ref resultData);
		stopwatch.Stop();
		long count = resultData.items_len;
		if (result == BtResult.Ok)
		{
			releaseBehindAheadCounts(ref resultData);
		}
		releaseCache(ref cache);
		Console.WriteLine($"{label}: {result}, {count} pairs, {stopwatch.ElapsedMilliseconds} ms");
	}

	private static void TimeBehindAheadAfterCommits(string label, NewCacheDelegate newCache, ReleaseCacheDelegate releaseCache, NewTokenDelegate newToken, ReleaseTokenDelegate releaseToken, GetCommitsDelegate getCommits, ReleaseCommitStorageDelegate releaseStorage, GetBehindAheadCountsDelegate getBehindAheadCounts, ReleaseBehindAheadCountsDelegate releaseBehindAheadCounts, string gitDir, BtOid[] tips, BtOidPair[] pairs)
	{
		BtCommitGraphCache cache = newCache("bench");
		BtCancellationToken token = newToken();
		BtCommitStorage storage = default;
		_ = getCommits(gitDir, tips, tips.Length, false, 10000, 0, 5, Array.Empty<BtOid>(), 0, ref cache, ref token, ref storage);
		releaseStorage(ref storage);
		releaseToken(ref token);
		BtBehindAheadCounts resultData = default;
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		BtResult result = getBehindAheadCounts(gitDir, pairs, pairs.Length, ref cache, ref resultData);
		stopwatch.Stop();
		long count = resultData.items_len;
		if (result == BtResult.Ok)
		{
			releaseBehindAheadCounts(ref resultData);
		}
		releaseCache(ref cache);
		Console.WriteLine($"{label}: {result}, {count} pairs, {stopwatch.ElapsedMilliseconds} ms");
	}

	private static void CompareOid(List<string> failures)
	{
		string sha = "0123456789abcdef0123456789abcdef01234567";
		BtOid oldOid = default;
		BtOid newOid = default;
		BtResult oldResult = Original.bt_oid_from_str(Encoding.UTF8.GetBytes(sha + "\0"), ref oldOid);
		BtResult newResult = New.bt_oid_from_str(Encoding.UTF8.GetBytes(sha + "\0"), ref newOid);
		if (oldResult != newResult || !oldOid.Equals(newOid))
		{
			failures.Add($"bt_oid_from_str differs: old={oldResult}/{oldOid}, new={newResult}/{newOid}");
		}
	}

	private static string[] CreateFixtureRepositories()
	{
		string normal = CreateFixtureRepository("normal", detach: false, packRefs: false);
		string detached = CreateFixtureRepository("detached", detach: true, packRefs: false);
		string packed = CreateFixtureRepository("packed", detach: false, packRefs: true);
		return new[] { normal, detached, packed };
	}

	private static string CreateFixtureRepository(string name, bool detach, bool packRefs)
	{
		string root = Path.Combine(Path.GetTempPath(), "ForkPlus.Biturbo.Compare", Guid.NewGuid().ToString("N"), name);
		Directory.CreateDirectory(root);
		RunProcess("git", root, "init");
		RunProcess("git", root, "config", "user.name", "Biturbo Compare");
		RunProcess("git", root, "config", "user.email", "compare@example.com");
		File.WriteAllText(Path.Combine(root, "a.txt"), "one\n");
		RunProcess("git", root, "add", "a.txt");
		RunProcess("git", root, "commit", "-m", "initial");
		RunProcess("git", root, "checkout", "-b", "feature/test");
		File.WriteAllText(Path.Combine(root, "a.txt"), "one\ntwo\n");
		RunProcess("git", root, "commit", "-am", "update a");
		Directory.CreateDirectory(Path.Combine(root, "dir with space"));
		File.WriteAllText(Path.Combine(root, "dir with space", "中文 file.txt"), "hello\n");
		RunProcess("git", root, "add", ".");
		RunProcess("git", root, "commit", "-m", "add unicode path", "-m", "body line");
		RunProcess("git", root, "tag", "-a", "v1", "-m", "version one");
		File.WriteAllText(Path.Combine(root, "stash.txt"), "stash me\n");
		RunProcess("git", root, "stash", "push", "-m", "fixture stash", "--include-untracked");
		if (packRefs)
		{
			RunProcess("git", root, "pack-refs", "--all");
		}
		if (detach)
		{
			RunProcess("git", root, "checkout", "--detach", "HEAD");
		}
		return root;
	}

	private static string RunProcess(string fileName, string workingDirectory, params string[] args)
	{
		var psi = new System.Diagnostics.ProcessStartInfo(fileName)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};
		foreach (string arg in args)
		{
			psi.ArgumentList.Add(arg);
		}
		using var process = System.Diagnostics.Process.Start(psi);
		string stdout = process.StandardOutput.ReadToEnd();
		string stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"{fileName} {string.Join(" ", args)} failed: {stderr}");
		}
		return stdout;
	}

	private static void CompareParsePatch(List<string> failures)
	{
		string[] patches =
		{
			"diff --git forkSrcPrefix/a.txt forkDstPrefix/a.txt\nindex 1111111111111111111111111111111111111111..2222222222222222222222222222222222222222 100644\n--- forkSrcPrefix/a.txt\n+++ forkDstPrefix/a.txt\n@@ -1,2 +1,2 @@\n line1\n-old\n+new\n",
			"diff --git forkSrcPrefix/src/a forkDstPrefix/src/a\n--- forkSrcPrefix/src/a\n+++ forkDstPrefix/src/a\n@@ -0,0 +1 @@\n+added\n",
			"diff --git forkSrcPrefix/old.txt forkDstPrefix/new.txt\nsimilarity index 90\nrename from old.txt\nrename to new.txt\nindex 1111111111111111111111111111111111111111..2222222222222222222222222222222222222222 100644\n--- forkSrcPrefix/old.txt\n+++ forkDstPrefix/new.txt\n@@ -1 +1 @@\n-a\n+b\n",
			"diff --git forkSrcPrefix/deleted.txt forkDstPrefix/deleted.txt\ndeleted file mode 100644\nindex 1111111111111111111111111111111111111111..0000000000000000000000000000000000000000\n--- forkSrcPrefix/deleted.txt\n+++ /dev/null\n@@ -1 +0,0 @@\n-deleted\n",
			"diff --git forkSrcPrefix/dir with space/中文 file.txt forkDstPrefix/dir with space/中文 file.txt\nindex 1111111111111111111111111111111111111111..2222222222222222222222222222222222222222 100644\n--- forkSrcPrefix/dir with space/中文 file.txt\n+++ forkDstPrefix/dir with space/中文 file.txt\n@@ -1 +1 @@\n-old\n+new\n",
			"diff --git forkSrcPrefix/image.bin forkDstPrefix/image.bin\nindex 1111111111111111111111111111111111111111..2222222222222222222222222222222222222222 100644\nBinary files forkSrcPrefix/image.bin and forkDstPrefix/image.bin differ\n",
			"diff --git forkSrcPrefix/sub forkDstPrefix/sub\nindex 1111111111111111111111111111111111111111..2222222222222222222222222222222222222222 160000\n--- forkSrcPrefix/sub\n+++ forkDstPrefix/sub\n@@ -1 +1 @@\n-Subproject commit 1111111111111111111111111111111111111111\n+Subproject commit 2222222222222222222222222222222222222222\n"
		};
		for (int i = 0; i < patches.Length; i++)
		{
			BtPatchToken[] oldTokens = ParsePatch(Original.bt_parse_patch, Original.bt_release_parse_patch, patches[i], out BtResult oldResult);
			BtPatchToken[] newTokens = ParsePatch(New.bt_parse_patch, New.bt_release_parse_patch, patches[i], out BtResult newResult);
			if (oldResult != newResult || !oldTokens.SequenceEqual(newTokens))
			{
				failures.Add($"bt_parse_patch sample {i} differs: old={oldResult}/{oldTokens.Length}, new={newResult}/{newTokens.Length}\nold={FormatTokens(oldTokens)}\nnew={FormatTokens(newTokens)}");
			}
		}
	}

	private static BtPatchToken[] ParsePatch(ParsePatchDelegate parse, ReleaseParsePatchDelegate release, string patch, out BtResult result)
	{
		byte[] patchBytes = Encoding.UTF8.GetBytes(patch);
		byte[] srcPrefix = Encoding.UTF8.GetBytes("forkSrcPrefix/");
		byte[] dstPrefix = Encoding.UTF8.GetBytes("forkDstPrefix/");
		BtParsePatchResult parseResult = default;
		result = parse(patchBytes, (ulong)patchBytes.Length, srcPrefix, (ulong)srcPrefix.Length, dstPrefix, (ulong)dstPrefix.Length, ref parseResult);
		if (result != BtResult.Ok)
		{
			return Array.Empty<BtPatchToken>();
		}
		try
		{
			var tokens = new BtPatchToken[parseResult.tokens_len];
			int size = Marshal.SizeOf<BtPatchToken>();
			for (int i = 0; i < tokens.Length; i++)
			{
				tokens[i] = Marshal.PtrToStructure<BtPatchToken>(parseResult.tokens + i * size);
			}
			return tokens;
		}
		finally
		{
			release(ref parseResult);
		}
	}

	private static void CompareTreemap(List<string> failures)
	{
		(long[] values, BtRect rect)[] cases =
		{
			(new long[] { 10, 20, 30 }, new BtRect { x = 0, y = 0, w = 600, h = 300 }),
			(new long[] { 100, 1, 1, 1 }, new BtRect { x = 10, y = 20, w = 200, h = 700 }),
			(new long[] { 0, 0, 5, 10, 15 }, new BtRect { x = 0, y = 0, w = 512, h = 512 })
		};
		for (int i = 0; i < cases.Length; i++)
		{
			BtTreemapItem[] oldItems = LayoutTreemap(Original.bt_layout_treemap, Original.bt_release_layout_treemap, cases[i].values, cases[i].rect, out BtResult oldResult);
			BtTreemapItem[] newItems = LayoutTreemap(New.bt_layout_treemap, New.bt_release_layout_treemap, cases[i].values, cases[i].rect, out BtResult newResult);
			if (oldResult != newResult || FormatTreemap(oldItems) != FormatTreemap(newItems))
			{
				failures.Add($"bt_layout_treemap case {i} differs: old={oldResult}/{FormatTreemap(oldItems)}, new={newResult}/{FormatTreemap(newItems)}");
			}
		}
	}

	private static void CompareMarkdown(List<string> failures)
	{
		string[] cases =
		{
			"# Title\n\nParagraph with **bold** and `code`.\n\n- item 1\n- item 2\n\n```csharp\nConsole.WriteLine(\"x\");\n```\n",
			"## Link\n\n[site](https://example.com) and ![img](a.png)\n\n1. one\n2. two\n",
			"> quote\n\n| A | B |\n|---|---|\n| 1 | 2 |\n"
		};
		for (int i = 0; i < cases.Length; i++)
		{
			string oldHtml = MarkdownToHtml(Original.bt_md_to_html, Original.bt_release_md_to_html, cases[i], out BtResult oldResult);
			string newHtml = MarkdownToHtml(New.bt_md_to_html, New.bt_release_md_to_html, cases[i], out BtResult newResult);
			if (oldResult == BtResult.Ok && newResult == BtResult.Ok && oldHtml.Contains("|---|") && newHtml.Contains("<table>"))
			{
				continue;
			}
			if (oldResult != newResult || oldHtml != newHtml)
			{
				failures.Add($"bt_md_to_html case {i} differs:\nold={oldResult} {oldHtml}\nnew={newResult} {newHtml}");
			}
		}
	}

	private static void CompareDecodeImage(List<string> failures)
	{
		byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
		byte[] tga = { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 24, 0, 0, 0, 255 };
		(byte[] data, string name)[] cases = { (png, "png"), (tga, "tga") };
		foreach ((byte[] data, string name) in cases)
		{
			byte[] oldData = DecodeImage(Original.bt_decode_image, Original.bt_release_decode_image, data, out BtResult oldResult);
			byte[] newData = DecodeImage(New.bt_decode_image, New.bt_release_decode_image, data, out BtResult newResult);
			if (oldResult != BtResult.Ok && newResult == BtResult.Ok)
			{
				continue;
			}
			if (oldResult != newResult || !oldData.SequenceEqual(newData))
			{
				failures.Add($"bt_decode_image {name} differs: old={oldResult}/{oldData.Length}/{Hex(oldData)}, new={newResult}/{newData.Length}/{Hex(newData)}");
			}
		}
	}

	private static void CompareHighlight(List<string> failures)
	{
		var ranges = new[] { new BtRange { start = 0, end = 42 } };
		string code = "public class C { string S = \"x\"; // hi\n}\n";
		BtHighlighedRange[] oldItems = Highlight(Original.bt_highlight_syntax, Original.bt_release_highlight_syntax, "a.cs", code, ranges, out BtResult oldResult);
		BtHighlighedRange[] newItems = Highlight(New.bt_highlight_syntax, New.bt_release_highlight_syntax, "a.cs", code, ranges, out BtResult newResult);
		if (oldResult != BtResult.Ok && newResult == BtResult.Ok)
		{
			return;
		}
		if (oldResult != newResult || FormatHighlight(oldItems) != FormatHighlight(newItems))
		{
			failures.Add($"bt_highlight_syntax differs: old={oldResult}/{FormatHighlight(oldItems)}, new={newResult}/{FormatHighlight(newItems)}");
		}
	}

	private static string MarkdownToHtml(MdToHtmlDelegate convert, ReleaseMdToHtmlDelegate release, string markdown, out BtResult result)
	{
		BtMdToHtmlResult htmlResult = default;
		result = convert(markdown, ref htmlResult);
		if (result != BtResult.Ok)
		{
			return "";
		}
		try
		{
			return PtrToString(htmlResult.html) ?? "";
		}
		finally
		{
			release(ref htmlResult);
		}
	}

	private static void CompareHead(string gitDir, List<string> failures)
	{
		BtHead oldHead = default;
		BtHead newHead = default;
		BtResult oldResult = Original.bt_get_head(gitDir, ref oldHead);
		BtResult newResult = New.bt_get_head(gitDir, ref newHead);
		try
		{
			string oldRef = PtrToString(oldHead.Reference);
			string newRef = PtrToString(newHead.Reference);
			if (oldResult != newResult || oldRef != newRef || !oldHead.DetachedHead.Equals(newHead.DetachedHead))
			{
				failures.Add($"bt_get_head differs: old={oldResult}/{oldRef}/{oldHead.DetachedHead}, new={newResult}/{newRef}/{newHead.DetachedHead}");
			}
		}
		finally
		{
			if (oldResult == BtResult.Ok) Original.bt_release_head(ref oldHead);
			if (newResult == BtResult.Ok) New.bt_release_head(ref newHead);
		}
	}

	private static void CompareReferences(string gitDir, List<string> failures)
	{
		BtReferences oldRefs = default;
		BtReferences newRefs = default;
		BtResult oldResult = Original.bt_get_references(gitDir, false, ref oldRefs);
		BtResult newResult = New.bt_get_references(gitDir, false, ref newRefs);
		try
		{
			string oldSummary = oldResult == BtResult.Ok ? SummarizeReferences(oldRefs) : "";
			string newSummary = newResult == BtResult.Ok ? SummarizeReferences(newRefs) : "";
			if (oldResult != newResult || oldSummary != newSummary)
			{
				failures.Add($"bt_get_references differs:\nold={oldResult} {oldSummary}\nnew={newResult} {newSummary}");
			}
		}
		finally
		{
			if (oldResult == BtResult.Ok) Original.bt_release_references(ref oldRefs);
			if (newResult == BtResult.Ok) New.bt_release_references(ref newRefs);
		}
	}

	private static void CompareGitConfig(string configPath, List<string> failures)
	{
		BtGitConfig oldConfig = default;
		BtGitConfig newConfig = default;
		BtResult oldResult = Original.bt_get_git_config(configPath, ref oldConfig);
		BtResult newResult = New.bt_get_git_config(configPath, ref newConfig);
		try
		{
			string oldSummary = oldResult == BtResult.Ok ? SummarizeGitConfig(oldConfig) : "";
			string newSummary = newResult == BtResult.Ok ? SummarizeGitConfig(newConfig) : "";
			if (oldResult != newResult || oldSummary != newSummary)
			{
				failures.Add($"bt_get_git_config differs:\nold={oldResult} {oldSummary}\nnew={newResult} {newSummary}");
			}
		}
		finally
		{
			if (oldResult == BtResult.Ok) Original.bt_release_git_config(ref oldConfig);
			if (newResult == BtResult.Ok) New.bt_release_git_config(ref newConfig);
		}
	}

	private static void CompareRevisionHeaders(string repositoryPath, string gitDir, List<string> failures)
	{
		BtOid[] sample = SampleOids(gitDir, 10);
		if (sample.Length == 0)
		{
			return;
		}
		BtRevisionHeaders oldHeaders = default;
		BtRevisionHeaders newHeaders = default;
		BtResult oldResult = Original.bt_get_revision_headers(repositoryPath, gitDir, sample, sample.Length, ref oldHeaders);
		BtResult newResult = New.bt_get_revision_headers(repositoryPath, gitDir, sample, sample.Length, ref newHeaders);
		try
		{
			string oldSummary = oldResult == BtResult.Ok ? SummarizeRevisionHeaders(oldHeaders) : "";
			string newSummary = newResult == BtResult.Ok ? SummarizeRevisionHeaders(newHeaders) : "";
			if (oldResult != newResult || oldSummary != newSummary)
			{
				failures.Add($"bt_get_revision_headers differs:\nold={oldResult} {oldSummary}\nnew={newResult} {newSummary}");
			}
		}
		finally
		{
			if (oldResult == BtResult.Ok) Original.bt_release_revision_headers(ref oldHeaders);
			if (newResult == BtResult.Ok) New.bt_release_revision_headers(ref newHeaders);
		}
	}

	private static void CompareCommits(string gitDir, List<string> failures)
	{
		BtOid[] tips = SampleOids(gitDir, 1);
		if (tips.Length == 0)
		{
			return;
		}
		CompareCommitsCase(gitDir, tips, dateOrder: false, pageSize: 100, skipPages: 0, minPages: 1, failures);
		CompareCommitsCase(gitDir, tips, dateOrder: true, pageSize: 100, skipPages: 0, minPages: 1, failures);
		CompareCommitsCase(gitDir, tips, dateOrder: true, pageSize: 2, skipPages: 1, minPages: 1, failures);
		BtOid[] allRefs = ReferenceOids(gitDir, 2000);
		if (allRefs.Length > 1)
		{
			CompareCommitsCase(gitDir, allRefs, dateOrder: false, pageSize: 100, skipPages: 0, minPages: 1, failures);
			CompareCommitsCase(gitDir, allRefs, dateOrder: true, pageSize: 100, skipPages: 0, minPages: 1, failures);
		}
	}

	private static void CompareRefreshHotspots(string gitDir, List<string> failures)
	{
		BtOid[] refs = ReferenceOids(gitDir, 20);
		if (refs.Length == 0)
		{
			return;
		}
		BtCommitGraphCache oldCache = Original.bt_new_commit_graph_cache("compare");
		BtCommitGraphCache newCache = New.bt_new_commit_graph_cache("compare");
		BtCommitterTimes oldTimes = default;
		BtCommitterTimes newTimes = default;
		BtResult oldTimesResult = Original.bt_get_committer_times(gitDir, refs, refs.Length, ref oldCache, ref oldTimes);
		BtResult newTimesResult = New.bt_get_committer_times(gitDir, refs, refs.Length, ref newCache, ref newTimes);
		try
		{
			string oldSummary = oldTimesResult == BtResult.Ok ? SummarizeCommitterTimes(oldTimes) : "";
			string newSummary = newTimesResult == BtResult.Ok ? SummarizeCommitterTimes(newTimes) : "";
			if (oldTimesResult != newTimesResult || oldSummary != newSummary)
			{
				failures.Add($"bt_get_committer_times differs:\nold={oldTimesResult} {oldSummary}\nnew={newTimesResult} {newSummary}");
			}
		}
		finally
		{
			if (oldTimesResult == BtResult.Ok) Original.bt_release_committer_times(ref oldTimes);
			if (newTimesResult == BtResult.Ok) New.bt_release_committer_times(ref newTimes);
			Original.bt_release_commit_graph_cache(ref oldCache);
			New.bt_release_commit_graph_cache(ref newCache);
		}
		BtOidPair[] pairs = CreatePairs(refs, 10);
		if (pairs.Length == 0)
		{
			return;
		}
		oldCache = Original.bt_new_commit_graph_cache("compare");
		newCache = New.bt_new_commit_graph_cache("compare");
		BtBehindAheadCounts oldCounts = default;
		BtBehindAheadCounts newCounts = default;
		BtResult oldCountsResult = Original.bt_get_behind_ahead_counts(gitDir, pairs, pairs.Length, ref oldCache, ref oldCounts);
		BtResult newCountsResult = New.bt_get_behind_ahead_counts(gitDir, pairs, pairs.Length, ref newCache, ref newCounts);
		try
		{
			string oldSummary = oldCountsResult == BtResult.Ok ? SummarizeBehindAheadCounts(oldCounts) : "";
			string newSummary = newCountsResult == BtResult.Ok ? SummarizeBehindAheadCounts(newCounts) : "";
			if (oldCountsResult != newCountsResult || oldSummary != newSummary)
			{
				failures.Add($"bt_get_behind_ahead_counts differs:\nold={oldCountsResult} {oldSummary}\nnew={newCountsResult} {newSummary}");
			}
		}
		finally
		{
			if (oldCountsResult == BtResult.Ok) Original.bt_release_behind_ahead_counts(ref oldCounts);
			if (newCountsResult == BtResult.Ok) New.bt_release_behind_ahead_counts(ref newCounts);
			Original.bt_release_commit_graph_cache(ref oldCache);
			New.bt_release_commit_graph_cache(ref newCache);
		}
	}

	private static void CompareCommitsCase(string gitDir, BtOid[] tips, bool dateOrder, long pageSize, long skipPages, long minPages, List<string> failures)
	{
		BtCommitGraphCache oldCache = Original.bt_new_commit_graph_cache("compare");
		BtCommitGraphCache newCache = New.bt_new_commit_graph_cache("compare");
		BtCancellationToken oldToken = Original.bt_new_cancellation_token();
		BtCancellationToken newToken = New.bt_new_cancellation_token();
		BtCommitStorage oldStorage = default;
		BtCommitStorage newStorage = default;
		BtResult oldResult = Original.bt_get_commits(gitDir, tips, tips.Length, dateOrder, pageSize, skipPages, minPages, Array.Empty<BtOid>(), 0, ref oldCache, ref oldToken, ref oldStorage);
		BtResult newResult = New.bt_get_commits(gitDir, tips, tips.Length, dateOrder, pageSize, skipPages, minPages, Array.Empty<BtOid>(), 0, ref newCache, ref newToken, ref newStorage);
		try
		{
			string oldSummary = oldResult == BtResult.Ok ? SummarizeCommitStorage(oldStorage) : "";
			string newSummary = newResult == BtResult.Ok ? SummarizeCommitStorage(newStorage) : "";
			if (oldResult != newResult || oldSummary != newSummary)
			{
				failures.Add($"bt_get_commits date={dateOrder} page={pageSize} skip={skipPages} differs:\nold={oldResult} {oldSummary}\nnew={newResult} {newSummary}");
			}
		}
		finally
		{
			if (oldResult == BtResult.Ok) Original.bt_release_commit_storage(ref oldStorage);
			if (newResult == BtResult.Ok) New.bt_release_commit_storage(ref newStorage);
			Original.bt_release_cancellation_token(ref oldToken);
			New.bt_release_cancellation_token(ref newToken);
			Original.bt_release_commit_graph_cache(ref oldCache);
			New.bt_release_commit_graph_cache(ref newCache);
		}
	}

	private static void CompareTree(string gitDir, List<string> failures)
	{
		BtOid[] sample = SampleOids(gitDir, 1);
		if (sample.Length == 0) return;
		BtOid tree = TryParseOid(RunGit(gitDir, "show", "-s", "--format=%T", sample[0].ToString()).Trim()).Value;
		BtTree oldTree = default, newTree = default;
		BtResult oldResult = Original.bt_get_tree(gitDir, ref tree, ref oldTree);
		BtResult newResult = New.bt_get_tree(gitDir, ref tree, ref newTree);
		try
		{
			string oldSummary = oldResult == BtResult.Ok ? SummarizeTree(oldTree) : "";
			string newSummary = newResult == BtResult.Ok ? SummarizeTree(newTree) : "";
			if (oldResult != newResult || oldSummary != newSummary)
			{
				failures.Add($"bt_get_tree differs:\nold={oldResult} {oldSummary}\nnew={newResult} {newSummary}");
			}
		}
		finally
		{
			if (oldResult == BtResult.Ok) Original.bt_release_tree(ref oldTree);
			if (newResult == BtResult.Ok) New.bt_release_tree(ref newTree);
		}
	}

	private static void CompareTag(string gitDir, List<string> failures)
	{
		BtOid? tag = TryParseOid(RunGit(gitDir, "rev-parse", "v1").Trim());
		if (!tag.HasValue) return;
		BtTagDetails oldTag = default, newTag = default;
		BtResult oldResult = Original.bt_get_tag_details(gitDir, tag.Value, ref oldTag);
		BtResult newResult = New.bt_get_tag_details(gitDir, tag.Value, ref newTag);
		try
		{
			string oldSummary = oldResult == BtResult.Ok ? SummarizeTag(oldTag) : "";
			string newSummary = newResult == BtResult.Ok ? SummarizeTag(newTag) : "";
			if (oldResult != newResult || oldSummary != newSummary)
			{
				failures.Add($"bt_get_tag_details differs:\nold={oldResult} {oldSummary}\nnew={newResult} {newSummary}");
			}
		}
		finally
		{
			if (oldResult == BtResult.Ok) Original.bt_release_tag_details(ref oldTag);
			if (newResult == BtResult.Ok) New.bt_release_tag_details(ref newTag);
		}
	}

	private static void CompareStashes(string gitDir, List<string> failures)
	{
		BtRepositoryStashes oldStashes = default, newStashes = default;
		string workTree = gitDir.Substring(0, gitDir.Length - 5);
		BtResult oldResult = Original.bt_get_repository_stashes(workTree, gitDir, ref oldStashes);
		BtResult newResult = New.bt_get_repository_stashes(workTree, gitDir, ref newStashes);
		try
		{
			string oldSummary = oldResult == BtResult.Ok ? SummarizeStashes(oldStashes) : "";
			string newSummary = newResult == BtResult.Ok ? SummarizeStashes(newStashes) : "";
			if (oldResult != newResult || oldSummary != newSummary)
			{
				failures.Add($"bt_get_repository_stashes differs:\nold={oldResult} {oldSummary}\nnew={newResult} {newSummary}");
			}
		}
		finally
		{
			if (oldResult == BtResult.Ok) Original.bt_release_repository_stashes(ref oldStashes);
			if (newResult == BtResult.Ok) New.bt_release_repository_stashes(ref newStashes);
		}
	}

	private static void CompareSearch(string gitDir, List<string> failures)
	{
		BtOid[] sample = SampleOids(gitDir, 10);
		BtCancellationToken oldToken = Original.bt_new_cancellation_token();
		BtCancellationToken newToken = New.bt_new_cancellation_token();
		BtSearchCommitsResult oldSearch = default, newSearch = default;
		BtResult oldResult = Original.bt_search_commits(gitDir, sample, sample.Length, "update", Array.Empty<BtOid>(), 0, ref oldToken, ref oldSearch);
		BtResult newResult = New.bt_search_commits(gitDir, sample, sample.Length, "update", Array.Empty<BtOid>(), 0, ref newToken, ref newSearch);
		try
		{
			string oldSummary = oldResult == BtResult.Ok ? string.Join(",", ReadStructArray<BtOid>(oldSearch.matches, oldSearch.matches_len).Select(x => x.ToString())) : "";
			string newSummary = newResult == BtResult.Ok ? string.Join(",", ReadStructArray<BtOid>(newSearch.matches, newSearch.matches_len).Select(x => x.ToString())) : "";
			if (oldResult != newResult || oldSummary != newSummary)
			{
				failures.Add($"bt_search_commits differs:\nold={oldResult} {oldSummary}\nnew={newResult} {newSummary}");
			}
		}
		finally
		{
			if (oldResult == BtResult.Ok) Original.bt_release_search_commits(ref oldSearch);
			if (newResult == BtResult.Ok) New.bt_release_search_commits(ref newSearch);
			Original.bt_release_cancellation_token(ref oldToken);
			New.bt_release_cancellation_token(ref newToken);
		}
	}

	private static BtTreemapItem[] LayoutTreemap(LayoutTreemapDelegate layout, ReleaseTreemapDelegate release, long[] values, BtRect rect, out BtResult result)
	{
		BtLayoutTreemapResult layoutResult = default;
		result = layout(values, values.Length, rect, ref layoutResult);
		if (result != BtResult.Ok)
		{
			return Array.Empty<BtTreemapItem>();
		}
		try
		{
			var items = new BtTreemapItem[layoutResult.items_len];
			int size = Marshal.SizeOf<BtTreemapItem>();
			for (int i = 0; i < items.Length; i++)
			{
				items[i] = Marshal.PtrToStructure<BtTreemapItem>(layoutResult.items + i * size);
			}
			return items;
		}
		finally
		{
			release(ref layoutResult);
		}
	}

	private static string FormatTokens(IEnumerable<BtPatchToken> tokens)
	{
		return string.Join(", ", tokens.Select(x => $"{x.kind}:{x.start}-{x.end}"));
	}

	private static string FormatTreemap(IEnumerable<BtTreemapItem> items)
	{
		return string.Join("|", items.Select(x => $"{x.index}:{x.rect.x:0.###},{x.rect.y:0.###},{x.rect.w:0.###},{x.rect.h:0.###}"));
	}

	private static string FormatHighlight(IEnumerable<BtHighlighedRange> items)
	{
		return string.Join("|", items.Select(x => $"{x.range_utf16.start}-{x.range_utf16.end}:{x.style}"));
	}

	private static string Hex(byte[] data)
	{
		return Convert.ToHexString(data.Take(32).ToArray());
	}

	private static string SummarizeTree(BtTree tree)
	{
		return string.Join("|", ReadStructArray<BtTreeItem>(tree.entries, tree.entries_len).Select(x => $"{x.kind}:{PtrToString(x.filename)}:{x.treeish}"));
	}

	private static string SummarizeTag(BtTagDetails tag)
	{
		return $"{tag.tag_object_oid}:{PtrToString(tag.tagger_name)}<{PtrToString(tag.tagger_email)}>:{tag.tagger_time}:{PtrToString(tag.name)}:{PtrToString(tag.message)}";
	}

	private static string SummarizeStashes(BtRepositoryStashes stashes)
	{
		BtIdentity[] identities = ReadStructArray<BtIdentity>(stashes.identities, stashes.identities_len);
		return string.Join("|", ReadStructArray<BtStash>(stashes.stashes, stashes.stashes_len).Select(x =>
		{
			string identity = x.author_index >= 0 && x.author_index < identities.Length ? PtrToString(identities[x.author_index].name) : "?";
			return $"{x.reflog_id}:{x.oid}:{x.first_parent}:{identity}:{x.author_time}:{PtrToString(x.subject)}";
		}));
	}

	private static byte[] DecodeImage(DecodeImageDelegate decode, ReleaseDecodeImageDelegate release, byte[] data, out BtResult result)
	{
		BtDecodeImageResult image = default;
		result = decode(data, data.Length, ref image);
		if (result != BtResult.Ok) return Array.Empty<byte>();
		try { return ReadByteArray(image.data, image.data_len); }
		finally { release(ref image); }
	}

	private static BtHighlighedRange[] Highlight(HighlightDelegate highlight, ReleaseHighlightDelegate release, string path, string code, BtRange[] ranges, out BtResult result)
	{
		BtHighlightedDiff diff = default;
		result = highlight(path, code, ranges, ranges.Length, ref diff);
		if (result != BtResult.Ok) return Array.Empty<BtHighlighedRange>();
		try { return ReadStructArray<BtHighlighedRange>(diff.items, diff.items_len); }
		finally { release(ref diff); }
	}

	private static string SummarizeReferences(BtReferences refs)
	{
		string[] names = ReadStringData(refs.names_data, refs.names_offsets, refs.names_offsets_len);
		BtOid[] oids = ReadStructArray<BtOid>(refs.oids, refs.oids_len);
		string[] symrefs = ReadStringData(refs.symrefs_data, refs.symrefs_offsets, refs.symrefs_offsets_len);
		var builder = new StringBuilder();
		builder.Append("refs=");
		builder.Append(string.Join("|", names.Zip(oids, (name, oid) => name + "=" + oid)));
		builder.Append(";symrefs=");
		for (int i = 0; i + 1 < symrefs.Length; i += 2)
		{
			if (i > 0) builder.Append("|");
			builder.Append(symrefs[i]).Append("->").Append(symrefs[i + 1]);
		}
		return builder.ToString();
	}

	private static string SummarizeGitConfig(BtGitConfig config)
	{
		BtGitConfigSection[] sections = ReadStructArray<BtGitConfigSection>(config.sections, config.sections_len);
		var builder = new StringBuilder();
		for (int i = 0; i < sections.Length; i++)
		{
			if (i > 0) builder.Append("|");
			builder.Append(PtrToString(sections[i].name)).Append(".").Append(PtrToString(sections[i].sub_section)).Append(":");
			BtGitConfigVariable[] variables = ReadStructArray<BtGitConfigVariable>(sections[i].variables, sections[i].variables_len);
			builder.Append(string.Join(",", variables.Select(v => PtrToString(v.name) + "=" + PtrToString(v.value))));
		}
		return builder.ToString();
	}

	private static string SummarizeRevisionHeaders(BtRevisionHeaders headers)
	{
		BtIdentity[] identities = ReadStructArray<BtIdentity>(headers.identities, headers.identities_len);
		BtRevisionHeader[] revisions = ReadStructArray<BtRevisionHeader>(headers.revisions, headers.revisions_len);
		return string.Join("|", revisions.Select(r =>
		{
			string identity = r.author_index >= 0 && r.author_index < identities.Length
				? PtrToString(identities[r.author_index].name) + "<" + PtrToString(identities[r.author_index].email) + ">"
				: "?";
			return $"{identity}@{r.author_time}:{PtrToString(r.subject)}:{r.has_body}";
		}));
	}

	private static string SummarizeCommitStorage(BtCommitStorage storage)
	{
		BtOid[] oids = ReadStructArray<BtOid>(storage.oids, storage.oids_len);
		uint[] indexes = ReadUInt32Array(storage.indexes, storage.indexes_len);
		return $"oids={string.Join(",", oids.Select(o => o.ToString()).Take(40))};indexes={string.Join(",", indexes.Take(40))};hasMore={storage.has_more}";
	}

	private static string SummarizeCommitterTimes(BtCommitterTimes times)
	{
		long[] values = ReadInt64Array(times.times, times.times_len);
		return string.Join(",", values.Take(40));
	}

	private static string SummarizeBehindAheadCounts(BtBehindAheadCounts counts)
	{
		BtBehindAheadCount[] values = ReadStructArray<BtBehindAheadCount>(counts.items, counts.items_len);
		return string.Join(",", values.Take(40).Select(x => x.left + "/" + x.right));
	}

	private static BtOid[] SampleOids(string gitDir, int count)
	{
		string output = RunGit(gitDir, "rev-list", "--max-count=" + count, "HEAD");
		return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(TryParseOid)
			.Where(x => x.HasValue)
			.Select(x => x.Value)
			.ToArray();
	}

	private static BtOid[] ReferenceOids(string gitDir, int count)
	{
		string output = RunGit(gitDir, "for-each-ref", "--format=%(objectname)%09%(*objectname)");
		return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(line =>
			{
				string[] parts = line.Split('\t');
				string sha = parts.Length > 1 && parts[1].Length >= 40 ? parts[1] : parts[0];
				return TryParseOid(sha);
			})
			.Where(x => x.HasValue)
			.Select(x => x.Value)
			.Distinct()
			.Take(count)
			.ToArray();
	}

	private static BtOidPair[] CreatePairs(BtOid[] oids, int maxPairs)
	{
		if (oids.Length < 2)
		{
			return Array.Empty<BtOidPair>();
		}
		var pairs = new List<BtOidPair>();
		for (int i = 1; i < oids.Length && pairs.Count < maxPairs; i++)
		{
			pairs.Add(new BtOidPair { left = oids[0], right = oids[i] });
		}
		return pairs.ToArray();
	}

	private static string RunGit(string gitDir, params string[] args)
	{
		var psi = new System.Diagnostics.ProcessStartInfo("git");
		psi.ArgumentList.Add("--git-dir=" + gitDir);
		string workTree = gitDir.EndsWith(Path.DirectorySeparatorChar + ".git", StringComparison.OrdinalIgnoreCase)
			? gitDir.Substring(0, gitDir.Length - 5)
			: Directory.GetCurrentDirectory();
		psi.ArgumentList.Add("--work-tree=" + workTree);
		foreach (string arg in args)
		{
			psi.ArgumentList.Add(arg);
		}
		psi.RedirectStandardOutput = true;
		psi.RedirectStandardError = true;
		psi.UseShellExecute = false;
		using var process = System.Diagnostics.Process.Start(psi);
		string stdout = process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		return stdout;
	}

	private static BtOid? TryParseOid(string sha)
	{
		BtOid oid = default;
		if (New.bt_oid_from_str(Encoding.UTF8.GetBytes(sha + "\0"), ref oid) == BtResult.Ok)
		{
			return oid;
		}
		return null;
	}

	private static string[] ReadStringData(IntPtr dataPtr, IntPtr offsetsPtr, long offsetsLen)
	{
		long[] offsets = ReadInt64Array(offsetsPtr, offsetsLen);
		byte[] data = ReadByteArray(dataPtr, offsets.Length == 0 ? 0 : offsets[^1]);
		string[] result = new string[offsets.Length];
		for (int i = 0; i < offsets.Length; i++)
		{
			int start = i == 0 ? 0 : (int)offsets[i - 1];
			int end = (int)offsets[i];
			result[i] = Encoding.UTF8.GetString(data, start, end - start);
		}
		return result;
	}

	private static T[] ReadStructArray<T>(IntPtr ptr, long len) where T : struct
	{
		if (ptr == IntPtr.Zero || len <= 0)
		{
			return Array.Empty<T>();
		}
		var result = new T[len];
		int size = Marshal.SizeOf<T>();
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = Marshal.PtrToStructure<T>(ptr + i * size);
		}
		return result;
	}

	private static long[] ReadInt64Array(IntPtr ptr, long len)
	{
		if (ptr == IntPtr.Zero || len <= 0)
		{
			return Array.Empty<long>();
		}
		var result = new long[len];
		Marshal.Copy(ptr, result, 0, (int)len);
		return result;
	}

	private static uint[] ReadUInt32Array(IntPtr ptr, long len)
	{
		if (ptr == IntPtr.Zero || len <= 0)
		{
			return Array.Empty<uint>();
		}
		var ints = new int[len];
		Marshal.Copy(ptr, ints, 0, (int)len);
		return Array.ConvertAll(ints, x => unchecked((uint)x));
	}

	private static byte[] ReadByteArray(IntPtr ptr, long len)
	{
		if (ptr == IntPtr.Zero || len <= 0)
		{
			return Array.Empty<byte>();
		}
		var result = new byte[len];
		if (len > 0)
		{
			Marshal.Copy(ptr, result, 0, (int)len);
		}
		return result;
	}

	private static string PtrToString(IntPtr ptr)
	{
		if (ptr == IntPtr.Zero)
		{
			return null;
		}
		int len = 0;
		while (Marshal.ReadByte(ptr, len) != 0) len++;
		var bytes = new byte[len];
		Marshal.Copy(ptr, bytes, 0, len);
		return Encoding.UTF8.GetString(bytes);
	}

	private delegate BtResult ParsePatchDelegate(byte[] patch, ulong patchLen, byte[] srcPrefix, ulong srcPrefixLen, byte[] dstPrefix, ulong dstPrefixLen, ref BtParsePatchResult result);
	private delegate void ReleaseParsePatchDelegate(ref BtParsePatchResult result);
	private delegate BtResult LayoutTreemapDelegate(long[] sizes, long sizesLen, BtRect rect, ref BtLayoutTreemapResult result);
	private delegate void ReleaseTreemapDelegate(ref BtLayoutTreemapResult result);
	private delegate BtResult MdToHtmlDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string markdown, ref BtMdToHtmlResult result);
	private delegate void ReleaseMdToHtmlDelegate(ref BtMdToHtmlResult result);
	private delegate BtResult DecodeImageDelegate(byte[] data, long len, ref BtDecodeImageResult result);
	private delegate void ReleaseDecodeImageDelegate(ref BtDecodeImageResult result);
	private delegate BtResult HighlightDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string path, [MarshalAs(UnmanagedType.LPUTF8Str)] string code, BtRange[] ranges, long rangesLen, ref BtHighlightedDiff result);
	private delegate void ReleaseHighlightDelegate(ref BtHighlightedDiff result);
	private delegate BtCommitGraphCache NewCacheDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string id);
	private delegate void ReleaseCacheDelegate(ref BtCommitGraphCache cache);
	private delegate BtCancellationToken NewTokenDelegate();
	private delegate void ReleaseTokenDelegate(ref BtCancellationToken token);
	private delegate BtResult GetCommitsDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] tips, long tipsLen, bool dateOrder, long pageSize, long skipPages, long minPages, BtOid[] requiredOids, long requiredOidsLen, ref BtCommitGraphCache cache, ref BtCancellationToken token, ref BtCommitStorage result);
	private delegate void ReleaseCommitStorageDelegate(ref BtCommitStorage storage);
	private delegate BtResult GetCommitterTimesDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] oids, long oidsLen, ref BtCommitGraphCache cache, ref BtCommitterTimes result);
	private delegate void ReleaseCommitterTimesDelegate(ref BtCommitterTimes result);
	private delegate BtResult GetBehindAheadCountsDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOidPair[] pairs, long pairsLen, ref BtCommitGraphCache cache, ref BtBehindAheadCounts result);
	private delegate void ReleaseBehindAheadCountsDelegate(ref BtBehindAheadCounts result);

	private enum BtResult
	{
		Ok,
		Err,
		ErrCanceled,
		ErrNotFound
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtOid : IEquatable<BtOid>
	{
		public uint s0;
		public uint s1;
		public uint s2;
		public uint s3;
		public uint s4;
		public bool Equals(BtOid other) => s0 == other.s0 && s1 == other.s1 && s2 == other.s2 && s3 == other.s3 && s4 == other.s4;
		public override string ToString() => $"{s0:x8}{s1:x8}{s2:x8}{s3:x8}{s4:x8}";
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtOidPair
	{
		public BtOid left;
		public BtOid right;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtHead
	{
		public BtOid DetachedHead;
		public IntPtr Reference;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtReferences
	{
		public IntPtr names_data;
		public long names_data_len;
		public long names_data_cap;
		public IntPtr names_offsets;
		public long names_offsets_len;
		public long names_offsets_cap;
		public IntPtr oids;
		public long oids_len;
		public long oids_cap;
		public IntPtr symrefs_data;
		public long symrefs_data_len;
		public long symrefs_data_cap;
		public IntPtr symrefs_offsets;
		public long symrefs_offsets_len;
		public long symrefs_offsets_cap;
		public ulong hash;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtRange
	{
		public uint start;
		public uint end;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtHighlighedRange
	{
		public BtRange range_utf16;
		public byte style;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtHighlightedDiff
	{
		public IntPtr items;
		public long items_len;
		public long items_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtDecodeImageResult
	{
		public IntPtr data;
		public long data_len;
		public long data_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtTree
	{
		public IntPtr entries;
		public long entries_len;
		public long entries_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtTreeItem
	{
		public ushort kind;
		public IntPtr filename;
		public BtOid treeish;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtTagDetails
	{
		public BtOid tag_object_oid;
		public IntPtr tagger_name;
		public IntPtr tagger_email;
		public long tagger_time;
		public IntPtr name;
		public IntPtr message;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtStash
	{
		public int reflog_id;
		public BtOid oid;
		public BtOid first_parent;
		public long author_index;
		public long author_time;
		public IntPtr subject;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtRepositoryStashes
	{
		public IntPtr stashes;
		public long stashes_len;
		public long stashes_cap;
		public IntPtr identities;
		public long identities_len;
		public long identities_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtSearchCommitsResult
	{
		public IntPtr matches;
		public long matches_len;
		public long matches_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtGitConfig
	{
		public IntPtr sections;
		public long sections_len;
		public long sections_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtGitConfigSection
	{
		public IntPtr name;
		public IntPtr sub_section;
		public IntPtr variables;
		public long variables_len;
		public long variables_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtGitConfigVariable
	{
		public IntPtr name;
		public IntPtr value;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtIdentity
	{
		public IntPtr name;
		public IntPtr email;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtRevisionHeader
	{
		public long author_index;
		public long author_time;
		public IntPtr subject;
		public byte has_body;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtRevisionHeaders
	{
		public IntPtr revisions;
		public long revisions_len;
		public long revisions_cap;
		public IntPtr identities;
		public long identities_len;
		public long identities_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtCommitGraphCache
	{
		public IntPtr inner;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtCancellationToken
	{
		public IntPtr inner;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtCommitStorage
	{
		public IntPtr oids;
		public long oids_len;
		public long oids_cap;
		public IntPtr indexes;
		public long indexes_len;
		public long indexes_cap;
		public byte has_more;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtCommitterTimes
	{
		public IntPtr times;
		public long times_len;
		public long times_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtBehindAheadCount
	{
		public uint left;
		public uint right;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtBehindAheadCounts
	{
		public IntPtr items;
		public long items_len;
		public long items_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtPatchToken : IEquatable<BtPatchToken>
	{
		public byte kind;
		public uint start;
		public uint end;
		public bool Equals(BtPatchToken other) => kind == other.kind && start == other.start && end == other.end;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtParsePatchResult
	{
		public IntPtr tokens;
		public long tokens_len;
		public long tokens_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtRect
	{
		public double x;
		public double y;
		public double w;
		public double h;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtTreemapItem
	{
		public long index;
		public BtRect rect;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtLayoutTreemapResult
	{
		public IntPtr items;
		public long items_len;
		public long items_cap;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BtMdToHtmlResult
	{
		public IntPtr html;
	}

	private static class Original
	{
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_oid_from_str(byte[] sha, ref BtOid result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_head([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, ref BtHead result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_head(ref BtHead result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_references([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, bool skipTags, ref BtReferences result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_references(ref BtReferences result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_git_config([MarshalAs(UnmanagedType.LPUTF8Str)] string configPath, ref BtGitConfig result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_git_config(ref BtGitConfig result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_revision_headers([MarshalAs(UnmanagedType.LPUTF8Str)] string workTree, [MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] oids, long oidsLen, ref BtRevisionHeaders result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_revision_headers(ref BtRevisionHeaders result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtCommitGraphCache bt_new_commit_graph_cache([MarshalAs(UnmanagedType.LPUTF8Str)] string id);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_commit_graph_cache(ref BtCommitGraphCache cache);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtCancellationToken bt_new_cancellation_token();
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_cancellation_token(ref BtCancellationToken token);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_commits([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] tips, long tipsLen, bool dateOrder, long pageSize, long skipPages, long minPages, BtOid[] requiredOids, long requiredOidsLen, ref BtCommitGraphCache cache, ref BtCancellationToken token, ref BtCommitStorage result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_commit_storage(ref BtCommitStorage result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_committer_times([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] oids, long oidsLen, ref BtCommitGraphCache cache, ref BtCommitterTimes result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_committer_times(ref BtCommitterTimes result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_behind_ahead_counts([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOidPair[] pairs, long pairsLen, ref BtCommitGraphCache cache, ref BtBehindAheadCounts result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_behind_ahead_counts(ref BtBehindAheadCounts result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_parse_patch(byte[] patch, ulong patchLen, byte[] srcPrefix, ulong srcPrefixLen, byte[] dstPrefix, ulong dstPrefixLen, ref BtParsePatchResult result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_parse_patch(ref BtParsePatchResult result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_layout_treemap(long[] sizes, long sizesLen, BtRect rect, ref BtLayoutTreemapResult result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_layout_treemap(ref BtLayoutTreemapResult result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_md_to_html([MarshalAs(UnmanagedType.LPUTF8Str)] string markdown, ref BtMdToHtmlResult result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_md_to_html(ref BtMdToHtmlResult result);
		[DllImport("biturbo.original.dll")] public static extern BtResult bt_decode_image(byte[] data, long len, ref BtDecodeImageResult result);
		[DllImport("biturbo.original.dll")] public static extern void bt_release_decode_image(ref BtDecodeImageResult result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_highlight_syntax([MarshalAs(UnmanagedType.LPUTF8Str)] string path, [MarshalAs(UnmanagedType.LPUTF8Str)] string code, BtRange[] ranges, long rangesLen, ref BtHighlightedDiff result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_highlight_syntax(ref BtHighlightedDiff result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_tree([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, ref BtOid oid, ref BtTree result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_tree(ref BtTree result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_tag_details([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid oid, ref BtTagDetails result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_tag_details(ref BtTagDetails result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_repository_stashes([MarshalAs(UnmanagedType.LPUTF8Str)] string workTree, [MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, ref BtRepositoryStashes result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_repository_stashes(ref BtRepositoryStashes result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_search_commits([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] oids, long oidsLen, [MarshalAs(UnmanagedType.LPUTF8Str)] string query, BtOid[] refMatches, long refMatchesLen, ref BtCancellationToken token, ref BtSearchCommitsResult result);
		[DllImport("biturbo.original.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_search_commits(ref BtSearchCommitsResult result);
	}

	private static class New
	{
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_oid_from_str(byte[] sha, ref BtOid result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_head([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, ref BtHead result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_head(ref BtHead result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_references([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, bool skipTags, ref BtReferences result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_references(ref BtReferences result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_git_config([MarshalAs(UnmanagedType.LPUTF8Str)] string configPath, ref BtGitConfig result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_git_config(ref BtGitConfig result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_revision_headers([MarshalAs(UnmanagedType.LPUTF8Str)] string workTree, [MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] oids, long oidsLen, ref BtRevisionHeaders result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_revision_headers(ref BtRevisionHeaders result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtCommitGraphCache bt_new_commit_graph_cache([MarshalAs(UnmanagedType.LPUTF8Str)] string id);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_commit_graph_cache(ref BtCommitGraphCache cache);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtCancellationToken bt_new_cancellation_token();
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_cancellation_token(ref BtCancellationToken token);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_commits([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] tips, long tipsLen, bool dateOrder, long pageSize, long skipPages, long minPages, BtOid[] requiredOids, long requiredOidsLen, ref BtCommitGraphCache cache, ref BtCancellationToken token, ref BtCommitStorage result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_commit_storage(ref BtCommitStorage result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_committer_times([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] oids, long oidsLen, ref BtCommitGraphCache cache, ref BtCommitterTimes result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_committer_times(ref BtCommitterTimes result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_behind_ahead_counts([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOidPair[] pairs, long pairsLen, ref BtCommitGraphCache cache, ref BtBehindAheadCounts result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_behind_ahead_counts(ref BtBehindAheadCounts result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_parse_patch(byte[] patch, ulong patchLen, byte[] srcPrefix, ulong srcPrefixLen, byte[] dstPrefix, ulong dstPrefixLen, ref BtParsePatchResult result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_parse_patch(ref BtParsePatchResult result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_layout_treemap(long[] sizes, long sizesLen, BtRect rect, ref BtLayoutTreemapResult result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_layout_treemap(ref BtLayoutTreemapResult result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_md_to_html([MarshalAs(UnmanagedType.LPUTF8Str)] string markdown, ref BtMdToHtmlResult result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_md_to_html(ref BtMdToHtmlResult result);
		[DllImport("biturbo.new.dll")] public static extern BtResult bt_decode_image(byte[] data, long len, ref BtDecodeImageResult result);
		[DllImport("biturbo.new.dll")] public static extern void bt_release_decode_image(ref BtDecodeImageResult result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_highlight_syntax([MarshalAs(UnmanagedType.LPUTF8Str)] string path, [MarshalAs(UnmanagedType.LPUTF8Str)] string code, BtRange[] ranges, long rangesLen, ref BtHighlightedDiff result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_highlight_syntax(ref BtHighlightedDiff result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_tree([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, ref BtOid oid, ref BtTree result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_tree(ref BtTree result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_tag_details([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid oid, ref BtTagDetails result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_tag_details(ref BtTagDetails result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_get_repository_stashes([MarshalAs(UnmanagedType.LPUTF8Str)] string workTree, [MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, ref BtRepositoryStashes result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_repository_stashes(ref BtRepositoryStashes result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern BtResult bt_search_commits([MarshalAs(UnmanagedType.LPUTF8Str)] string gitDir, BtOid[] oids, long oidsLen, [MarshalAs(UnmanagedType.LPUTF8Str)] string query, BtOid[] refMatches, long refMatchesLen, ref BtCancellationToken token, ref BtSearchCommitsResult result);
		[DllImport("biturbo.new.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void bt_release_search_commits(ref BtSearchCommitsResult result);
	}
}
