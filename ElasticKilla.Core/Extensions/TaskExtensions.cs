using System.Threading.Tasks;

namespace ElasticKilla.Core.Extensions
{
    public static class TaskExtensions
    {
        public static Task IgnoreExceptions(this Task task) => task.ContinueWith(t => t);
    }
}