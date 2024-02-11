using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GitWorkTree
{
    public partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        //[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "GitWorkTree", "General", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General>
        {
            public bool IsLoadSolution { get; set; }
            public string DefaultBranchPath { get; set; }
        }
    }

    public class General : BaseOptionModel<General>
    {
        [DisplayName("Load Solution: ")]
        [Description("Load the solution of newly created Worktree branch directly into the current instance of Visual Studio immediately after creation")]
        [DefaultValue("False")]
        public bool IsLoadSolution { get; set; }

        [DisplayName("Default Branch Path: ")]
        [Description("Default Branch Path to load newly created Worktrees")]
        [DefaultValue("D:\\WorkTrees")]
        public string DefaultBranchPath { get; set; }
    }
}
