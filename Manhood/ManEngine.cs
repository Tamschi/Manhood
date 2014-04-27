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
    /// <summary>
    /// Used to load content files and intrepret Manhood patterns.
    /// </summary>
    public partial class ManEngine
    {
        Dictionary<char, WordList> wordBank;
        Dictionary<string, Pattern> patternBank;
        Dictionary<string, Definition> defBank;
        Dictionary<string, Func<ManRandom, string>> customFuncs;
        List<string> flagsGlobal, flagsLocal;
        StringBuilder errorLog;

        /// <summary>
        /// Initializes a new instance of the Manhood.ManEngine class and loads the content at the specified path.
        /// </summary>
        /// <param name="packPath">The path to the content pack to load.</param>
        public ManEngine(string packPath)
        {
            Init();
            Load(packPath);
        }

        /// <summary>
        /// Initalizes a new instance of the Manhood.ManEngine class.
        /// </summary>
        public ManEngine()
        {
            Init();
        }

        private void Init()
        {
            errorLog = new StringBuilder();
            flagsGlobal = new List<string>();
            flagsLocal = new List<string>();
            customFuncs = new Dictionary<string, Func<ManRandom, string>>();
            wordBank = new Dictionary<char, WordList>();
            patternBank = new Dictionary<string, Pattern>();
            defBank = new Dictionary<string, Definition>();
        }

        /// <summary>
        /// Gets the word bank loaded by the engine.
        /// </summary>
        public Dictionary<char, WordList> WordBank
        {
            get { return wordBank; }
        }

        /// <summary>
        /// Gets the pattern bank loaded by the engine.
        /// </summary>
        public Dictionary<string, Pattern> PatternBank
        {
            get { return patternBank; }
        }

        /// <summary>
        /// Gets the definitions loaded by the engine.
        /// </summary>
        public Dictionary<string, Definition> Definitions
        {
            get { return defBank; }
        }

        /// <summary>
        /// Gets the collection of custom functions used by this instance.
        /// </summary>
        public Dictionary<string, Func<ManRandom, string>> CustomFunctions
        {
            get { return customFuncs; }
        }

        /// <summary>
        /// Gets the current error log.
        /// </summary>
        public StringBuilder ErrorLog
        {
            get { return errorLog; }
        }

        /// <summary>
        /// Interprets globals and stores their values.
        /// </summary>
        /// <param name="rand">The random number generator to pass to the interpreter.</param>
        public void AssignGlobals(ManRandom rand) // redo in interpreter locally
        {
            foreach (KeyValuePair<string, Definition> entry in defBank)
            {
                var ogc = new OutputGroup();
                Interpret(rand, ogc, entry.Value.Body);
                entry.Value.State = ogc.ToString();
            }
        }

        /// <summary>
        /// Generates output from a pattern string.
        /// </summary>
        /// <param name="random">The random number generator to pass to the interpreter.</param>
        /// <param name="pattern">The pattern to interpret.</param>
        /// <returns></returns>
        public string GenerateText(ManRandom random, string pattern)
        {
            errorLog.Clear();
            var ogc = new OutputGroup();

            Interpret(random, ogc, pattern);

            return ogc.ToString();
        }

        /// <summary>
        /// Generates output from a pattern loaded by the engine.
        /// </summary>
        /// <param name="random">The random number generator to pass to the interpreter.</param>
        /// <param name="patName">The name of the pattern.</param>
        /// <returns></returns>
        public string GenerateTextFromPattern(ManRandom random, string patName)
        {
            errorLog.Clear();
            var ogc = new OutputGroup();

            Interpret(random, ogc, patternBank[patName].Body);

            return ogc.ToString();
        }

        /// <summary>
        /// Generates an output group from a pattern string.
        /// </summary>
        /// <param name="random">The random number generator to pass to the interpreter.</param>
        /// <param name="pattern">The pattern to interpret.</param>
        /// <returns></returns>
        public OutputGroup GenerateOutputGroup(ManRandom random, string pattern)
        {
            errorLog.Clear();
            var ogc = new OutputGroup();

            Interpret(random, ogc, pattern);

            return ogc;
        }

        /// <summary>
        /// Generates an output group from a pattern loaded by the engine.
        /// </summary>
        /// <param name="random">The random number generator to pass to the interpreter.</param>
        /// <param name="patName">The name of the pattern.</param>
        /// <returns></returns>
        public OutputGroup GenerateOutputGroupFromPattern(ManRandom random, string patName)
        {
            errorLog.Clear();
            var ogc = new OutputGroup();

            Interpret(random, ogc, patternBank[patName].Body);

            return ogc;
        }

        /// <summary>
        /// Expands the definitions used in the specified pattern and returns the result.
        /// </summary>
        /// <param name="pattern">The pattern to expand.</param>
        /// <returns></returns>
        public string Expand(string pattern)
        {
            return TranslateDefs(pattern);
        }

        /// <summary>
        /// Gets a global flag.
        /// </summary>
        /// <param name="flagName">The name of the flag.</param>
        public void SetGlobalFlag(string flagName)
        {
            if (!flagsGlobal.Contains(flagName))
            {
                flagsGlobal.Add(flagName);
            }
        }

        /// <summary>
        /// Unsets a global flag.
        /// </summary>
        /// <param name="flagName">The name of the flag.</param>
        public void UnsetGlobalFlag(string flagName)
        {
            if (flagsGlobal.Contains(flagName))
            {
                flagsGlobal.Remove(flagName);
            }
        }

        /// <summary>
        /// Returns the set status of a global flag.
        /// </summary>
        /// <param name="flagName">The name of the flag.</param>
        /// <returns></returns>
        public bool CheckGlobalFlag(string flagName)
        {
            return flagsGlobal.Contains(flagName);
        }

        #region Non-public methods

        private void Load(string packPath)
        {
            using (EasyReader reader = new EasyReader(packPath))
            {
                ContentType type;
                while (!reader.EndOfStream)
                {
                    switch (type = (ContentType)reader.ReadByte())
                    {
                        case ContentType.DefTable:
                            {
                                int count = reader.ReadInt32();
                                for (int i = 0; i < count; i++)
                                {
                                    DefinitionType defType = reader.ReadEnum<DefinitionType>();
                                    string title = reader.ReadString();
                                    string[] parameters = reader.ReadStringArray();
                                    string body = reader.ReadString();
                                    bool valid = true;

                                    foreach(string p in parameters)
                                    {
                                        if (!Definition.IsValidName(p))
                                        {
                                            Console.WriteLine("Failed to load def '{0}': Invalid parameter name '{1}': Names must be at least 1 character long and can only contain letters, numbers, dashes and underscores.");
                                            valid = false;
                                        }
                                    }

                                    if (!valid)
                                    {
                                        continue;
                                    }

                                    if (!defBank.ContainsKey(title))
                                    {
                                        try
                                        {
                                            defBank.Add(title, new Definition(defType, title, body, parameters.ToList()));
                                        }
                                        catch(Exception ex)
                                        {
                                            Console.WriteLine("Failed to load def '{0}': {1}", title, ex.Message);
                                        }
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

        #endregion
    }
}
