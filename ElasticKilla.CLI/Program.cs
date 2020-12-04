using System;
using System.Collections.Generic;
using ElasticKilla.Core.Analyzers;
using ElasticKilla.Core.Tokenizer;

namespace ElasticKilla.CLI
{
    internal class Inputs
    {
        public const string Exit = "exit";

        public const string Query = "q";

        public const string DelayedQuery = "qw";

        public const string Subscribe = "sub";

        public const string Unsubscribe = "unsub";

        public const string Progress = "index?";

        public const string Subscriptions = "sub?";
    }

    class Program
    {
        private const string Promt = "> ";

        private static FileAnalyzer _analyzer;

        private static bool InProgress() => _analyzer.IsIndexing;

        private static IEnumerable<string> Subscriptions() => _analyzer.Subscriptions;

        private static IEnumerable<string> Search(string query) => _analyzer.Search(query);

        private static IEnumerable<string> DelayedSearch(string query) => _analyzer.DelayedSearch(query).Result;

        private static void Subscribe(string path, string pattern) => _analyzer.Subscribe(path, pattern);

        private static void Unsubscribe(string path) => _analyzer.Unsubscribe(path);

        private static bool ProcessInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return true;

            var cmd = input.Split(" ");
            var command = cmd[0];
            switch (command)
            {
                case Inputs.Query:
                case Inputs.DelayedQuery:
                {
                    if (cmd.Length < 2)
                        break;

                    var query = cmd[1];
                    var files = command.Equals(Inputs.Query) ? Search(query) : DelayedSearch(query);

                    Console.WriteLine($"Files, that contain \"{query}\":");
                    foreach (var file in files)
                        Console.WriteLine(file);

                    break;
                }
                case Inputs.Subscribe:
                {
                    if (cmd.Length < 2)
                        break;

                    var path = cmd[1];
                    var pattern = cmd.Length > 2 ? cmd[2] : string.Empty;

                    Console.WriteLine($"Subscribing to {path}");
                    Subscribe(path, pattern);

                    break;
                }
                case Inputs.Unsubscribe:
                {
                    if (cmd.Length < 2)
                        break;

                    var path = cmd[1];
                    Console.WriteLine($"Unsubscribing from {path}");
                    Unsubscribe(path);

                    break;
                }
                case Inputs.Progress:
                {
                    var inProgress = InProgress() ? "indexing" : "not indexing";
                    Console.WriteLine($"Analyzer is {inProgress} right now");

                    break;
                }
                case Inputs.Subscriptions:
                {
                    var subscriptions = Subscriptions();

                    Console.WriteLine($"Analyzer subscribed to:");
                    foreach (var subscription in subscriptions)
                        Console.WriteLine(subscription);

                    break;
                }
                case Inputs.Exit:
                    return false;
            }

            return true;
        }

        public static void Main(string[] args)
        {
            _analyzer = new FileAnalyzer(new WhitespaceTokenizer());

            bool next;
            do
            {
                Console.Write(Promt);
                var input = Console.ReadLine();
                next = ProcessInput(input);
            } while (next);
        }
    }
}