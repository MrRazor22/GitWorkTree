using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GitWorkTree
{
    public enum OpenBehavior
    {
        DoNotOpen,
        NewVSWindow,
        CurrentWindow,
        Explorer
    }

    public partial class OptionsProvider
    {
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General>
        {
            public OpenBehavior PreferredCreateAction { get; set; }
            public OpenBehavior PreferredOpenAction { get; set; }
            public string DefaultWorktreeDirectory { get; set; }
            public string WorktreeSubFolder { get; set; }
            public bool PreserveBranchHierarchy { get; set; }
        }
    }

    public class General : BaseOptionModel<General>
    {
        [Browsable(false)]
        [DefaultValue(OpenBehavior.NewVSWindow)]
        public OpenBehavior PreferredCreateAction { get; set; } = OpenBehavior.NewVSWindow;

        [Browsable(false)]
        [DefaultValue(OpenBehavior.NewVSWindow)]
        public OpenBehavior PreferredOpenAction { get; set; } = OpenBehavior.NewVSWindow;


        [DisplayName("Default Worktree Directory")]
        [Description("Absolute path where new worktrees will be created. If empty, defaults to a sibling \"<repo>_Worktrees\" folder. Ignored when Worktree Sub-Folder is specified.")]
        [DefaultValue("")]
        public string DefaultWorktreeDirectory { get; set; }

        [DisplayName("Worktree Sub-Folder")]
        [Description("Folder created inside the repository to hold worktrees (e.g. \".worktrees\"). Tip: add this folder to .gitignore.")]
        [DefaultValue("")]
        public string WorktreeSubFolder { get; set; }

        [DisplayName("Preserve Branch Hierarchy")]
        [Description("Preserve branch hierarchy in worktree paths.\n\nEnabled:\nfeature/foo → Worktrees\\feature\\foo\n\nDisabled:\nfeature/foo → Worktrees\\feature-foo")]
        [DefaultValue(true)]
        public bool PreserveBranchHierarchy { get; set; } = true;

        [Browsable(false)]
        [DefaultValue(false)]
        public bool IsNewBranchMode { get; set; } = false;
    }
}
