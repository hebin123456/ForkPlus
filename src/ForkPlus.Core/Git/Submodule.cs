using System;

namespace ForkPlus.Git
{
    public class Submodule
    {
        public static readonly SubmoduleComparer Comparer = new SubmoduleComparer(StringComparer.Ordinal);

        public string Path { get; }

        public bool IsActive { get; }

        public string FriendlyName
        {
            get
            {
                try
                {
                    return System.IO.Path.GetFileName(Path);
                }
                catch
                {
                    return Path;
                }
            }
        }

        public Submodule(string path, bool isActive)
        {
            Path = path;
            IsActive = isActive;
        }

        public bool SubmoduleEquals(Submodule submodule)
        {
            if (Path == submodule.Path)
            {
                return IsActive == submodule.IsActive;
            }
            return false;
        }
    }
}
