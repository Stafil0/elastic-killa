using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElasticKilla.Core.Indexes;
using Xunit;

namespace ElasticKilla.Tests.IndexesTests
{
    public class StringIndexEventsTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task OnAdd_AddEventInvoked_ReturnedAddedValue(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, string>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = Guid.NewGuid().ToString();

            var triggered = 0;
            index.Added += (q, values) =>
            {
                var input = inputs[q];
                Assert.Equal(new [] {input}, values);
                Interlocked.Increment(ref triggered);
            };

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Add(q, input)));

            await Task.WhenAll(tasks);
            Assert.Equal(tasksCount, triggered);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task OnAddMultiple_AddEventInvoked_ReturnedAddedValues(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new [] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};

            var triggered = 0;
            index.Added += (q, values) =>
            {
                var input = inputs[q];
                Assert.Equal(input, values);
                Interlocked.Increment(ref triggered);
            };

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Add(q, input)));

            await Task.WhenAll(tasks);
            Assert.Equal(tasksCount, triggered);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task OnAddEmpty_AddEventNotInvoked(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new string[0];

            var triggered = 0;
            index.Added += (q, values) =>
            {
                var input = inputs[q];
                Assert.Equal(input, values);
                Interlocked.Increment(ref triggered);
            };

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Add(q, input)));

            await Task.WhenAll(tasks);
            Assert.Equal(0, triggered);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task OnRemove_RemoveEventInvoked_ReturnedRemovedValue(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, string>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = Guid.NewGuid().ToString();

            foreach (var (q, input) in inputs)
                index.Add(q, input);
            
            var triggered = 0;
            index.Removed += (q, values) =>
            {
                var input = inputs[q];
                Assert.Equal(new [] {input}, values);
                Interlocked.Increment(ref triggered);
            };

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Remove(q, input)));

            await Task.WhenAll(tasks);
            Assert.Equal(tasksCount, triggered);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task OnRemoveMultiple_RemoveEventInvoked_ReturnedRemovedValues(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new [] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};
            
            foreach (var (q, input) in inputs)
                index.Add(q, input);
            
            var triggered = 0;
            index.Removed += (q, values) =>
            {
                var input = inputs[q];
                Assert.Equal(input, values);
                Interlocked.Increment(ref triggered);
            };

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Remove(q, input)));

            await Task.WhenAll(tasks);
            Assert.Equal(tasksCount, triggered);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task OnRemoveEmpty_RemoveEventNotInvoked(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new string[0];
            
            foreach (var (q, input) in inputs)
                index.Add(q, input);
            
            var triggered = 0;
            index.Removed += (q, values) =>
            {
                var input = inputs[q];
                Assert.Equal(input, values);
                Interlocked.Increment(ref triggered);
            };

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Remove(q, input)));

            await Task.WhenAll(tasks);
            Assert.Equal(0, triggered);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task OnRemoveAll_RemoveEventInvoked_ReturnedRemovedValues(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new [] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};

            foreach (var (q, input) in inputs)
                index.Add(q, input);

            var triggered = 0;
            index.Removed += (q, values) =>
            {
                var input = inputs[q];
                Assert.All(input, x => Assert.Contains(x, values));
                Interlocked.Increment(ref triggered);
            };

            var tasks = new List<Task>();
            foreach (var (q, _) in inputs)
                tasks.Add(Task.Run(() => index.RemoveAll(q, out _)));

            await Task.WhenAll(tasks);
            Assert.Equal(tasksCount, triggered);
        }
    }

    public class StringIndexTests
    {
        [Fact]
        public void AddNull_DoNothing()
        {
            var index = new StringIndex<string>();
            index.Add(null, new string[0]);
            
            var result = index.Get(null);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task AddUnique_GetByQuery_EqualsWithInput(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new [] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
            foreach (var inp in input)
            {
                tasks.Add(Task.Run(() => index.Add(q, inp)));
            }

            await Task.WhenAll(tasks);
            
            var getTasks = inputs.Keys.Select(q => Task.Run(() => new {Query = q, Result = index.Get(q)})).ToList();

            await Task.WhenAll(getTasks);

            foreach (var task in getTasks)
            {
                var (query, result) = (task.Result.Query, task.Result.Result);
                var input = inputs[query].ToList();
                Assert.All(input, x => Assert.Contains(x, result));
                Assert.Equal(input.Count, result.Count);
            }
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task AddNonUnique_GetByQuery_ContainsOnlyUnique(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
            {
                var nonUnique = Guid.NewGuid().ToString();
                inputs[Guid.NewGuid().ToString()] = new[] {nonUnique, nonUnique, nonUnique};
            }

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
            foreach (var inp in input)
            {
                tasks.Add(Task.Run(() => index.Add(q, inp)));
            }

            await Task.WhenAll(tasks);
            
            var getTasks = inputs.Keys.Select(q => Task.Run(() => new {Query = q, Result = index.Get(q)})).ToList();

            await Task.WhenAll(getTasks);

            foreach (var task in getTasks)
            {
                var (query, result) = (task.Result.Query, task.Result.Result);
                var input = inputs[query].ToList();
                Assert.All(input, x => Assert.Contains(x, result));
                Assert.Equal(1, result.Count);
                Assert.NotEqual(input.Count, result.Count);
                Assert.Equal(new HashSet<string>(input), result);
            }
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task AddMultipleUnique_GetByQuery_EqualsWithInput(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new [] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Add(q, input)));

            await Task.WhenAll(tasks);
            
            var getTasks = inputs.Keys.Select(q => Task.Run(() => new {Query = q, Result = index.Get(q)})).ToList();

            await Task.WhenAll(getTasks);

            foreach (var task in getTasks)
            {
                var (query, result) = (task.Result.Query, task.Result.Result);
                var input = inputs[query];
                Assert.All(input, x => Assert.Contains(x, result));
            }
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task AddMultipleNonUnique_GetByQuery_ContainsOnlyUnique(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
            {
                var nonUnique = Guid.NewGuid().ToString();
                inputs[Guid.NewGuid().ToString()] = new[] {nonUnique, nonUnique, nonUnique};
            }

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Add(q, input)));

            await Task.WhenAll(tasks);
            
            var getTasks = inputs.Keys.Select(q => Task.Run(() => new {Query = q, Result = index.Get(q)})).ToList();

            await Task.WhenAll(getTasks);

            foreach (var task in getTasks)
            {
                var (query, result) = (task.Result.Query, task.Result.Result);
                var input = inputs[query].ToList();
                Assert.All(input, x => Assert.Contains(x, result));
                Assert.Equal(1, result.Count);
                Assert.NotEqual(input.Count, result.Count);
                Assert.Equal(new HashSet<string>(input), result);
            }
        }

        [Fact]
        public void RemoveNull_DoNothing()
        {
            var index = new StringIndex<string>();
            var result = index.Remove(null, new string[0]);
            Assert.False(result);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task RemoveSingle_GetByQuery_MustNotContainValue(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new [] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Add(q, input)));

            await Task.WhenAll(tasks);

            var removeTasks = new List<Task>();
            foreach (var (q, input) in inputs)
                removeTasks.Add(Task.Run(() => index.Remove(q, input.First())));

            await Task.WhenAll(removeTasks);

            var getTasks = inputs.Keys.Select(q => Task.Run(() => new {Query = q, Result = index.Get(q)})).ToList();

            await Task.WhenAll(getTasks);

            foreach (var task in getTasks)
            {
                var (query, result) = (task.Result.Query, task.Result.Result);
                var input = inputs[query].ToList();
                Assert.All(input.Skip(1), x => Assert.Contains(x, result));
                Assert.DoesNotContain(input.First(), result);
                Assert.Equal(input.Count - 1, result.Count);
            }
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task RemoveMultiple_GetByQuery_NotContainsRemoved(int tasksCount)
        {
            var skipTake = 3;
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new []
                {
                    Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()
                };

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Add(q, input)));

            await Task.WhenAll(tasks);

            var removeTasks = new List<Task>();
            foreach (var (q, input) in inputs)
                removeTasks.Add(Task.Run(() => index.Remove(q, input.Take(skipTake))));

            await Task.WhenAll(removeTasks);

            var getTasks = inputs.Keys.Select(q => Task.Run(() => new {Query = q, Result = index.Get(q)})).ToList();

            await Task.WhenAll(getTasks);

            foreach (var task in getTasks)
            {
                var (query, result) = (task.Result.Query, task.Result.Result);
                var input = inputs[query].ToList();
                Assert.All(input.Skip(skipTake), x => Assert.Contains(x, result));
                Assert.All(input.Take(skipTake), x => Assert.DoesNotContain(x, result));
                Assert.Equal(input.Count - skipTake, result.Count);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task RemoveAll_GetByQuery_ReturnsEmpty(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new []
                {
                    Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()
                };

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Add(q, input)));

            await Task.WhenAll(tasks);

            var removeTasks = inputs.Keys.Select(q => Task.Run(() => index.RemoveAll(q, out _))).ToList();

            await Task.WhenAll(removeTasks);

            var getTasks = inputs.Keys.Select(q => Task.Run(() => new {Query = q, Result = index.Get(q)})).ToList();

            await Task.WhenAll(getTasks);

            foreach (var task in getTasks)
            {
                var (query, result) = (task.Result.Query, task.Result.Result);
                var input = inputs[query].ToList();
                Assert.All(input, x => Assert.DoesNotContain(x, result));
                Assert.Equal(0, result.Count);
            }
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task Flush_GetByQuery_ReturnsEmpty(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new Dictionary<string, IEnumerable<string>>();
            for (var i = 0; i < tasksCount; i++)
                inputs[Guid.NewGuid().ToString()] = new []
                {
                    Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()
                };

            var tasks = new List<Task>();
            foreach (var (q, input) in inputs)
                tasks.Add(Task.Run(() => index.Add(q, input)));

            await Task.WhenAll(tasks);

            await Task.Run(() => index.Flush());

            var getTasks = inputs.Keys.Select(x => Task.Run(() => index.Get(x))).ToList();

            await Task.WhenAll(getTasks);
            
            foreach (var task in getTasks)
            {
                Assert.Empty(task.Result);
            }
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task RemoveFromEmpty_GetByQuery_MustBeEmpty(int tasksCount)
        {
            var index = new StringIndex<string>();
            var inputs = new List<string>();
            for (var i = 0; i < tasksCount; i++)
                inputs.Add(Guid.NewGuid().ToString());

            var removeTasks = inputs.Select(query => Task.Run(() => index.Remove(query, Guid.NewGuid().ToString()))).Cast<Task>().ToList();

            await Task.WhenAll(removeTasks);

            var getTasks = inputs.Select(q => Task.Run(() => index.Get(q))).ToList();

            await Task.WhenAll(getTasks);

            foreach (var task in getTasks)
            {
                var result = task.Result;
                Assert.Empty(result);
            }
        }
        
        [Fact]
        public void GetNull_ReturnNothing()
        {
            var index = new StringIndex<string>();
            var result = index.Get(null);
            Assert.Empty(result);
        }
    }
}