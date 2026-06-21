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

        private void OpenWithVisualStudio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is GitChangeNode node && node.IsFile)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    await VS.Documents.OpenAsync(node.FullPath);
                });
            }
        }

        private void CompareWithHead_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is GitChangeNode node && node.IsFile)
            {
                string fullPath = node.FullPath;
                string fileRelativePath = node.RelativePath;
                string statusPart = node.Status;

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
        }

        private void OpenContainingFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is GitChangeNode node)
            {
                try
                {
                    if (node.IsFile)
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{node.FullPath}\"");
                    }
                    else
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{node.FullPath}\"");
                    }
                }
                catch (System.Exception ex)
                {
                    var vm = DataContext as ManageWorktreesViewModel;
                    vm?.RefreshCommand?.Execute(null); // Try refreshing tree or similar
                }
            }
        }

        private void CopyFullPath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is GitChangeNode node)
            {
                try
                {
                    System.Windows.Clipboard.SetText(node.FullPath);
                }
                catch { }
            }
        }

        private void CopyRelativePath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is GitChangeNode node)
            {
                try
                {
                    System.Windows.Clipboard.SetText(node.RelativePath);
                }
                catch { }
            }
        }

        private void ViewCommitDetails_Click(object sender, RoutedEventArgs e)
        {
            var commit = (sender is MenuItem menuItem ? menuItem.DataContext : null) as GitCommitInfo 
                          ?? OutgoingListBox.SelectedItem as GitCommitInfo;
            if (commit != null)
            {
                if (DataContext is ManageWorktreesViewModel vm && vm.OpenCommitDetailsCommand != null)
                {
                    vm.OpenCommitDetailsCommand.Execute(commit);
                }
            }
        }

        private void CopyCommitSha_Click(object sender, RoutedEventArgs e)
        {
            var commit = (sender is MenuItem menuItem ? menuItem.DataContext : null) as GitCommitInfo 
                          ?? OutgoingListBox.SelectedItem as GitCommitInfo;
            if (commit != null)
            {
                try
                {
                    System.Windows.Clipboard.SetText(commit.FullSha);
                }
                catch { }
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

        private bool _clearingSelection = false;

        private void ChangesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_clearingSelection) return;
            if (e.NewValue == null) return;

            _clearingSelection = true;
            try
            {
                var activeTreeView = sender as TreeView;
                var vm = DataContext as ManageWorktreesViewModel;
                if (vm != null)
                {
                    if (activeTreeView != StagedChangesTreeView)
                    {
                        ClearNodeSelection(vm.StagedChangesTree);
                    }
                    if (activeTreeView != UnstagedChangesTreeView)
                    {
                        ClearNodeSelection(vm.UnstagedChangesTree);
                    }
                    if (activeTreeView != UntrackedChangesTreeView)
                    {
                        ClearNodeSelection(vm.UntrackedChangesTree);
                    }
                }

                if (OutgoingListBox != null && OutgoingListBox.SelectedItem != null)
                {
                    OutgoingListBox.SelectedItem = null;
                }
            }
            finally
            {
                _clearingSelection = false;
            }
        }

        private void OutgoingListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_clearingSelection) return;
            if (OutgoingListBox.SelectedItem == null) return;

            _clearingSelection = true;
            try
            {
                var vm = DataContext as ManageWorktreesViewModel;
                if (vm != null)
                {
                    ClearNodeSelection(vm.StagedChangesTree);
                    ClearNodeSelection(vm.UnstagedChangesTree);
                    ClearNodeSelection(vm.UntrackedChangesTree);
                }
            }
            finally
            {
                _clearingSelection = false;
            }
        }

        private void ClearNodeSelection(System.Collections.IEnumerable nodes)
        {
            if (nodes == null) return;
            foreach (var item in nodes)
            {
                if (item is GitChangeNode node)
                {
                    node.IsSelected = false;
                    if (node.Children != null && node.Children.Count > 0)
                    {
                        ClearNodeSelection(node.Children);
                    }
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
