namespace GitWorkTree.Services
{
    public sealed class GitOperationResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }

        public GitOperationResult(bool success, string errorMessage = "")
        {
            Success = success;
            ErrorMessage = errorMessage;
        }
    }
}
