using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using GitWorkTree.Services;

namespace GitWorkTree.Tests
{
    [TestClass]
    public class GitPathResolverTests
    {
        [TestMethod]
        public void GetGitPath_ReturnsNonNullPath()
        {
            var resolver = new GitPathResolver();
            string gitPath = resolver.GetGitPath();

            Assert.IsFalse(string.IsNullOrWhiteSpace(gitPath), "Git path should not be null or whitespace.");
        }

        [TestMethod]
        public async Task GitCommandExecutor_UsesProvidedGitPathResolver()
        {
            var mockLoggingService = new Mock<ILoggingService>();
            var mockResolver = new Mock<IGitPathResolver>();
            const string expectedPath = @"C:\Custom\git.exe";

            mockResolver.Setup(r => r.GetGitPath()).Returns(expectedPath);

            var executor = new GitCommandExecutor(mockLoggingService.Object, mockResolver.Object);
            await executor.ExecuteAsync("status", @"C:\MockRepo");

            mockResolver.Verify(r => r.GetGitPath(), Times.AtLeastOnce);
        }
    }
}
