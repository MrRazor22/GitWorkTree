using GitWorkTree.ToolWindows.ViewModel;
using Microsoft.VisualStudio.PlatformUI;
using System.Linq;
using System.Windows.Controls;

namespace GitWorkTree.ToolWindows.View
{
    public partial class WorkTreeDialogWindow : DialogWindow
    {
        public WorkTreeDialogWindow()
        {
            InitializeComponent();
        }

        private void BranchName_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var cmb = (ComboBox)sender;
            cmb.IsDropDownOpen = true;
            //var textbox = cmb.Template.FindName("PART_EditableTextBox", cmb) as TextBox;
            cmb.ItemsSource = (DataContext as WorkTreeDialogViewModel).Branches.ToList()
                .Where(p => string.IsNullOrEmpty(cmb.Text) || p.ToLower().Contains(cmb.Text.ToLower())).ToList();
        }
    }
}
