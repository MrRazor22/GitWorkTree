using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using GitWorkTree.Commands;
using GitWorkTree.Services;
using GitWorkTree.ViewModel;

namespace GitWorkTree.Tests
{
    [TestClass]
    public class WorkTreeDialogViewModelTests
    {
        private Mock<IGitService> _mockGitService;
        private Mock<ISolutionService> _mockSolutionService;
        private Mock<ILoggingService> _mockLoggingService;
        private General _options;

        [TestInitialize]
        public void SetUp()
        {
#pragma warning disable VSSDK005
            _ = new Microsoft.VisualStudio.Threading.JoinableTaskContext();
#pragma warning restore VSSDK005

            _mockGitService = new Mock<IGitService>();
            _mockSolutionService = new Mock<ISolutionService>();
            _mockLoggingService = new Mock<ILoggingService>();
            _options = new General
            {
                PreferredCreateAction = OpenBehavior.NewVSWindow,
                PreferredOpenAction = OpenBehavior.NewVSWindow,
                DefaultWorktreeDirectory = @"C:\worktrees"
            };

            _mockGitService.Setup(g => g.GetBranchesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string> { "main" });
            _mockGitService.Setup(g => g.GetTagsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string>());
            _mockGitService.Setup(g => g.GetWorkTreePathsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string>());
        }

        [TestMethod]
        public void Validate_EmptyActiveRepositoryPath_ReturnsError()
        {
            // Arrange
            var viewModel = new WorkTreeDialogViewModel(
                null,
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            // Act
            var error = viewModel["ActiveRepositoryPath"];

            // Assert
            Assert.AreEqual("No repository loaded", error);
        }

        [TestMethod]
        public void Validate_EmptySelectedBranch_ReturnsError()
        {
            // Arrange
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );
            viewModel.SelectedBranch = null;

            // Act
            var error = viewModel["SelectedBranch"];

            // Assert
            Assert.AreEqual("No valid branch/Worktree selected", error);
        }

        [TestMethod]
        public void Validate_NewBranchNameEmptyInNewBranchMode_ReturnsError()
        {
            // Arrange
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = ""
            };

            // Act
            var error = viewModel["NewBranchName"];

            // Assert
            Assert.AreEqual("Branch name cannot be empty", error);
        }

        [TestMethod]
        public void Validate_NewBranchNameWithSpaces_ReturnsError()
        {
            // Arrange
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "WTF bro"
            };

            // Act
            var error = viewModel["NewBranchName"];

