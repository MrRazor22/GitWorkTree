using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using UserControl = System.Windows.Controls.UserControl;

namespace GitWorkTree.View.UserControls
{
    /// <summary>
    /// Interaction logic for FolderEntry.xaml
    /// </summary>
    public partial class FolderEntry : UserControl
    {
        public static DependencyProperty FolderPathProperty = DependencyProperty.Register("FolderPath", typeof(string), typeof(FolderEntry),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static DependencyProperty DescriptionProperty = DependencyProperty.Register("Description", typeof(string), typeof(FolderEntry), new PropertyMetadata(null));

        public string FolderPath { get { return GetValue(FolderPathProperty) as string; } set { SetValue(FolderPathProperty, value); } }

        public string Description { get { return GetValue(DescriptionProperty) as string; } set { SetValue(DescriptionProperty, value); } }

        public FolderEntry() { InitializeComponent(); }

        private void BrowseFolder(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = Description;
                dlg.SelectedPath = FolderPath;
                dlg.ShowNewFolderButton = true;
                System.Windows.Forms.DialogResult result = dlg.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    FolderPath = dlg.SelectedPath;

                    // Manually update the binding source
                    BindingExpression be = GetBindingExpression(FolderPathProperty);
                    if (be != null)
                        be.UpdateSource();
                }
            }
        }
    }
}
