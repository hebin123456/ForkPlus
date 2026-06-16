using System;

namespace ForkPlus.Git.Commands
{
    public class GitCommandError
    {
        public class GenericError : GitCommandError
        {
            public string Message { get; }

            public override string FriendlyDescription => Message;

            public GenericError(string message)
            {
                Message = message;
            }
        }

        public class NotFound : GitCommandError
        {
            [Null]
            public string Message { get; }

            public override string FriendlyDescription => Message ?? "[internal] Not found";

            public NotFound([Null] string message = null)
            {
                Message = message;
            }
        }

        public class Cancelled : GitCommandError
        {
            public override string FriendlyDescription => "[internal] Cancelled";
        }

        public class ChangesAreTooLarge : GitCommandError
        {
            public long FileSize { get; }

            public override string FriendlyDescription => "Changes are too large to display";

            public ChangesAreTooLarge(long fileSize)
            {
                FileSize = fileSize;
            }
        }

        public class WorkingDirectoryIsDirty : GitCommandError
        {
            public override string FriendlyDescription => "Error: Working tree contains changes";
        }

        public class ParseError : GitCommandError
        {
            public string ErrorMessage { get; }

            public override string FriendlyDescription => ErrorMessage;

            public ParseError(string stderr)
            {
                ErrorMessage = stderr;
            }
        }

        public class Bug : GitCommandError
        {
            public string Message { get; }

            public override string FriendlyDescription => "[bug] " + Message;

            public Bug(string message)
            {
                Message = message;
            }
        }

        public class FileIsBusy : GitCommandError
        {
            public string FilePath { get; }

            public override string FriendlyDescription => "[internal] File is busy '" + FilePath + "'";

            public FileIsBusy(string filePath)
            {
                FilePath = filePath;
            }
        }

        public class UnknownException : GitCommandError
        {
            public Exception Exception { get; }

            public override string FriendlyDescription => $"[Internal]: {Exception}";

            public UnknownException(Exception ex)
            {
                Exception = ex;
            }
        }

        public class GitError : GitCommandError
        {
            public string FullOutput { get; }
            public string Stderr { get; }

            public override string FriendlyDescription => FullOutput ?? Stderr ?? string.Empty;

            public GitError(string fullOutput, string stderr)
            {
                FullOutput = fullOutput;
                Stderr = stderr;
            }

            public GitError(string stderr)
            {
                FullOutput = stderr;
                Stderr = stderr;
            }

            public override string ToString()
            {
                return FullOutput;
            }
        }

        public class CallbackUnknownError : GitCommandError
        {
            public string FullOutput { get; }

            public override string FriendlyDescription => FullOutput;

            public CallbackUnknownError(string fullOutput)
            {
                FullOutput = fullOutput;
            }
        }

        public class CommitFailed : GitCommandError
        {
            public string Message { get; }
            public bool Amend { get; }
            public bool CommitAndPush { get; }
            public string Stderr { get; }

            public override string FriendlyDescription => "[Internal]: git commit failed";

            public CommitFailed(string message, bool amend, bool commitAndPush, string stderr)
            {
                Message = message;
                Amend = amend;
                CommitAndPush = commitAndPush;
                Stderr = stderr;
            }
        }

        public abstract string FriendlyDescription { get; }
    }
}
