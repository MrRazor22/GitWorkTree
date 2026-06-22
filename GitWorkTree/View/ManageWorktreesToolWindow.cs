using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.VisualStudio.Shell;

namespace GitWorkTree.View
{
    [Guid("98d36b44-9cb6-41fb-a1e6-b9a35e40e2cd")]
    public class ManageWorktreesToolWindow : ToolWindowPane
    {
        public ManageWorktreesToolWindow() : base(null)
        {
            this.Caption = "Manage Worktrees";
            this.Content = new ManageWorktreesControl();
        }
    }
}
