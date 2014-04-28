using System;
using Manhood;

namespace ManConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Manhood Debug Console";
            
            var engine = new ManEngine("content\\content.man");
            var rand = new ManRandom();
            engine.Errors += (sender, eventArgs) =>
            {
                Console.WriteLine("Errors ({0}):", eventArgs.Errors.Count);
                foreach (var error in eventArgs.Errors)
                {
                    Console.WriteLine(eventArgs.Errors.GetVisualError(error));
                }
            };
            string cmd;
            while((cmd = Prompt()) != "quit")
            {
                PrintOutputGroup(engine, rand, cmd);
            }
        }

        static string Prompt()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("> ");
            var input = Console.ReadLine();
            Console.ResetColor();
            return input;
        }

        static void PrintOutputGroup(ManEngine engine, ManRandom rand, string pattern)
        {
            try
            {
                var output = engine.GenerateOutputGroup(rand, pattern);
                Console.ForegroundColor = ConsoleColor.Cyan;
                foreach (var group in output)
                {
                    Console.WriteLine("{0}: {1}", group.Key, group.Value);
                }
            }
            catch(Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: {0}", ex.Message);
            }
            Console.ResetColor();
        }
    }
}
