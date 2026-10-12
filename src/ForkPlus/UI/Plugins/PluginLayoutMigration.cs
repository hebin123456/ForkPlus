using System;
using System.IO;

namespace ForkPlus.UI.Plugins
{
	/// <summary>
	/// v5.1.0：插件布局一次性迁移。旧版（≤ v5.0.7）插件是<strong>平铺</strong>在
	/// <c>plugins/</c> 根目录下的 DLL；新版要求每个插件独占目录（<c>plugins/&lt;AssemblyName&gt;/</c>）
	/// 且带 <c>plugin.json</c> 清单。旧布局的插件不具备兼容字段，新版一律不再支持。
	///
	/// 迁移策略（用户确认）：检测到旧布局即**清空整个 plugins/ 目录**，由调用方随后弹窗告知用户
	/// 「旧插件已移除，请从插件仓 1.2.0 重新安装」。检测依据是「plugins/ 根目录存在 *.dll 文件」——
	/// 新布局下插件 DLL 都在子目录内，根目录出现 DLL 必是旧版遗留；因此本迁移天然幂等，无需落库标记。
	/// </summary>
	public static class PluginLayoutMigration
	{
		/// <summary>本次启动是否执行了迁移（供调用方决定是否弹窗告知）。</summary>
		public static bool Migrated { get; private set; }

		/// <summary>
		/// 若检测到旧布局则清空 plugins/ 目录并返回 true；否则返回 false。
		/// 必须早于 <see cref="DiffViewPluginLoader.LoadDefault"/> 调用。
		/// </summary>
		public static bool RunIfNeeded()
		{
			Migrated = false;
			try
			{
				string directory = DiffViewPluginLoader.DefaultPluginDirectory;
				if (!Directory.Exists(directory))
				{
					return false;
				}
				// 新布局：根目录只应有子目录，不应有 DLL（含 .pending-uninstall 这类标记文件）。
				if (Directory.GetFiles(directory, "*.dll").Length == 0)
				{
					return false;
				}
				ClearDirectory(directory);
				Migrated = true;
				Log.Info("Plugin layout migrated: legacy flat plugins/ cleared for v5.1.0 per-plugin directories");
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to migrate legacy plugin layout", ex);
				return false;
			}
		}

		/// <summary>删除目录下的全部文件与子目录（保留目录本身）。</summary>
		private static void ClearDirectory(string directory)
		{
			foreach (string file in Directory.GetFiles(directory))
			{
				try
				{
					File.SetAttributes(file, FileAttributes.Normal);
					File.Delete(file);
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to delete legacy plugin file '" + file + "'", ex);
				}
			}
			foreach (string subDirectory in Directory.GetDirectories(directory))
			{
				try
				{
					Directory.Delete(subDirectory, recursive: true);
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to delete legacy plugin directory '" + subDirectory + "'", ex);
				}
			}
		}
	}
}