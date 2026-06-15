using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using GitWorkTree.Services;

namespace GitWorkTree.Tests
{
    [TestClass]
    public class GitHelperTests
    {
        private Mock<ILoggingService> _mockLoggingService;
        private Mock<IGitCommandExecutor> _mockCommandExecutor;
        private GitHelper _gitHelper;
        private const string RepoPath = @"C:\MockRepo";

        [TestInitialize]
        public void SetUp()
        {
            _mockLoggingService = new Mock<ILoggingService>();
            _mockCommandExecutor = new Mock<IGitCommandExecutor>();

            _mockCommandExecutor
                .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>()))
                .ReturnsAsync(true);

            _mockCommandExecutor
                .Setup(e => e.ExecuteWithResultAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>()))
                .ReturnsAsync(new GitCommandExecutionResult(true));

            _gitHelper = new GitHelper(_mockLoggingService.Object, _mockCommandExecutor.Object, @"C:\git.exe");
        }

        [TestMethod]
        public async Task GetWorkTreePathsAsync_PrependsLongPathsOption()
        {
            // Act
            await _gitHelper.GetWorkTreePathsAsync(RepoPath);

            // Assert
            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                @"C:\git.exe",
                It.Is<string>(args => args.StartsWith("-c core.longpaths=true worktree list")),
                RepoPath,
                It.IsAny<Action<string>>()), Times.Once);
        }

        [TestMethod]
        public async Task GetBranchesAsync_PrependsLongPathsOption()
        {
            // Act
            await _gitHelper.GetBranchesAsync(RepoPath);

            // Assert
            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                @"C:\git.exe",
                It.Is<string>(args => args.StartsWith("-c core.longpaths=true --no-pager branch")),
                RepoPath,
                It.IsAny<Action<string>>()), Times.Once);
        }

        [TestMethod]
        public async Task CreateBranchAsync_PrependsLongPathsOption()
        {
            // Act
            await _gitHelper.CreateBranchAsync(RepoPath, "new-branch", "main");

            // Assert
            _mockCommandExecutor.Verify(e => e.ExecuteWithResultAsync(
                @"C:\git.exe",
                It.Is<string>(args => args.StartsWith("-c core.longpaths=true branch new-branch")),
                RepoPath,
                It.IsAny<Action<string>>()), Times.Once);
        }

        [TestMethod]
        public async Task DeleteBranchAsync_PrependsLongPathsOption()
        {
            // Act
            await _gitHelper.DeleteBranchAsync(RepoPath, "old-branch");

            // Assert
            _mockCommandExecutor.Verify(e => e.ExecuteWithResultAsync(
                @"C:\git.exe",
                It.Is<string>(args => args.StartsWith("-c core.longpaths=true branch -D old-branch")),
                RepoPath,
                It.IsAny<Action<string>>()), Times.Once);
        }

        [TestMethod]
        public async Task CreateWorkTreeAsync_PrependsLongPathsOption()
        {
            // Act
            await _gitHelper.CreateWorkTreeAsync(RepoPath, "feature-branch", @"C:\WT");

            // Assert
            _mockCommandExecutor.Verify(e => e.ExecuteWithResultAsync(
                @"C:\git.exe",
                It.Is<string>(args => args.StartsWith("-c core.longpaths=true worktree add")),
                RepoPath,
                It.IsAny<Action<string>>()), Times.Once);
        }

        [TestMethod]
        public async Task RemoveWorkTreeAsync_PrependsLongPathsOption()
        {
            // Act
            await _gitHelper.RemoveWorkTreeAsync(RepoPath, @"C:\WT", false);

            // Assert
            _mockCommandExecutor.Verify(e => e.ExecuteWithResultAsync(
                @"C:\git.exe",
                It.Is<string>(args => args.StartsWith("-c core.longpaths=true worktree remove")),
                RepoPath,
                It.IsAny<Action<string>>()), Times.Once);
        }

        [TestMethod]
        public async Task PruneAsync_PrependsLongPathsOption()
        {
            // Act
            await _gitHelper.PruneAsync(RepoPath);

            // Assert
            _mockCommandExecutor.Verify(e => e.ExecuteWithResultAsync(
                @"C:\git.exe",
                It.Is<string>(args => args.StartsWith("-c core.longpaths=true worktree prune")),
                RepoPath,
                It.IsAny<Action<string>>()), Times.Once);
        }

        [TestMethod]
        public async Task GetGitFolderDirectoryAsync_PrependsLongPathsOption()
        {
            // Act
            await _gitHelper.GetGitFolderDirectoryAsync(RepoPath);

            // Assert
            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                @"C:\git.exe",
                It.Is<string>(args => args.StartsWith("-c core.longpaths=true rev-parse")),
                RepoPath,
                It.IsAny<Action<string>>()), Times.Once);
        }
    }
}
