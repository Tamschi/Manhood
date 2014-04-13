using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using EasyIO;
using System.Globalization;

namespace Manhood
{
    public partial class ManEngine
    {
        Dictionary<char, WordList> wordBank;
        Dictionary<string, Pattern> patternBank;
        Dictionary<string, Definition> defBank;
        Dictionary<string, string> patternScriptBank;
        List<string> flagsGlobal, flagsLocal;
        StringBuilder errorLog;

        public ManEngine(string packPath)
        {
            Init();
            Load(packPath);
        }

        public ManEngine()
        {
            Init();
        }

        private void Init()
        {
            this.State = new EngineState();

            errorLog = new StringBuilder();
            flagsGlobal = new List<string>();
            flagsLocal = new List<string>();
            wordBank = new Dictionary<char, WordList>();
            patternBank = new Dictionary<string, Pattern>();
            defBank = new Dictionary<string, Definition>();
            patternScriptBank = new Dictionary<string, string>();
        }

        private void Load(string packPath)
        {
            using(EasyReader reader = new EasyReader(packPath))
            {
                ContentType type;
                while(!reader.EndOfStream)
                {
                    switch (type = (ContentType)reader.ReadByte())
                    {
                        case ContentType.DefTable:
                            {
                                int count = reader.ReadInt32();
                                for(int i = 0; i < count; i++)
                                {
                                    DefinitionType defType = reader.ReadEnum<DefinitionType>();
                                    string title = reader.ReadString();
                                    string body = reader.ReadString();
                                    if (!defBank.ContainsKey(title))
                                    {
                                        defBank.Add(title, new Definition(defType, title, body));
                                    }
                                    else
                                    {
                                        Console.WriteLine("Duplicate def '{0}' found in {1}", title, packPath);
                                    }
                                }
                            }
                            break;
                        case ContentType.Pattern:
                            {
                                string title = reader.ReadString();
                                string body = reader.ReadString();
                                if (!patternBank.ContainsKey(title))
                                {
                                    patternBank.Add(title, new Pattern(title, body));
                                }
                                else
                                {
                                    Console.WriteLine("Duplicate pattern '{0}' found in {1}", title, packPath);
                                }
                            }
                            break;
                        case ContentType.Vocabulary:
                            {
                                WordList list = WordList.LoadModernList(reader);
                                if (!wordBank.ContainsKey(list.Symbol))
                                {
                                    wordBank.Add(list.Symbol, list);
                                }
                                else
                                {
                                    Console.WriteLine("Duplicate word list '{0}' found in {1}", list.Symbol, packPath);
                                }
                            }
                        break;
                    }
                }
            }
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
    }
}
