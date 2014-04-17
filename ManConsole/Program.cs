using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Manhood;

namespace ManConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Manhood Debug Console";
            
            ManEngine engine = new ManEngine("content\\content.man");
            ManRandom rand = new ManRandom();
            string cmd = "";
            while((cmd = Prompt()) != "quit")
            {
                PrintOGC(engine, rand, cmd);
                Console.WriteLine(engine.ErrorLog.ToString());
            }
        }

        static string Prompt()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("> ");
            string input = Console.ReadLine();
            Console.ResetColor();
            return input;
        }

        static void PrintOGC(ManEngine engine, ManRandom rand, string pattern)
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
