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
        }
    }

    public class General : BaseOptionModel<General>
    {
        [DisplayName("Load Solution")]
        [Description("Automatically load the worktree solution in Visual Studio after creation.")]
        [DefaultValue(true)]
        public bool IsLoadSolution { get; set; }


        [DisplayName("Default Worktree Directory: ")]
        [Description("Path where new worktrees will be created. If empty, the current repository directory will be used.")]
        [DefaultValue("")]
        public string DefaultWorktreeDirectory { get; set; }
    }
}
