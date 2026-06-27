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
                .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), default))
                .ReturnsAsync(new GitCommandExecutionResult(true, ""));

            _gitHelper = new GitHelper(_mockLoggingService.Object, _mockCommandExecutor.Object);
        }

        [TestMethod]
        public async Task GetWorkTreePathsAsync_ExecutesWorktreeList()
        {
            await _gitHelper.GetWorkTreePathsAsync(RepoPath);

            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                It.Is<string>(args => args.Contains("worktree list")),
                RepoPath,
                It.IsAny<Action<string>>(), default), Times.Once);
        }

        [TestMethod]
        public async Task GetBranchesAsync_ExecutesBranchCommand()
        {
            await _gitHelper.GetBranchesAsync(RepoPath);

            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                It.Is<string>(args => args.Contains("--no-pager branch")),
                RepoPath,
                It.IsAny<Action<string>>(), default), Times.Once);
        }

        [TestMethod]
        public async Task CreateBranchAsync_ExecutesBranchCreate()
        {
            await _gitHelper.CreateBranchAsync(RepoPath, "new-branch", "main");

            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                It.Is<string>(args => args.Contains("branch new-branch")),
                RepoPath,
                It.IsAny<Action<string>>(), default), Times.Once);
        }

        [TestMethod]
        public async Task DeleteBranchAsync_ExecutesBranchDelete()
        {
            await _gitHelper.DeleteBranchAsync(RepoPath, "old-branch");

            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                It.Is<string>(args => args.Contains("branch -D old-branch")),
                RepoPath,
                It.IsAny<Action<string>>(), default), Times.Once);
        }

        [TestMethod]
        public async Task CreateWorkTreeAsync_ExecutesWorktreeAdd()
        {
            await _gitHelper.CreateWorkTreeAsync(RepoPath, "feature-branch", @"C:\WT");

            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                It.Is<string>(args => args.Contains("worktree add")),
                RepoPath,
                It.IsAny<Action<string>>(), default), Times.Once);
        }

        [TestMethod]
        public async Task RemoveWorkTreeAsync_ExecutesWorktreeRemove()
        {
            await _gitHelper.RemoveWorkTreeAsync(RepoPath, @"C:\WT", false);

            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                It.Is<string>(args => args.Contains("worktree remove")),
                RepoPath,
                It.IsAny<Action<string>>(), default), Times.Once);
        }

        [TestMethod]
        public async Task PruneAsync_ExecutesWorktreePrune()
        {
            await _gitHelper.PruneAsync(RepoPath);

            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                It.Is<string>(args => args.Contains("worktree prune")),
                RepoPath,
                It.IsAny<Action<string>>(), default), Times.Once);
        }

        [TestMethod]
        public async Task GetGitFolderDirectoryAsync_ExecutesRevParse()
        {
            await _gitHelper.GetGitFolderDirectoryAsync(RepoPath);

            _mockCommandExecutor.Verify(e => e.ExecuteAsync(
                It.Is<string>(args => args.Contains("rev-parse")),
                RepoPath,
                It.IsAny<Action<string>>(), default), Times.Once);
        }
    }
}
