using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace GitWorkTree.Helpers
{
    public static class DialogHelper
    {
        public static void ShowOperationError(IServiceProvider serviceProvider, string operationName, string errorMessage)
        {
            string displayMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? $"Unable to {operationName.ToLower()}.\n\nSee Output window for additional details."
                : $"Unable to {operationName.ToLower()}.\n\n{errorMessage}\n\nSee Output window for additional details.";

            if (serviceProvider == null)
            {
                // No VS shell in test environments — skip silently.
                // Production always passes a real provider via CommandExecutor.
                return;
            }

            VsShellUtilities.ShowMessageBox(
                serviceProvider,
                displayMessage,
                $"{operationName} Failed",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
