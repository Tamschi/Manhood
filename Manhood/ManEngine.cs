using System;
using System.Collections.Generic;
using System.Linq;
using EasyIO;

namespace Manhood
{
    /// <summary>
    /// Used to load content files and intrepret Manhood patterns.
    /// </summary>
    public partial class ManEngine
    {
        /// <summary>
        /// Raised when the last Manhood interpreter session encounters errors. Provides an error log.
        /// </summary>
        public event EventHandler<ManhoodErrorEventArgs> Errors;

        Dictionary<char, WordList> _wordBank;
        Dictionary<string, Pattern> _patternBank;
        Dictionary<string, Definition> _defBank;
        Dictionary<string, Func<ManRandom, string>> _customFuncs;
        List<string> _flagsGlobal, _flagsLocal;

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
            _flagsGlobal = new List<string>();
            _flagsLocal = new List<string>();
            _customFuncs = new Dictionary<string, Func<ManRandom, string>>();
            _wordBank = new Dictionary<char, WordList>();
            _patternBank = new Dictionary<string, Pattern>();
            _defBank = new Dictionary<string, Definition>();
        }

        /// <summary>
        /// Gets the word bank loaded by the engine.
        /// </summary>
        public Dictionary<char, WordList> WordBank
        {
            get { return _wordBank; }
        }

        /// <summary>
        /// Gets the pattern bank loaded by the engine.
        /// </summary>
        public Dictionary<string, Pattern> PatternBank
        {
            get { return _patternBank; }
        }

        /// <summary>
        /// Gets the definitions loaded by the engine.
        /// </summary>
        public Dictionary<string, Definition> Definitions
        {
            get { return _defBank; }
        }

        /// <summary>
        /// Gets the collection of custom functions used by this instance.
        /// </summary>
        public Dictionary<string, Func<ManRandom, string>> CustomFunctions
        {
            get { return _customFuncs; }
        }

        /// <summary>
        /// Interprets globals and stores their values.
        /// </summary>
        /// <param name="rand">The random number generator to pass to the interpreter.</param>
        public void AssignGlobals(ManRandom rand) // redo in interpreter locally
        {
            foreach (var entry in _defBank)
            {
                var ogc = new OutputGroup();
                Interpret(rand, ogc, entry.Value.Body);
                entry.Value.State = ogc.ToString();
            }
        }

        /// <summary>
        /// Generates output from a pattern string.
        /// </summary>
        /// <param name="pattern">The pattern to interpret.</param>
        /// <returns></returns>
        public string GenerateText(string pattern)
        {
            return GenerateText(new ManRandom(), pattern);
        }

        /// <summary>
        /// Generates output from a pattern string.
        /// </summary>
        /// <param name="random">The random number generator to pass to the interpreter.</param>
        /// <param name="pattern">The pattern to interpret.</param>
        /// <returns></returns>
        public string GenerateText(ManRandom random, string pattern)
        {
            var ogc = new OutputGroup();

            Interpret(random, ogc, pattern);
            CheckForErrors();

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
            var ogc = new OutputGroup();

            Interpret(random, ogc, _patternBank[patName].Body);
            CheckForErrors();

            return ogc.ToString();
        }

        /// <summary>
        /// Generates an output group from a pattern string.
        /// </summary>
        /// <param name="pattern">The pattern to interpret.</param>
        /// <returns></returns>
        public OutputGroup GenerateOutputGroup(string pattern)
        {
            return GenerateOutputGroup(new ManRandom(), pattern);
        }

        /// <summary>
        /// Generates an output group from a pattern string.
        /// </summary>
        /// <param name="random">The random number generator to pass to the interpreter.</param>
        /// <param name="pattern">The pattern to interpret.</param>
        /// <returns></returns>
        public OutputGroup GenerateOutputGroup(ManRandom random, string pattern)
        {
            var ogc = new OutputGroup();

            Interpret(random, ogc, pattern);
            CheckForErrors();

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
            var ogc = new OutputGroup();

            Interpret(random, ogc, _patternBank[patName].Body);
            CheckForErrors();

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
            if (!_flagsGlobal.Contains(flagName))
            {
                _flagsGlobal.Add(flagName);
            }
        }

        /// <summary>
        /// Unsets a global flag.
        /// </summary>
        /// <param name="flagName">The name of the flag.</param>
        public void UnsetGlobalFlag(string flagName)
        {
            if (_flagsGlobal.Contains(flagName))
            {
                _flagsGlobal.Remove(flagName);
            }
        }

        /// <summary>
        /// Returns the set status of a global flag.
        /// </summary>
        /// <param name="flagName">The name of the flag.</param>
        /// <returns></returns>
        public bool CheckGlobalFlag(string flagName)
        {
            return _flagsGlobal.Contains(flagName);
        }

        #region Non-public methods

        private void Load(string packPath)
        {
            using (var reader = new EasyReader(packPath))
            {
                while (!reader.EndOfStream)
                {
                    switch ((ContentType)reader.ReadByte())
                    {
                        case ContentType.DefTable:
                            {
                                var count = reader.ReadInt32();
                                for (var i = 0; i < count; i++)
                                {
                                    var defType = reader.ReadEnum<DefinitionType>();
                                    var title = reader.ReadString();
                                    var parameters = reader.ReadStringArray();
                                    var body = RegComments.Replace(reader.ReadString(), ""); // Filter out comments
                                    var valid = true;

                                    foreach (var p in parameters.Where(p => !Definition.IsValidName(p)))
                                    {
                                        Console.WriteLine("Failed to load def '{0}': Invalid parameter name '{1}': Names must be at least 1 character long and can only contain letters, numbers, dashes and underscores.", title, p);
                                        valid = false;
                                    }

                                    if (!valid)
                                    {
                                        continue;
                                    }

                                    if (!_defBank.ContainsKey(title))
                                    {
                                        try
                                        {
                                            _defBank.Add(title, new Definition(defType, title, body, parameters.ToList()));
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
                                var title = reader.ReadString();
                                var body = RegComments.Replace(reader.ReadString(), ""); // Filter out comments
                                if (!_patternBank.ContainsKey(title))
                                {
                                    _patternBank.Add(title, new Pattern(title, body));
                                }
                                else
                                {
                                    Console.WriteLine("Duplicate pattern '{0}' found in {1}", title, packPath);
                                }
                            }
                            break;
                        case ContentType.Vocabulary:
                            {
                                var list = WordList.LoadModernList(reader);
                                if (!_wordBank.ContainsKey(list.Symbol))
                                {
                                    _wordBank.Add(list.Symbol, list);
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

        private void CheckForErrors()
        {
            if (Errors != null && Middleman.State.Errors.Count > 0)
            {
                Errors(this, new ManhoodErrorEventArgs(Middleman.State.Errors));
            }
        }

        private bool ClassExists(char symbol, string className)
        {
            return _wordBank[symbol].Classes.ContainsKey(className) || className == "";
        }

        private void SetLocalFlag(string flagName)
        {
            if (!_flagsLocal.Contains(flagName))
            {
                _flagsLocal.Add(flagName);
            }
        }

        private void UnsetLocalFlag(string flagName)
        {
            if (_flagsLocal.Contains(flagName))
            {
                _flagsLocal.Remove(flagName);
            }
        }

        private bool CheckLocalFlag(string flagName)
        {
            return _flagsLocal.Contains(flagName);
        }

        #endregion
    }
}
