using ForkPlus.Accounts;
using ForkPlus.UI.Helpers;
using System.Windows.Media;
using ForkPlus.UI;

namespace ForkPlus.Git
{
	public partial class Remote
	{
		/// <summary>
		/// Remote.Account 在 Phase 0.2c 迁移到 Core 后类型变为 IAccount（接口反转）。
		/// UI 层需要访问 Account.Service / Account.Email 等 IAccount 未暴露的成员时，
		/// 通过此属性强转回具体 Account 类型。Phase 0.5 迁移 Account 到 Core 后可移除。
		/// </summary>
		[Null]
		public Account AccountConcrete => (Account)Account;

		public ImageSource Icon => Theme.FindImage(IconKey) ?? Theme.RemoteIcon;

		public Geometry IconGeometry => Theme.FindGeometry(IconGeometryKey) ?? Theme.RemoteGeometry;
	}
}
