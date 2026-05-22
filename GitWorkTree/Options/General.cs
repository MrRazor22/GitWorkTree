using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GitWorkTree
{
    public partial class OptionsProvider
    {
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General>
        {
            public bool IsLoadSolution { get; set; }
            public string DefaultWorktreeDirectory { get; set; }
            public string WorktreeSubFolder { get; set; }
        }
    }

    public class General : BaseOptionModel<General>
    {
        [DisplayName("Load Solution")]
        [Description("Automatically load the worktree solution in Visual Studio after creation.")]
        [DefaultValue(true)]
        public bool IsLoadSolution { get; set; }


        [DisplayName("Default Worktree Directory: ")]
        [Description("Absolute path where new worktrees will be created. If empty (and Worktree Sub-Folder is also empty), worktrees default to a sibling \"<repo>_Worktrees\" folder.")]
        [DefaultValue("")]
        public string DefaultWorktreeDirectory { get; set; }

        [DisplayName("Worktree Sub-Folder: ")]
        [Description("Folder name created inside each repository to hold its worktrees (e.g. \".worktrees\"). When set, takes precedence over Default Worktree Directory. Tip: add this folder to .gitignore.")]
        [DefaultValue("")]
        public string WorktreeSubFolder { get; set; }
    }
}
