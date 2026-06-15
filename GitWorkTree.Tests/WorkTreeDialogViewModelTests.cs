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
                .ReturnsAsync(true);
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

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
                SelectedBranch = new BranchInfo("main", false),
                FolderPath = @"C:\worktrees\feature-1"
            };

            // Act
            await viewModel.CreateCommand.ExecuteAsync(null);

            // Assert
            _mockGitService.Verify(g => g.CreateBranchAsync(@"C:\repo", "feature-1", "main"), Times.Once);
            _mockGitService.Verify(g => g.CreateWorkTreeAsync(@"C:\repo", "feature-1", @"C:\worktrees\feature-1", false), Times.Once);
            _mockSolutionService.Verify(s => s.OpenSolution(@"C:\worktrees\feature-1", It.IsAny<bool>()), Times.Once);
        }

        [TestMethod]
        public async Task Create_NewBranchMode_WorkTreeFails_DeletesBranchAsRollback()
        {
            // Arrange
            _mockGitService.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(false);
            _mockGitService.Setup(g => g.DeleteBranchAsync(It.IsAny<string>(), "feature-1"))
                .ReturnsAsync(true);

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
                SelectedBranch = new BranchInfo("main", false),
                FolderPath = @"C:\worktrees\feature-1"
            };

            // Act
            await viewModel.CreateCommand.ExecuteAsync(null);

            // Assert
            _mockGitService.Verify(g => g.CreateBranchAsync(@"C:\repo", "feature-1", "main"), Times.Once);
            _mockGitService.Verify(g => g.CreateWorkTreeAsync(@"C:\repo", "feature-1", @"C:\worktrees\feature-1", false), Times.Once);
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
                .ReturnsAsync(true);
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

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
                SelectedBranch = new BranchInfo("main", false),
                FolderPath = @"C:\worktrees\feature-1"
            };

            await viewModel.CreateCommand.ExecuteAsync(OpenBehavior.DoNotOpen);

            _mockSolutionService.Verify(s => s.OpenSolution(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task CreateCommand_WithCurrentWindow_OpensSolutionInCurrentWindow()
        {
            _mockGitService.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

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
                SelectedBranch = new BranchInfo("main", false),
                FolderPath = @"C:\worktrees\feature-1"
            };

            await viewModel.CreateCommand.ExecuteAsync(OpenBehavior.CurrentWindow);

            _mockSolutionService.Verify(s => s.OpenSolution(@"C:\worktrees\feature-1", true), Times.Once);
        }

        [TestMethod]
        public async Task CreateCommand_UpdatesPreferredCreateAction()
        {
            _mockGitService.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitService.Setup(g => g.CreateWorkTreeAsync(It.IsAny<string>(), "feature-1", It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

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
                SelectedBranch = new BranchInfo("main", false),
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
            viewModel.SelectedBranch = new BranchInfo("feature/foo", false);
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
            viewModel.SelectedBranch = new BranchInfo("feature/foo", false);
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
            viewModel.SelectedBranch = new BranchInfo("remotes/origin/feature/bar", false);
            await Task.Delay(200);

            // Assert
            Assert.AreEqual(@"C:\worktrees\feature\bar", viewModel.FolderPath);
        }
    }
}
