using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ElasticKilla.Core.Collections;
using Moq;
using Xunit;

namespace ElasticKilla.Tests.CollectionsTests
{
    public class BackgroundTaskQueueTests
    {    
        public interface ICat
        {
            void Purrr(int times);
        }

        [Fact]
        public async Task Queue_AddTasks_MustRunInQueue()
        {
            var key = "kitty";
            var queue = new BackgroundTaskQueue<string>();
            var cat = new Mock<ICat>(MockBehavior.Strict);

            var sequence = new MockSequence();
            cat.InSequence(sequence).Setup(x => x.Purrr(2));
            cat.InSequence(sequence).Setup(x => x.Purrr(1));
            cat.InSequence(sequence).Setup(x => x.Purrr(3));

            var task1 = queue.QueueTask(key, async token => { await Task.Delay(1000, token); cat.Object.Purrr(2); });
            var task2 = queue.QueueTask(key, async token => { await Task.Delay(100, token); cat.Object.Purrr(1); });
            var task3 = queue.QueueTask(key, async token => { await Task.Delay(10, token); cat.Object.Purrr(3); });

            await Task.WhenAll(task1, task2, task3);

            cat.Verify(x => x.Purrr(1), Times.Once);
            cat.Verify(x => x.Purrr(2), Times.Once);
            cat.Verify(x => x.Purrr(3), Times.Once);
        }
        
        [Fact]
        public async Task Queue_AddWithCancellation_CancelTask()
        {
            var key = "kitty";
            var cancel = "cancel";

            var queue = new BackgroundTaskQueue<string>();
            var cat = new Mock<ICat>();

            cat.Setup(x => x.Purrr(1));
            cat.Setup(x => x.Purrr(2));
            cat.Setup(x => x.Purrr(3));

            var task1 = queue.QueueTask(key, async token => { await Task.Delay(1000, token); cat.Object.Purrr(1); }, cancel);
            var task2 = queue.QueueTask(key, async token => { await Task.Delay(1000000, token); cat.Object.Purrr(2); }, cancel);
            var task3 = queue.QueueTask(key, async token => { await Task.Delay(1000000, token); cat.Object.Purrr(3); }, cancel);

            await Task.Run(async () =>
            {
                await Task.Delay(100);
                queue.CancelTasks(cancel);
            });

            await Assert.ThrowsAsync<TaskCanceledException>(async () => await Task.WhenAll(task1, task2, task3));

            Assert.True(task1.IsCompleted);
            Assert.All(new [] {task2, task3}, task => Assert.True(task.IsCanceled));

            cat.Verify(x => x.Purrr(1), Times.Once);
            cat.Verify(x => x.Purrr(2), Times.Never);
            cat.Verify(x => x.Purrr(3), Times.Never);
        }

        [Fact]
        public async Task Queue_AddMultipleWithCancellation_CancelPart_RestShouldRanToCompletion()
        {
            var key = "kitty";
            var cancel = "cancel";

            var queue = new BackgroundTaskQueue<string>();
            var cat = new Mock<ICat>();

            cat.Setup(x => x.Purrr(1));
            cat.Setup(x => x.Purrr(2));
            cat.Setup(x => x.Purrr(3));

            var task1 = queue.QueueTask(key, async token => { await Task.Delay(1000, token); cat.Object.Purrr(1); }, cancel);
            var task2 = queue.QueueTask(key, async token => { await Task.Delay(10000, token); cat.Object.Purrr(2); }, cancel);
            var task3 = queue.QueueTask(key, async token => { await Task.Delay(100000, token); cat.Object.Purrr(3); }, cancel);
            var task4 = queue.QueueTask(key, async token => { await Task.Delay(100, token); cat.Object.Purrr(4); });

            await Task.Run(async () =>
            {
                await Task.Delay(100);
                queue.CancelTasks(cancel);
            });

            await Assert.ThrowsAsync<TaskCanceledException>(async () => await Task.WhenAll(task1, task2, task3, task4));

            Assert.All(new [] {task1, task4}, task => Assert.True(task.IsCompleted));
            Assert.All(new [] {task2, task3}, task => Assert.True(task.IsCanceled));

            cat.Verify(x => x.Purrr(1), Times.Once);
            cat.Verify(x => x.Purrr(2), Times.Never);
            cat.Verify(x => x.Purrr(3), Times.Never);
            cat.Verify(x => x.Purrr(4), Times.Once);
        }
        
        [Fact]
        public async Task Queue_AddWithDifferentKeys_MustRunInQueue()
        {
            var key1 = "kitty1";
            var key2 = "kitty2";

            var queue = new BackgroundTaskQueue<string>();
            var cat = new Mock<ICat>(MockBehavior.Strict);

            var sequence1 = new MockSequence();
            cat.InSequence(sequence1).Setup(x => x.Purrr(1));
            cat.InSequence(sequence1).Setup(x => x.Purrr(3));

            var sequence2 = new MockSequence();
            cat.InSequence(sequence2).Setup(x => x.Purrr(2));
            cat.InSequence(sequence2).Setup(x => x.Purrr(4));

            var task1 = queue.QueueTask(key1, async token => { await Task.Delay(1000, token); cat.Object.Purrr(1); });
            var task2 = queue.QueueTask(key2, async token => { await Task.Delay(1000, token); cat.Object.Purrr(2); });
            var task3 = queue.QueueTask(key1, async token => { await Task.Delay(100, token); cat.Object.Purrr(3); });
            var task4 = queue.QueueTask(key2, async token => { await Task.Delay(100, token); cat.Object.Purrr(4); });

            var tasks = new[] {task1, task2, task3, task4};
            await Task.WhenAll(tasks);

            Assert.All(tasks, task => Assert.True(task.IsCompleted));

            cat.Verify(x => x.Purrr(1), Times.Once);
            cat.Verify(x => x.Purrr(2), Times.Once);
            cat.Verify(x => x.Purrr(3), Times.Once);
            cat.Verify(x => x.Purrr(4), Times.Once);
        }
        
