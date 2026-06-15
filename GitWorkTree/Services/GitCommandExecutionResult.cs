namespace GitWorkTree.Services
{
    public sealed class GitCommandExecutionResult
    {
        public bool Success { get; }
        public string StandardError { get; }

        public GitCommandExecutionResult(bool success, string standardError = "")
        {
            Success = success;
            StandardError = standardError;
        }
    }
}
