using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ForkPlus.Biturbo
{
	public static class Bt
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void ReadLineCallback(IntPtr cb_target_ptr, byte kind, IntPtr data_ptr, long data_len);

		private const string BiTurboDll = "biturbo.dll";

		// .NET 10 跨平台运行时：[DllImport("biturbo.dll")] 的字面 DLL 名在
		// Linux/macOS 上找不到（Biturbo release 发布的是 libbiturbo.so / libbiturbo.dylib）。
		// 注册 DllImportResolver 在静态构造里按 OS 重定向到对应原生库，
		// 51 个 [DllImport("biturbo.dll", ...)] 声明完全不用改（DllImport 的
		// LibraryName 必须是编译期常量，故只能用 resolver 在运行时改写）。
		static Bt()
		{
			NativeLibrary.SetDllImportResolver(typeof(Bt).Assembly, ResolveBiturbo);
		}

		private static IntPtr ResolveBiturbo(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
		{
			if (libraryName != BiTurboDll)
			{
				return IntPtr.Zero;
			}
			string actual = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "biturbo.dll"
				: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libbiturbo.dylib"
				: "libbiturbo.so";
			return NativeLibrary.Load(actual, assembly, searchPath);
		}

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_oid_from_str(byte[] sha_string, ref BtOid out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern long bt_get_last_error_message(IntPtr buffer, ulong length);

		[DllImport("biturbo.dll")]
		public static extern BtResult bt_decode_image(byte[] image_data_ptr, long image_data_len, ref BtDecodeImageResult out_result);

		[DllImport("biturbo.dll")]
		public static extern void bt_release_decode_image(ref BtDecodeImageResult decode_image_result_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_commits([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, BtOid[] tips_ptr, long tips_len, bool date_order, long page_size, long skip_pages, long min_pages, BtOid[] required_oids_ptr, long required_oids_len, ref BtCommitGraphCache commit_graph_cache_ptr, ref BtCancellationToken cancellation_token_ptr, ref BtCommitStorage out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_commit_subgraph([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, ref BtOid oid, ref BtCommitGraphCache commit_graph_cache_ptr, ref BtCommitStorage out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_commit_subgraph_2([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, ref BtOid src, ref BtOid dst, ref BtCommitGraphCache commit_graph_cache_ptr, ref BtCommitStorage out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_commit_storage(ref BtCommitStorage bt_commit_storage_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_find_fartherest_tip([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, ref BtOid head_oid, BtOid[] tips_ptr, long tips_len, ref BtOid base_oid, ref BtCommitGraphCache commit_graph_cache_ptr, ref BtOid out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_behind_ahead_counts([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, BtOidPair[] oid_pairs_ptr, long oid_pairs_len, ref BtCommitGraphCache commit_graph_cache_ptr, ref BtBehindAheadCounts out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_behind_ahead_counts(ref BtBehindAheadCounts bt_behind_ahead_counts_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_committer_times([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, BtOid[] oids_ptr, long oids_len, ref BtCommitGraphCache commit_graph_cache_ptr, ref BtCommitterTimes out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_committer_times(ref BtCommitterTimes bt_committer_times);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_git_config([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string path, ref BtGitConfig out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_git_config(ref BtGitConfig bt_git_config);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_repository_manager([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string path, ref BtRepositoryManager out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_repository_manager(ref BtRepositoryManager bt_repo_manager);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_save_repository_manager([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string path, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.Utf8ArrayMarshaler")] string[] source_dirs_ptr, long source_dirs_len, byte scan_depth, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.Utf8ArrayMarshaler")] string[] ignore_ptr, long ignore_len, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.Utf8ArrayMarshaler")] string[] paths_ptr, long paths_len, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.Utf8ArrayMarshaler")] string[] aliases_ptr, long aliases_len, uint[] opened_ptr, long opened_len, byte[] colors_ptr, long colors_len);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_tag_details([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, BtOid tag_oid, ref BtTagDetails out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_tag_details(ref BtTagDetails tag_details);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_references([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, bool skip_tags, ref BtReferences out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_references(ref BtReferences bt_references_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_tree([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, ref BtOid oid, ref BtTree out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_tree(ref BtTree bt_tree_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_head([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, ref BtHead out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_head(ref BtHead head);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_revision_headers([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string working_dir_path, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, BtOid[] oids_ptr, long oids_len, ref BtRevisionHeaders out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_revision_headers(ref BtRevisionHeaders revision_headers);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_get_repository_stashes([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string working_dir_path, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, ref BtRepositoryStashes out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_repository_stashes(ref BtRepositoryStashes repository_stashes);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_highlight_syntax([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string file_path, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string diff, BtRange[] ranges_ptr, long ranges_len, ref BtHighlightedDiff out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_highlight_syntax(ref BtHighlightedDiff highlightedDiffPtr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_layout_treemap(long[] sizes_ptr, long sizes_len, BtRect rect, ref BtLayoutTreemapResult out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_layout_treemap(ref BtLayoutTreemapResult layout_treemap_result_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_md_to_html([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string md_utf8, ref BtMdToHtmlResult out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_md_to_html(ref BtMdToHtmlResult md_to_html_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtCancellationToken bt_new_cancellation_token();

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_cancel_cancellation_token(ref BtCancellationToken cancellation_token_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_cancellation_token(ref BtCancellationToken cancellation_token_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtProcessCancellationToken bt_new_process_cancellation_token();

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_kill_process_cancellation_token(ref BtProcessCancellationToken process_cancellation_token_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_process_cancellation_token(ref BtProcessCancellationToken process_cancellation_token_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtCommitGraphCache bt_new_commit_graph_cache([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string identifier);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_commit_graph_cache(ref BtCommitGraphCache commit_graph_cache_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_parse_patch(byte[] patch_utf8, ulong patch_utf8_len, byte[] src_prefix_utf8, ulong src_prefix_utf8_len, byte[] dst_prefix_utf8, ulong dst_prefix_utf8_len, ref BtParsePatchResult out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_parse_patch(ref BtParsePatchResult bt_parse_patch_ptr);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_search_commits([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string git_dir_path, BtOid[] oids_ptr, long oids_len, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string query, BtOid[] ref_matches_ptr, long ref_matches_len, ref BtCancellationToken cancellation_token_ptr, ref BtSearchCommitsResult out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_search_commits(ref BtSearchCommitsResult search_commits_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_spawn_with_output([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string path, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string current_dir, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.Utf8ArrayMarshaler")] string[] args_ptr, long args_len, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.Utf8ArrayMarshaler")] string[] env_ptr, long evn_len, byte[] stdin_ptr, long stdin_len, ref BtSpawnWithOutputResult out_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void bt_release_spawn_with_output_result(ref BtSpawnWithOutputResult bt_spawn_with_output_result);

		[DllImport("biturbo.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern BtResult bt_spawn_with_callback([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string path, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.UTF8StringCodec")] string current_dir, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.Utf8ArrayMarshaler")] string[] args_ptr, long args_len, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "ForkPlus.Biturbo.Utf8ArrayMarshaler")] string[] env_ptr, long evn_len, byte[] stdin_ptr, long stdin_len, IntPtr cb_target_ptr, ReadLineCallback read_line_cb, ref BtProcessCancellationToken process_cancellation_token_ptr, ref BtSpawnWithCallbackResult out_result);
	}
}
