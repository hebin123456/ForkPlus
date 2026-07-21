namespace ForkPlus.UI
{
	/// <summary>
	/// Phase 0.4 从 <c>src/ForkPlus/UI/Workspace.cs</c> 迁入 Core。
	/// 被 <see cref="ForkPlus.Settings.ForkPlusSettings.WorkspacesSettings"/> 用作工作区数据模型。
	/// 无 WPF 依赖，零修改迁移。
	/// </summary>
	public class Workspace
	{
		public string Name { get; }

		public string[] Repositories { get; set; }

		[Null]
		public string ActiveRepository { get; set; }

		public Workspace(string name, string[] repositories, [Null] string activeRepository)
		{
			Name = name;
			Repositories = repositories;
			ActiveRepository = activeRepository;
		}
	}
}
