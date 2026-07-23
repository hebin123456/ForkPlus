namespace ForkPlus.Services
{
	/// <summary>
	/// 文件关联查询抽象（替换 <see cref="ForkPlus.UI.Commands.OpenFileInDefaultEditorCommand"/>
	/// 中的 <c>Shlwapi.dll!AssocQueryString</c> P/Invoke）。
	/// Windows 实现查询注册表文件关联；Linux/macOS 实现可查询 mime 类型或始终返回 true（由 xdg-open 决定）。
	/// </summary>
	public interface IFileAssociationService
	{
		/// <summary>查询与指定扩展名关联的可执行程序路径（如 ".txt" → "C:\...\notepad.exe"）。无关联返回 null。</summary>
		string GetAssociatedExecutable(string extension);

		/// <summary>判断该扩展名是否有可用的默认编辑器。</summary>
		bool IsEditorAvailable(string extension);
	}
}
