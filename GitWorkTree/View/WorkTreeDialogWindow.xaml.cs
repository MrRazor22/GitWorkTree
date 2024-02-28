using GitWorkTree.ViewModel;
using Microsoft.VisualStudio.PlatformUI;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace GitWorkTree.View
{
    public partial class WorkTreeDialogWindow : DialogWindow
    {
        private CancellationTokenSource filterCancellationTokenSource;

        public WorkTreeDialogWindow()
        {
            InitializeComponent();
            filterCancellationTokenSource = new CancellationTokenSource();
        }

        private async void BranchName_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var cmb = (ComboBox)sender;
            cmb.IsDropDownOpen = true;

            filterCancellationTokenSource.Cancel();
            filterCancellationTokenSource = new CancellationTokenSource();

            await Task.Delay(300); // Introduce a delay to reduce the frequency of filtering

            var branches = (DataContext as WorkTreeDialogViewModel).Branches_Worktrees;
            cmb.ItemsSource = await FilterBranchNamesAsync(cmb.Text, branches.ToList(), filterCancellationTokenSource.Token); ;


        }

        private async Task<List<string>> FilterBranchNamesAsync(string filter, List<string> Branches, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return Branches
                .Where(p => string.IsNullOrEmpty(filter) || p.ToLower().Contains(filter.ToLower())).ToList();
            }, cancellationToken);
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
