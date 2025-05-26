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
            public string DefaultBranchPath { get; set; }
        }
    }

    public class General : BaseOptionModel<General>
    {
        [DisplayName("Load Solution: ")]
        [Description("Load Worktree solution in Visual Studio immediately after worktree creation")]
        [DefaultValue("True")]
        public bool IsLoadSolution { get; set; }

        [DisplayName("Default Branch Path: ")]
        [Description("Default Branch Path to load newly created Worktrees")]
        [DefaultValue("")]
        public string DefaultBranchPath { get; set; }
    }
}
