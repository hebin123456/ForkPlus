namespace ForkPlus.Git
{
	/// <summary>提交签名状态，映射 git pretty format 占位符 <c>%G?</c> 的单字符输出：
	/// G=有效签名；B=坏签名；U=有效但未知有效性；X=签名已过期；Y=密钥已过期；R=密钥已吊销；
	/// E=无法验证（如缺少 gpg）；N=无签名。未知字符归为 <see cref="Error"/>（UI 灰显）。</summary>
	public enum CommitSignatureStatus
	{
		Unknown,
		Good,
		Bad,
		Unverified,
		Expired,
		ExpiredKey,
		RevokedKey,
		Error,
		None
	}
}
