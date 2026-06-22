using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GitWorkTree.ViewModel;

namespace GitWorkTree.Tests
{
    [TestClass]
    public class AsyncRelayCommandTests
    {
        [TestMethod]
        public async Task ExecuteAsync_PreventsConcurrentExecution()
        {
            // Arrange
            int executionCount = 0;
            var tcs = new TaskCompletionSource<bool>();

            var command = new AsyncRelayCommand(async param =>
            {
                executionCount++;
                await tcs.Task; // Keep running until we release it
            });

            // Act
            Task execution1 = command.ExecuteAsync(null);
            Task execution2 = command.ExecuteAsync(null); // Should be ignored

            tcs.SetResult(true);
            await Task.WhenAll(execution1, execution2);

            // Assert
            Assert.AreEqual(1, executionCount, "Command should not execute concurrently.");
        }

        [TestMethod]
        public async Task CanExecute_ReturnsFalse_WhileExecuting()
        {
            // Arrange
            var tcs = new TaskCompletionSource<bool>();
            AsyncRelayCommand command = null;

            command = new AsyncRelayCommand(async param =>
            {
                // Assert while executing
                Assert.IsFalse(command.CanExecute(null), "CanExecute should return false while executing.");
                await tcs.Task;
            });

            // Act & Assert before executing
            Assert.IsTrue(command.CanExecute(null));

            Task execution = command.ExecuteAsync(null);

            tcs.SetResult(true);
            await execution;

            // Assert after executing
            Assert.IsTrue(command.CanExecute(null));
        }

        [TestMethod]
        public async Task ExecuteAsync_RaisesCanExecuteChanged_OnStartAndFinish()
        {
            // Arrange
            var tcs = new TaskCompletionSource<bool>();
            int eventCount = 0;

            var command = new AsyncRelayCommand(async param =>
            {
                await tcs.Task;
            });

            command.CanExecuteChanged += (s, e) =>
            {
                eventCount++;
            };

            // Act & Assert
            Task execution = command.ExecuteAsync(null);
            
            // At this point, execution has started, and CanExecuteChanged should have been raised
            tcs.SetResult(true);
            await execution;

            // Assert: should raise at least twice (start and end)
            // Note: Since CommandManager.RequerySuggested is used for the event under the hood in WPF, 
            // the count will depend on RequerySuggested behavior. We verify it doesn't throw and triggers properly.
            Assert.IsTrue(eventCount >= 2, $"CanExecuteChanged should be raised on start and finish. Actual raise count: {eventCount}");
        }

        [TestMethod]
        public async Task ExecuteAsync_PropagatesExceptions()
        {
            // Arrange
            var command = new AsyncRelayCommand(param =>
            {
                throw new InvalidOperationException("Test exception propagation");
            });

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await command.ExecuteAsync(null);
            });
        }
    }
}
