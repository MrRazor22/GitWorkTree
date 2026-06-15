using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using GitWorkTree.ViewModel;
using GitWorkTree.Services;

namespace GitWorkTree.View
{
    public partial class ManageWorktreesControl : UserControl
    {
        public ManageWorktreesControl()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var vm = DataContext as ManageWorktreesViewModel;
            if (vm == null) return;

            if (e.NewValue is HierarchyNode node && node.WorktreeItem != null)
            {
                vm.SelectedWorktree = node.WorktreeItem;
            }
            else
            {
                vm.SelectedWorktree = null;
            }
        }

        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void ChangesTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var treeView = sender as TreeView;
            if (treeView == null) return;

            var selectedNode = treeView.SelectedItem as GitChangeNode;
            if (selectedNode == null || selectedNode.IsFolder) return;

            string fullPath = selectedNode.FullPath;
            string fileRelativePath = selectedNode.RelativePath;
            string statusPart = selectedNode.Status;

            var vm = DataContext as ManageWorktreesViewModel;
            if (vm == null || vm.SelectedWorktree == null) return;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (statusPart == "??" || statusPart == "?")
                {
                    // Untracked: Open normally in the editor
                    await VS.Documents.OpenAsync(fullPath);
                }
                else
                {
                    // Modified / Staged: Open comparison (HEAD content vs working file) using IVsDifferenceService
                    var diffService = Package.GetGlobalService(typeof(SVsDifferenceService)) as IVsDifferenceService;
                    if (diffService != null)
                    {
                        // Write HEAD file content to a temp file
                        string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(fullPath) + "_HEAD");
                        
                        var gitService = new GitHelper(LoggingHelper.Instance);
                        string headContent = await gitService.ShowFileContentAsync(vm.SelectedWorktree.FullPath, $"HEAD:{fileRelativePath}");
                        if (headContent != null)
                        {
                            File.WriteAllText(tempFile, headContent);
                            diffService.OpenComparisonWindow(tempFile, fullPath);
                        }
                        else
                        {
                            // Fallback open normally if show failed
                            await VS.Documents.OpenAsync(fullPath);
                        }
                    }
                }
            });
        }

        private void OutgoingListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ManageWorktreesViewModel vm && OutgoingListBox.SelectedItem is GitCommitInfo commit)
            {
                vm.OpenCommitDetailsCommand.Execute(commit);
            }
        }

        private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                if (!item.IsSelected)
                {
                    item.IsSelected = true;
                    item.Focus();
                }
            }
        }

        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                if (!item.IsSelected)
                {
                    item.IsSelected = true;
                    item.Focus();
                }
            }
        }

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item == null) return;

            var node = item.DataContext as HierarchyNode;
            if (node == null || node.IsFolder || node.WorktreeItem == null) return;

            var vm = DataContext as ManageWorktreesViewModel;
            if (vm != null && vm.OpenCommand != null && vm.OpenCommand.CanExecute(vm.PreferredOpenAction))
            {
                vm.OpenCommand.Execute(vm.PreferredOpenAction);
                e.Handled = true;
            }
        }

        private void TreeView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var treeView = sender as TreeView;
            if (treeView == null) return;

            var node = treeView.SelectedItem as HierarchyNode;
            if (node == null || node.IsFolder || node.WorktreeItem == null) return;

            var vm = DataContext as ManageWorktreesViewModel;
            if (vm == null) return;

            if (e.Key == Key.Enter)
            {
                if (vm.OpenCommand != null && vm.OpenCommand.CanExecute(vm.PreferredOpenAction))
                {
                    vm.OpenCommand.Execute(vm.PreferredOpenAction);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Delete)
            {
                if (vm.RemoveCommand != null && vm.RemoveCommand.CanExecute(null))
                {
                    vm.RemoveCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }

    internal static class GuidList
    {
        public static readonly Guid guidTeamExplorerCmdSet = new Guid("11138d99-fa2f-4b0d-b0e6-813b5bf9aa1d");
    }

    internal static class PkgCmdIDList
    {
        public const uint cmdidCommitDetails = 0x1005; // ID of Git Commit details command in VS
    }
}
