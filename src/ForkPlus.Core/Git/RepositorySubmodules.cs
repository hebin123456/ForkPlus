namespace ForkPlus.Git
{
    public class RepositorySubmodules
    {
        public static readonly RepositorySubmodules Empty = new RepositorySubmodules(new Submodule[0]);

        public Submodule[] Submodules { get; }

        public RepositorySubmodules(Submodule[] submodules)
        {
            Submodules = submodules;
        }
    }
}