            // Assert
            Assert.AreEqual("Branch name cannot contain spaces", error);
        }

        [TestMethod]
        public void Validate_NewBranchNameWithTrailingSlash_ReturnsError()
        {
            // Arrange
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "3434/"
            };

            // Act
            var error = viewModel["NewBranchName"];

            // Assert
            Assert.AreEqual("Branch name cannot start or end with a slash", error);
        }

        [TestMethod]
        public void Validate_NewBranchNameWithLeadingSlash_ReturnsError()
        {
            // Arrange
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "/foo"
            };

            // Act
            var error = viewModel["NewBranchName"];

            // Assert
            Assert.AreEqual("Branch name cannot start or end with a slash", error);
        }

        [TestMethod]
        public void Validate_NewBranchNameWithConsecutiveSlashes_ReturnsError()
        {
            // Arrange
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "feature//foo"
            };

            // Act
            var error = viewModel["NewBranchName"];

            // Assert
            Assert.AreEqual("Branch name cannot contain consecutive slashes", error);
        }

        [TestMethod]
        public void Validate_NewBranchNameWithInvalidCharacters_ReturnsError()
        {
            // Arrange
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "feature^one"
            };

            // Act
            var error = viewModel["NewBranchName"];

            // Assert
            Assert.AreEqual("Branch name contains invalid characters", error);
        }

        [TestMethod]
        public void Validate_FolderPathWithInvalidCharacters_ReturnsError()
        {
            // Arrange
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                FolderPath = @"C:\repo_Worktrees\invalid>path"
            };

            // Act
            var error = viewModel["FolderPath"];

            // Assert
            Assert.AreEqual("Please enter a valid path for worktree", error);
        }

        [TestMethod]
        public async Task Create_NewBranchMode_Success_CreatesBranchAndWorkTreeAndOpensSolution()
        {
            // Arrange
            _mockGitService.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(true));
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(true));

            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "feature-1",
                SelectedBranch = new BranchInfo("main", "main", false),
                FolderPath = @"C:\worktrees\feature-1"
            };

            // Act
            await viewModel.CreateCommand.ExecuteAsync(null);

            // Assert
            _mockGitService.Verify(g => g.CreateBranchAsync(@"C:\repo", "feature-1", "main"), Times.Once);
            _mockGitService.Verify(g => g.CreateWorkTreeAsync(@"C:\repo", "feature-1", @"C:\worktrees\feature-1"), Times.Once);
            _mockSolutionService.Verify(s => s.OpenSolution(@"C:\worktrees\feature-1", It.IsAny<bool>()), Times.Once);
        }

        [TestMethod]
        public async Task Create_NewBranchMode_WorkTreeFails_DeletesBranchAsRollback()
        {
            // Arrange
            _mockGitService.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(true));
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(false, "Failed to create worktree"));
            _mockGitService.Setup(g => g.DeleteBranchAsync(It.IsAny<string>(), "feature-1"))
                .ReturnsAsync(new GitOperationResult(true));

            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "feature-1",
                SelectedBranch = new BranchInfo("main", "main", false),
                FolderPath = @"C:\worktrees\feature-1"
            };

            // Act
            await viewModel.CreateCommand.ExecuteAsync(null);

            // Assert
            _mockGitService.Verify(g => g.CreateBranchAsync(@"C:\repo", "feature-1", "main"), Times.Once);
            _mockGitService.Verify(g => g.CreateWorkTreeAsync(@"C:\repo", "feature-1", @"C:\worktrees\feature-1"), Times.Once);
            _mockGitService.Verify(g => g.DeleteBranchAsync(@"C:\repo", "feature-1"), Times.Once);
            _mockSolutionService.Verify(s => s.OpenSolution(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public void Constructor_CreateCommandType_SetsIsNewBranchModeFalseByDefault()
        {
            // Arrange & Act
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            // Assert
            Assert.IsFalse(viewModel.IsNewBranchMode);
        }

        [TestMethod]
        public void Constructor_CreateCommandType_LoadsIsNewBranchModeFromOptions()
        {
            // Arrange
            _options.IsNewBranchMode = true;

            // Act
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            // Assert
            Assert.IsTrue(viewModel.IsNewBranchMode);
        }

        [TestMethod]
        public void Constructor_InitializesDefaultOpenActionFromSettings()
        {
            _options.PreferredCreateAction = OpenBehavior.DoNotOpen;
            _options.PreferredOpenAction = OpenBehavior.CurrentWindow;

            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            Assert.AreEqual(OpenBehavior.DoNotOpen, viewModel.PreferredCreateAction);
            Assert.AreEqual(OpenBehavior.CurrentWindow, viewModel.PreferredOpenAction);
        }

        [TestMethod]
        public async Task CreateCommand_WithDoNotOpen_DoesNotOpenSolution()
        {
            _mockGitService.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(true));
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(true));

            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "feature-1",
                SelectedBranch = new BranchInfo("main", "main", false),
                FolderPath = @"C:\worktrees\feature-1"
            };

            await viewModel.CreateCommand.ExecuteAsync(OpenBehavior.DoNotOpen);

            _mockSolutionService.Verify(s => s.OpenSolution(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task CreateCommand_WithCurrentWindow_OpensSolutionInCurrentWindow()
        {
            _mockGitService.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(true));
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(true));

            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "feature-1",
                SelectedBranch = new BranchInfo("main", "main", false),
                FolderPath = @"C:\worktrees\feature-1"
            };

            await viewModel.CreateCommand.ExecuteAsync(OpenBehavior.CurrentWindow);

            _mockSolutionService.Verify(s => s.OpenSolution(@"C:\worktrees\feature-1", true), Times.Once);
        }

        [TestMethod]
        public async Task CreateCommand_UpdatesPreferredCreateAction()
        {
            _mockGitService.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(true));
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(new GitOperationResult(true));

            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            )
            {
                IsNewBranchMode = true,
                NewBranchName = "feature-1",
                SelectedBranch = new BranchInfo("main", "main", false),
                FolderPath = @"C:\worktrees\feature-1"
            };

            await viewModel.CreateCommand.ExecuteAsync(OpenBehavior.CurrentWindow);

            Assert.AreEqual(OpenBehavior.CurrentWindow, viewModel.PreferredCreateAction);
            Assert.AreEqual(OpenBehavior.CurrentWindow, _options.PreferredCreateAction);
        }

        [TestMethod]
        public async Task UpdateFolderPath_PreserveBranchHierarchyTrue_CreatesSubdirectories()
        {
            // Arrange
            _options.PreserveBranchHierarchy = true;
            _options.DefaultWorktreeDirectory = @"C:\worktrees";
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            // Act
            viewModel.SelectedBranch = new BranchInfo("feature/foo", "feature/foo", false);
            await Task.Delay(200);

            // Assert
            Assert.AreEqual(@"C:\worktrees\feature\foo", viewModel.FolderPath);
        }

        [TestMethod]
        public async Task UpdateFolderPath_PreserveBranchHierarchyFalse_FlattensPath()
        {
            // Arrange
            _options.PreserveBranchHierarchy = false;
            _options.DefaultWorktreeDirectory = @"C:\worktrees";
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            // Act
            viewModel.SelectedBranch = new BranchInfo("feature/foo", "feature/foo", false);
            await Task.Delay(200);

            // Assert
            Assert.AreEqual(@"C:\worktrees\feature-foo", viewModel.FolderPath);
        }

        [TestMethod]
        public async Task UpdateFolderPath_RemoteBranchHierarchy_CleansRemotePrefix()
        {
            // Arrange
            _options.PreserveBranchHierarchy = true;
            _options.DefaultWorktreeDirectory = @"C:\worktrees";
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            // Act
            viewModel.SelectedBranch = new BranchInfo("feature/bar", "remotes/origin/feature/bar", false);
            await Task.Delay(200);

            // Assert
            Assert.AreEqual(@"C:\worktrees\feature\bar", viewModel.FolderPath);
        }

        [TestMethod]
        public async Task PopulateBranches_Worktrees_RemoteBranchAndCustomRemoteName_HasLinkedWorktreeAndValidationFailure()
        {
            // Arrange
            _mockGitService.Setup(g => g.GetBranchesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string> { "+ main", "remotes/origin/main", "remotes/custom-remote/main", "feature/other" });

            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            await Task.Delay(200);

            // Assert: Expecting 2 deduplicated branches ("main" and "feature/other") instead of 4
            Assert.AreEqual(2, viewModel.Branches_Worktrees.Count);
            
            // "main" has worktree (deduplicated local + remote references, preserves local "main" as Name & FullRef)
            Assert.AreEqual("main", viewModel.Branches_Worktrees[0].Name);
            Assert.AreEqual("main", viewModel.Branches_Worktrees[0].FullRef);
            Assert.IsTrue(viewModel.Branches_Worktrees[0].HasLinkedWorktree);

            // "feature/other" should not have worktree
            Assert.AreEqual("feature/other", viewModel.Branches_Worktrees[1].Name);
            Assert.AreEqual("feature/other", viewModel.Branches_Worktrees[1].FullRef);
            Assert.IsFalse(viewModel.Branches_Worktrees[1].HasLinkedWorktree);

            // By default, since IsExistingBranchMode is true and "main" has worktree, the SelectedBranch should be "feature/other"
            Assert.AreEqual("feature/other", viewModel.SelectedBranch.Name);

            // If we select a branch that has a worktree, we get a validation error
            viewModel.SelectedBranch = viewModel.Branches_Worktrees[0]; // main
            var error = viewModel["SelectedBranch"];
            Assert.AreEqual("This branch already has a worktree.", error);

            // If we switch to New Branch Mode, validation is skipped
            viewModel.IsNewBranchMode = true;
            viewModel.SelectedBranch = viewModel.Branches_Worktrees[0]; // main
            Assert.AreEqual("main", viewModel.SelectedBranch.Name);

            viewModel.IsNewBranchMode = false; // Switch back to Existing Branch Mode
            // It should detect "main" has a worktree, and auto-select the first one without: "feature/other"
            Assert.AreEqual("feature/other", viewModel.SelectedBranch.Name);
        }

        [TestMethod]
        public void PopulateBranches_Worktrees_IncludesTags()
        {
            // Arrange
            _mockGitService.Setup(g => g.GetBranchesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string> { "main", "feature/other" });
            _mockGitService.Setup(g => g.GetTagsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string> { "v1.0.0", "v2.0.0" });

            // Act
            var viewModel = new WorkTreeDialogViewModel(
                @"C:\repo",
                CommandType.Create,
                _options,
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            // Assert: Tags are NOT loaded by default (IsNewBranchMode is false)
            Assert.AreEqual(2, viewModel.Branches_Worktrees.Count);
            Assert.AreEqual("main", viewModel.Branches_Worktrees[0].Name);
            Assert.AreEqual("feature/other", viewModel.Branches_Worktrees[1].Name);

            // Act: Switch to New Branch Mode
            viewModel.IsNewBranchMode = true;

            // Assert: Now tags are loaded (2 branches + 2 tags = 4 items)
            Assert.AreEqual(4, viewModel.Branches_Worktrees.Count);

            // Verify branches
            Assert.AreEqual("main", viewModel.Branches_Worktrees[0].Name);
            Assert.IsFalse(viewModel.Branches_Worktrees[0].IsTag);

            Assert.AreEqual("feature/other", viewModel.Branches_Worktrees[1].Name);
            Assert.IsFalse(viewModel.Branches_Worktrees[1].IsTag);

            // Verify tags
            Assert.AreEqual("v1.0.0", viewModel.Branches_Worktrees[2].Name);
            Assert.AreEqual("refs/tags/v1.0.0", viewModel.Branches_Worktrees[2].FullRef);
            Assert.IsTrue(viewModel.Branches_Worktrees[2].IsTag);

            Assert.AreEqual("v2.0.0", viewModel.Branches_Worktrees[3].Name);
            Assert.AreEqual("refs/tags/v2.0.0", viewModel.Branches_Worktrees[3].FullRef);
            Assert.IsTrue(viewModel.Branches_Worktrees[3].IsTag);

            // Act: Switch back to Existing Branch Mode
            viewModel.IsNewBranchMode = false;

            // Assert: Tags are removed again
            Assert.AreEqual(2, viewModel.Branches_Worktrees.Count);
        }
    }
}
