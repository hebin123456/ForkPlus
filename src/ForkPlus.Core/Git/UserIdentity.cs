namespace ForkPlus.Git
{
    public class UserIdentity
    {
        public static readonly UserIdentity Dummy = new UserIdentity("dummy", "dummy@dummy.com");

        public string Name { get; }
        public string Email { get; }

        public UserIdentity(string name, string email)
        {
            Name = name;
            Email = email;
        }
    }
}
