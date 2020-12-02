using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElasticKilla.Core.Analyzers;
using ElasticKilla.Core.Tokenizer;

namespace ElasticKilla.CLI
{
    internal class Inputs
    {
        public const string Exit = "exit";
        
        public const string Query = "q";
        
        public const string Subscribe = "sub";
        
        public const string Unsubscribe = "unsub";
    }

    class Program
    {
        private const string Promt = "> ";

        private static FileAnalyzer _analyzer;

        private static IEnumerable<string> Search(string query) => _analyzer.Search(query);

        private static void Subscribe(string path, string pattern) => _analyzer.Subscribe(path, pattern);

        private static void Unsubscribe(string path) => _analyzer.Unsubscribe(path);

        private static bool ExecuteInput(string input)
        {
            if (input == null)
                return true;

            var cmd = input.Split(" ");
            switch (cmd[0])
            {
                case Inputs.Query:
                    var files = Search(cmd[1]);
                    foreach (var file in files)
                        Console.WriteLine(file);
                    break;
                case Inputs.Subscribe:
                    var path = cmd[1];
                    var pattern = cmd.Length > 2 ? cmd[2] : string.Empty;  
                    Subscribe(path, pattern);
                    break;
                case Inputs.Unsubscribe:
                    Unsubscribe(cmd[1]);
                    break;
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
                next = ExecuteInput(input);
            } while (next);
        }
    }
}