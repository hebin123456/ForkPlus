using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Git;
using Newtonsoft.Json;

namespace ForkPlus.Undo
{
	/// <summary>
	/// v3.3.0：.git/forkplus-undo-index.json 的读写。
	///
	/// 存储 {HeadSha → UndoIndexEntry} 映射，为 reflog 条目附加 UI 友好的操作名。
	///
	/// 设计要点：
	/// - 文件位置：&lt;gitdir&gt;/forkplus-undo-index.json（与 reflog 同生命周期，clone 后是空的）
	/// - 文件损坏时静默删除重建，不阻断 Undo/Redo
	/// - 容量上限：默认保留最近 500 条（防止无限增长），LRU 淘汰
	/// - 写入是原子的（先写临时文件再 rename），避免崩溃导致文件损坏
	/// </summary>
	public class UndoIndexStore
	{
		public const string IndexFileName = "forkplus-undo-index.json";
		public const int DefaultCapacity = 500;

		private readonly GitModule _gitModule;
		private readonly int _capacity;
		private Dictionary<string, UndoIndexEntry> _cache;

		public UndoIndexStore(GitModule gitModule, int capacity = DefaultCapacity)
		{
			_gitModule = gitModule;
			_capacity = capacity;
		}

		/// <summary>索引文件绝对路径。gitModule 为 null 时返回 null。</summary>
		public string GetIndexPath()
		{
			if (_gitModule == null)
			{
				return null;
			}
			try
			{
				string gitDir = _gitModule.GitDir();
				if (string.IsNullOrEmpty(gitDir))
				{
					return null;
				}
				return Path.Combine(gitDir, IndexFileName);
			}
			catch
			{
				return null;
			}
		}

		/// <summary>读取索引。文件不存在或损坏时返回空字典（不抛异常）。</summary>
		public Dictionary<string, UndoIndexEntry> Load()
		{
			if (_cache != null)
			{
				return _cache;
			}
			_cache = new Dictionary<string, UndoIndexEntry>();
			string path = GetIndexPath();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return _cache;
			}
			try
			{
				string json = File.ReadAllText(path);
				if (string.IsNullOrWhiteSpace(json))
				{
					return _cache;
				}
				List<UndoIndexEntry> list = JsonConvert.DeserializeObject<List<UndoIndexEntry>>(json);
				if (list != null)
				{
					foreach (UndoIndexEntry entry in list)
					{
						if (entry != null && !string.IsNullOrEmpty(entry.HeadSha))
						{
							_cache[entry.HeadSha] = entry;
						}
					}
				}
			}
			catch
			{
				// 文件损坏：静默删除重建
				TryDeleteFile(path);
				_cache = new Dictionary<string, UndoIndexEntry>();
			}
			return _cache;
		}

		/// <summary>记录一条操作。已存在同 HeadSha 的条目会被覆盖。</summary>
		public void Record(UndoIndexEntry entry)
		{
			if (entry == null || string.IsNullOrEmpty(entry.HeadSha))
			{
				return;
			}
			Dictionary<string, UndoIndexEntry> index = Load();
			index[entry.HeadSha] = entry;
			TrimToCapacity(index);
			Save(index);
		}

		/// <summary>查询某 HeadSha 对应的 UI 友好操作名。找不到返回 null。</summary>
		public UndoIndexEntry Lookup(string headSha)
		{
			if (string.IsNullOrEmpty(headSha))
			{
				return null;
			}
			Dictionary<string, UndoIndexEntry> index = Load();
			return index.TryGetValue(headSha, out UndoIndexEntry entry) ? entry : null;
		}

		/// <summary>持久化索引到磁盘。原子写入：先写临时文件再 rename。</summary>
		private void Save(Dictionary<string, UndoIndexEntry> index)
		{
			string path = GetIndexPath();
			if (string.IsNullOrEmpty(path))
			{
				return;
			}
			try
			{
				// 转成 List 序列化（JSON 数组形式，便于人读和未来扩展）
				List<UndoIndexEntry> list = new List<UndoIndexEntry>(index.Values);
				// 按时间倒序排（最近在前）
				list.Sort((a, b) => DateTime.Compare(b.TimestampUtc, a.TimestampUtc));
				string json = JsonConvert.SerializeObject(list, Formatting.Indented);

				// 原子写入：先写到 .tmp，再 rename 覆盖
				string tmpPath = path + ".tmp";
				File.WriteAllText(tmpPath, json);
				if (File.Exists(path))
				{
					File.Delete(path);
				}
				File.Move(tmpPath, path);
			}
			catch
			{
				// 静默：写入失败不阻断 Undo/Redo，下次启动会重建
			}
		}

		/// <summary>超出容量时淘汰最旧的条目。</summary>
		private void TrimToCapacity(Dictionary<string, UndoIndexEntry> index)
		{
			if (index.Count <= _capacity)
			{
				return;
			}
			// 按 TimestampUtc 升序排，删除最早的 (count - capacity) 条
			List<UndoIndexEntry> sorted = new List<UndoIndexEntry>(index.Values);
			sorted.Sort((a, b) => DateTime.Compare(a.TimestampUtc, b.TimestampUtc));
			int removeCount = index.Count - _capacity;
			for (int i = 0; i < removeCount; i++)
			{
				index.Remove(sorted[i].HeadSha);
			}
		}

		private static void TryDeleteFile(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch
			{
				// 静默
			}
		}
	}
}
