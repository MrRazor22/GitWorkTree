using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using GitWorkTree.Services;
using GitWorkTree.ViewModel;

namespace GitWorkTree.Tests
{
    [TestClass]
    public class ManageWorktreesViewModelTests
    {
        private Mock<IGitService> _mockGitService;
        private Mock<ISolutionService> _mockSolutionService;
        private Mock<ILoggingService> _mockLoggingService;

        [TestInitialize]
        public void SetUp()
        {
            _mockGitService = new Mock<IGitService>();
            _mockSolutionService = new Mock<ISolutionService>();
            _mockLoggingService = new Mock<ILoggingService>();
        }

        [TestMethod]
        public void BuildChangesTree_WithUnstagedRename_ParsesCorrectlyAndDoesNotThrow()
        {
            // Arrange
            var viewModel = new ManageWorktreesViewModel(
                _mockGitService.Object,
                _mockSolutionService.Object,
                _mockLoggingService.Object
            );

            var mockChanges = new List<string>
            {
                " R old_file.cs -> new_file.cs",
                "R  staged_old.cs -> staged_new.cs"
            };

            // Act: Invoke BuildChangesTree via reflection to bypass VS-specific threading in LoadDetailsAsync
            var method = typeof(ManageWorktreesViewModel).GetMethod("BuildChangesTree", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "BuildChangesTree method should exist");
            method.Invoke(viewModel, new object[] { mockChanges });

            // Assert
            Assert.AreEqual(1, viewModel.UnstagedChangesTree.Count);
            Assert.AreEqual("new_file.cs", viewModel.UnstagedChangesTree.First().Name);

            Assert.AreEqual(1, viewModel.StagedChangesTree.Count);
            Assert.AreEqual("staged_new.cs", viewModel.StagedChangesTree.First().Name);
        }
    }
}