        [Fact]
        public async Task Queue_AddWitDifferentKeysWithCancellation_CancelPart_RestShouldRanToCompletion()
        {
            var key1 = "kitty1";
            var key2 = "kitty2";
            var cancel = "cancel";

            var queue = new BackgroundTaskQueue<string>();
            var cat = new Mock<ICat>();

            var task1 = queue.QueueTask(key1, async token =>
            {
                Debug.WriteLine($"Delay before purrring 1 time. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(5000, token);
                
                Debug.WriteLine($"Purrring 1 time. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                cat.Object.Purrr(1);
            }, cancel);
            
            var task2 = queue.QueueTask(key1, async token =>
            {
                Debug.WriteLine($"Delay before purrring 2 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(1000000, token);
                
                Debug.WriteLine($"Purrring 2 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                cat.Object.Purrr(2);
            }, cancel);
            
            var task3 = queue.QueueTask(key1, async token =>
            {
                Debug.WriteLine($"Delay before purrring 3 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(10, token);
                
                Debug.WriteLine($"Purrring 3 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                cat.Object.Purrr(3);
            });
            
            var task4 = queue.QueueTask(key2, async token =>
            {
                Debug.WriteLine($"Delay before purrring 4 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(5000, token);
                
                Debug.WriteLine($"Purrring 4 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                cat.Object.Purrr(4);
            }, cancel);
            
            var task5 = queue.QueueTask(key2, async token =>
            {
                Debug.WriteLine($"Delay before purrring 5 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(1000000, token);
                
                Debug.WriteLine($"Purrring 5 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                cat.Object.Purrr(5);
            }, cancel);
            
            var task6 = queue.QueueTask(key2, async token =>
            {
                Debug.WriteLine($"Delay before purrring 6 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(10, token);
                
                Debug.WriteLine($"Purrring 6 times. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                cat.Object.Purrr(6);
            });

            await Task.Run(async () =>
            {
                Debug.WriteLine($"Delay before cancel. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(10);
                
                Debug.WriteLine($"Canceling. ThreadId = {Thread.CurrentThread.ManagedThreadId}");
                queue.CancelTasks(cancel);
            });

            await Assert.ThrowsAsync<TaskCanceledException>(async () => await Task.WhenAll(task1, task2, task3, task4, task5, task6));

            Assert.All(new [] {task1, task3, task4, task6}, task => Assert.True(task.IsCompleted));
            Assert.All(new [] {task2, task5}, task => Assert.True(task.IsCanceled));

            cat.Verify(x => x.Purrr(1), Times.Never);
            cat.Verify(x => x.Purrr(2), Times.Never);
            cat.Verify(x => x.Purrr(3), Times.Once);
            cat.Verify(x => x.Purrr(4), Times.Never);
            cat.Verify(x => x.Purrr(5), Times.Never);
            cat.Verify(x => x.Purrr(6), Times.Once);
        }
        
        [Fact]
        public async Task Queue_AddTasks_Pause_WaitUntilAllCompleted()
        {
            var key = "kitty";
            var queue = new BackgroundTaskQueue<string>();
            var cat = new Mock<ICat>(MockBehavior.Strict);

            var sequence = new MockSequence();
            cat.InSequence(sequence).Setup(x => x.Purrr(1));
            cat.InSequence(sequence).Setup(x => x.Purrr(2));
            cat.InSequence(sequence).Setup(x => x.Purrr(3));

            var task1 = queue.QueueTask(key, async token => { await Task.Delay(1000, token); cat.Object.Purrr(1); });
            var task2 = queue.QueueTask(key, async token => { await Task.Delay(1000, token); cat.Object.Purrr(2); });
            var task3 = queue.QueueTask(key, async token => { await Task.Delay(1000, token); cat.Object.Purrr(3); });

            using (queue.Pause())
            {
                Assert.All(new [] {task1, task2, task3}, task => Assert.True(task.IsCompleted));
            }

            await Task.WhenAll(task1, task2, task3);

            cat.Verify(x => x.Purrr(1), Times.Once);
            cat.Verify(x => x.Purrr(2), Times.Once);
            cat.Verify(x => x.Purrr(3), Times.Once);
        }
        
        [Fact]
        public async Task Queue_AddTasks_IsEmpty_MustReturnFalse_WaitUntilAllCompleted_MustReturnTrue()
        {
            var key = "kitty";
            var queue = new BackgroundTaskQueue<string>();
            var cat = new Mock<ICat>(MockBehavior.Strict);

            var sequence = new MockSequence();
            cat.InSequence(sequence).Setup(x => x.Purrr(1));
            cat.InSequence(sequence).Setup(x => x.Purrr(2));
            cat.InSequence(sequence).Setup(x => x.Purrr(3));

            var task1 = queue.QueueTask(key, async token => { await Task.Delay(1000, token); cat.Object.Purrr(1); });
            var task2 = queue.QueueTask(key, async token => { await Task.Delay(1000, token); cat.Object.Purrr(2); });
            var task3 = queue.QueueTask(key, async token => { await Task.Delay(1000, token); cat.Object.Purrr(3); });

            Assert.False(queue.IsEmpty);

            await Task.WhenAll(task1, task2, task3);

            Assert.True(queue.IsEmpty);
            
            cat.Verify(x => x.Purrr(1), Times.Once);
            cat.Verify(x => x.Purrr(2), Times.Once);
            cat.Verify(x => x.Purrr(3), Times.Once);
        }
    }
}