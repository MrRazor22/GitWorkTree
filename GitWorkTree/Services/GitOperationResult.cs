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

    public sealed class GitOperationResult<T>
    {
        public bool Success { get; }
        public T Value { get; }
        public string ErrorMessage { get; }

        public GitOperationResult(bool success, T value, string errorMessage = "")
        {
            Success = success;
            Value = value;
            ErrorMessage = errorMessage;
        }
    }
}
