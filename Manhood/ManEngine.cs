using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Globalization;

namespace Manhood
{
    public partial class ManEngine
    {
        Dictionary<char, WordList> wordBank;
        Dictionary<string, PatternList> patternBank;
        Dictionary<string, Definition> defBank;
        Dictionary<string, string> patternScriptBank;
        List<string> flagsGlobal;
        List<string> flagsLocal;
        StringBuilder errorLog;

        public ManEngine(string[] mount)
        {
            Init();

            foreach (string addon in mount)
            {
                MountLegacy(addon);
            }
        }

        public ManEngine()
        {
            Init();
        }

        private void Init()
        {
            errorLog = new StringBuilder();
            flagsGlobal = new List<string>();
            flagsLocal = new List<string>();
            wordBank = new Dictionary<char, WordList>();
            patternBank = new Dictionary<string, PatternList>();
            defBank = new Dictionary<string, Definition>();
            patternScriptBank = new Dictionary<string, string>();
        }

        public Dictionary<char, WordList> WordBank
        {
            get { return wordBank; }
        }

        public Dictionary<string, Definition> Definitions
        {
            get { return defBank; }
        }

        public StringBuilder ErrorLog
        {
            get { return errorLog; }
        }

        private void AssignGlobals(ManRandom rand) // redo in interpreter locally
        {
            foreach (KeyValuePair<string, Definition> entry in defBank)
            {
                var ogc = new OutputCollection();
                Interpret(rand, ogc, entry.Value.Body);
                entry.Value.State = ogc.ToString();
            }
        }

        private void ChangeWeights(ManRandom rand, int distSelect)
        {
            foreach (KeyValuePair<char, WordList> kvp in wordBank)
            {
                kvp.Value.RandomizeDistWeights(rand, distSelect);
            }
        }
        private void CheckPatternType(string type)
        {
            if (!patternBank.ContainsKey(type))
            {
                throw new Exception("Required pattern list is missing: " + type);
            }
        }

        public string GenerateFromSymbol(ManRandom rand, string type)
        {
            errorLog.Clear();
            var ogc = new OutputCollection();

            GenerateFromPattern(rand, ogc, patternBank[type]);

            return ogc.ToString();
        }

        public string GenerateFromPattern(ManRandom random, string pattern)
        {
            errorLog.Clear();
            var ogc = new OutputCollection();

            Interpret(random, ogc, pattern);

            return ogc.ToString();
        }

        public OutputCollection GenerateOGC(ManRandom random, string pattern)
        {
            errorLog.Clear();
            var ogc = new OutputCollection();

            Interpret(random, ogc, pattern);

            return ogc;
        }

        public string Expand(string pattern)
        {
            return TranslateDefs(pattern, "");
        }

        private bool ClassExists(char symbol, string className)
        {
            return wordBank[symbol].Classes.ContainsKey(className) || className == "";
        }

        private void Error(string problem, CharReader reader)
        {
            int col;
            int line = reader.Source.GetLineNumberFromIndex(reader.Position, out col);
            errorLog.AppendFormat("ERROR (Line {0}, Col {1}): {2}\n", line, col, problem);
        }

        private void Warning(string problem, CharReader reader)
        {
            int col;
            int line = reader.Source.GetLineNumberFromIndex(reader.Position, out col);
            errorLog.AppendFormat("WARNING (Line {0}, Col {1}): {2}\n", line, col, problem);
        }

        private void SetLocalFlag(string flagName)
        {
            if (!flagsLocal.Contains(flagName))
            {
                flagsLocal.Add(flagName);
            }
        }

        private void UnsetLocalFlag(string flagName)
        {
            if (flagsLocal.Contains(flagName))
            {
                flagsLocal.Remove(flagName);
            }
        }

        private bool CheckLocalFlag(string flagName)
        {
            return flagsLocal.Contains(flagName);
        }

        public void SetGlobalFlag(string flagName)
        {
            if (!flagsGlobal.Contains(flagName))
            {
                flagsGlobal.Add(flagName);
            }
        }

        public void UnsetGlobalFlag(string flagName)
        {
            if (flagsGlobal.Contains(flagName))
            {
                flagsGlobal.Remove(flagName);
            }
        }

        public bool CheckGlobalFlag(string flagName)
        {
            return flagsGlobal.Contains(flagName);
        }

        private void GenerateFromPattern(ManRandom rand, OutputCollection ogc, PatternList patterns)
        {
            int which = rand.Next(0, patterns.Patterns.Count);
            string rawPattern = patterns.Patterns[which].PatternText;
            Interpret(rand, ogc, rawPattern);
        }

        private void LoadDefinitions(DefinitionType type, string[] entries)
        {
            foreach(string entry in entries)
            {
                if (entry.StartsWith("//")) continue;
                string[] parts = entry.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;
                if (defBank.ContainsKey(parts[0]))
                {
                    defBank[parts[0]] = new Definition(type, parts[0], parts[1]);
                }
                else
                {
                    defBank.Add(parts[0], new Definition(type, parts[0], parts[1]));
                }
            }
        }
    }
}
