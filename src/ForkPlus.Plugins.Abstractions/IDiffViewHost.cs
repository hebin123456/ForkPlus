using System.IO;

namespace ForkPlus.Plugins.Abstractions
{
	/// <summary>LFS smudge 结果。</summary>
	public sealed class LfsSmudgeResult
	{
		public bool Succeeded { get; }

		/// <summary>smudge 成功时的真身字节；失败为 null。</summary>
		public MemoryStream Data { get; }

		/// <summary>失败时的用户可读错误描述。</summary>
		public string Error { get; }

		private LfsSmudgeResult(bool succeeded, MemoryStream data, string error)
		{
			Succeeded = succeeded;
			Data = data;
			Error = error;
		}

		public static LfsSmudgeResult Success(MemoryStream data)
		{
			return new LfsSmudgeResult(true, data, null);
		}

		public static LfsSmudgeResult Failure(string error)
		{
			return new LfsSmudgeResult(false, null, error);
		}
	}

	/// <summary>
	/// 宿主能力桥（v5.0.0 插件化架构）：插件的一切外部副作用都从这里走——
	/// LFS smudge/缓存、biturbo 图片解码、保存对话框、错误弹窗。
	/// 宿主实现里封装 JobQueue/对话框/Git 命令，插件保持纯 UI 逻辑。
	/// </summary>
	public interface IDiffViewHost
	{
		/// <summary>
		/// 图片字节就绪前的宿主侧预处理（当前为 .tga 经 biturbo 原生解码；其余原样返回）。
		/// 失败时返回 null（宿主已记日志）。
		/// </summary>
		MemoryStream PrepareImageStream(string path, MemoryStream raw);

		/// <summary>取 LFS 本地缓存（.git/lfs/objects 命中时直接给字节；未命中/失败返回 null）。</summary>
		MemoryStream GetCachedLfsData(LfsRef lfs);

		/// <summary>
		/// 启动 LFS smudge 任务：progress 在 UI 线程回调（null 表示不确定进度），
		/// completed 亦在 UI 线程回调。返回值 Dispose 即取消（对应原「取消 LFS 下载」按钮）。
		/// </summary>
		System.IDisposable RunLfsSmudge(LfsRef lfs, System.Action<double?> progress, System.Action<LfsSmudgeResult> completed);

		/// <summary>保存对话框 + 写文件（suggestedFileName 为建议名）。用户取消返回 false。</summary>
		bool SaveFileAs(string suggestedFileName, MemoryStream data);

		/// <summary>弹出错误窗（宿主样式）。</summary>
		void ShowError(string message);
	}
}
