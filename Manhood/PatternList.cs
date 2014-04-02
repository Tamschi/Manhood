using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Manhood
{
    internal class PatternList
    {
        public char Symbol;

        public string Title;

        public string[] Patterns;

        public PatternList(string title, char symbol, string[] patterns)
        {
            this.Title = title;
            this.Symbol = symbol;
            this.Patterns = patterns;
        }

        public bool Merge(PatternList list)
        {
            if (list.Symbol != this.Symbol)
            {
                return false;
            }
            int o = this.Patterns.Length;
            int s = this.Patterns.Length + list.Patterns.Length;

            if (o == s)
            {
                return true;
            }

            Array.Resize<string>(ref this.Patterns, s);

            Array.Copy(list.Patterns, 0, this.Patterns, o, list.Patterns.Length);

            return true;
        }

        public PatternList(string path, ref int totalPatterns)
        {
            if (!Directory.Exists(path))
            {
                throw new ArgumentException("Directory not found: " + path);
            }

            if (!File.Exists(path + "\\info.txt"))
            {
                throw new FileNotFoundException("Info.txt was missing from path: " + path);
            }
            using (StreamReader infoReader = new StreamReader(path + "\\info.txt"))
            {
                bool titleFound = false;
                bool symbolFound = false;
                while (!infoReader.EndOfStream)
                {
                    string rawEntry = infoReader.ReadLine();
                    if (rawEntry.StartsWith("title:"))
                    {
                        this.Title = rawEntry.Substring(6).Trim();
                        titleFound = true;
                    }
                    else if (rawEntry.StartsWith("symbol:"))
                    {
                        string symbolEntry = rawEntry.Substring(7).Trim();
                        if (symbolEntry.Length != 1)
                        {
                            throw new Exception("Pattern symbol must be one character long.");
                        }
                        symbolFound = true;
                        this.Symbol = symbolEntry[0];
                    }
                }
                if (!titleFound || !symbolFound)
                {
                    throw new Exception("Info.txt is incomplete for list at directory: " + path);
                }
                infoReader.Close();
            }

            string[] pathList = Directory.GetFiles(path, "*.pat");
            List<string> patterns = new List<string>();
            foreach (string filePath in pathList)
            {
                StreamReader reader = new StreamReader(filePath);
                patterns.Add(reader.ReadToEnd().Trim());
                totalPatterns++;
                reader.Close();
            }
            if (patterns.Count == 0)
            {
                throw new Exception("List \"" + this.Title + "\" is empty.");
            }

            this.Patterns = patterns.ToArray();
        }

        public string GetPattern(ManRandom r)
        {
            return this.Patterns[r.Next(0, this.Patterns.Length)];
        }
    }
}
