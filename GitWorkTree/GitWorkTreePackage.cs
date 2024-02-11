global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using GitWorkTree.Commands;
using System.Runtime.InteropServices;
using System.Threading;

namespace GitWorkTree
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Source Control", "Git WorkTree", 101, 102, true, new string[0], ProvidesLocalizedCategoryName = false, SupportsProfiles = true)]
    [ProvideProfile(typeof(OptionsProvider.GeneralOptions), "Source Control", "Git WorkTree", 101, 102, isToolsOptionPage: true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidGitWorkTreePackageString)]
    public sealed class GitWorkTreePackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
            await InteractWithSettingsAsync();
            General.Saved += CommandHelper.OnSettingsSaved;
        }
        private async Task InteractWithSettingsAsync()
        {
            // read settings
            var general = await General.GetLiveInstanceAsync();
            CommandHelper.optionsSaved = general;
        }
    }
}