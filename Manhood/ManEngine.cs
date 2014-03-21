using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Globalization;

namespace Manhood
{
    public class ManEngine
    {
        Dictionary<char, WordList> wordBank;
        Dictionary<string, PatternList> patternBank;
        Dictionary<string, string> macroBank;
        Dictionary<string, string> globalBank;
        Dictionary<string, string> globalValues;
        Dictionary<string, string> patternScriptBank;
        List<string> flagsGlobal;
        List<string> flagsLocal;
        List<string> oIntros; // Introduction paragraphs
        List<string> oBodies; // Body paragraphs
        List<string> oEndings; // Ending paragraphs
        StringBuilder errorLog;

        private const uint magicMB = 0xBADDF001;
        private const uint magicTV = 0xBADD5456;

        // +[class]s[subtype]<carrier>
        const string patWordCallLegacy = @"((?:\[)(?<class>\w+)(?:\]))?(?<symbol>\w)((?:\[)(?<subtype>\w+)(?:\]))?((?:\<)(?<carrier>[\w\s]+)(?:\>))?";

        // +s[subtype of class for carrier]
        const string patWordCallModern = @"((?<symbol>\w)(?:\[\s*(?<subtype>\w+)?(\s*of\s*(?<class>[\w\&\,]+))?(\s*for\s*((?<carrier>\w+)|(?:\"")(?<carrier>[\w\s]+)(?:\"")))?\s*\])?)";

        const string patWordCall = @"(" + patWordCallModern + "|" + patWordCallLegacy + ")";

        static readonly Regex regWordCall = new Regex(patWordCall, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public ManEngine(string[] mount)
        {
            errorLog = new StringBuilder();
            flagsGlobal = new List<string>();
            flagsLocal = new List<string>();
            wordBank = new Dictionary<char, WordList>();
            patternBank = new Dictionary<string, PatternList>();
            macroBank = new Dictionary<string, string>();
            globalBank = new Dictionary<string, string>();
            globalValues = new Dictionary<string, string>();
            oIntros = new List<string>();
            oBodies = new List<string>();
            oEndings = new List<string>();

            patternScriptBank = new Dictionary<string, string>();

            foreach (string addon in mount)
            {
                Mount(addon);
            }
        }

        public void Mount(string addonPath)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(addonPath, FileMode.Open), Encoding.ASCII))
            {
                int p = 0;
                uint magic = reader.ReadUInt32();
                if (magic != magicMB && magic != magicTV)
                {
                    throw new InvalidDataException("File is corrupt.");
                }

                reader.ReadLongString();
                reader.ReadLongString();
                reader.ReadLongString();

                if (reader.ReadBoolean()) // macros
                {
                    LoadMacroList(reader.ReadStringArray());
                }
                if (reader.ReadBoolean()) // globals
                {
                    LoadGlobalList(reader.ReadStringArray());
                }

                if (reader.ReadBoolean()) // outlines
                {
                    if (reader.ReadBoolean())
                    {
                        oIntros.AddRange(reader.ReadStringArray());
                    }
                    if (reader.ReadBoolean())
                    {
                        oBodies.AddRange(reader.ReadStringArray());
                    }
                    if (reader.ReadBoolean())
                    {
                        oEndings.AddRange(reader.ReadStringArray());
                    }
                }

                if (reader.ReadBoolean()) // vocab
                {
                    int c = reader.ReadInt32();
                    
                    for(int i = 0; i < c; i++)
                    {
                        reader.ReadLongString(); // filename
                        int bufferLength = reader.ReadInt32(); // data length - skipping because we can just load right off the stream
                        int bufferEnd = bufferLength + (int)reader.BaseStream.Position;
                        var list = new WordList(reader, ref p);
                        reader.BaseStream.Position = bufferEnd;

                        if (wordBank.ContainsKey(list.Symbol))
                        {
                            wordBank[list.Symbol].Merge(list);
                        }
                        else
                        {
                            wordBank.Add(list.Symbol, list);
                        }
                    }
                }

                if (reader.ReadBoolean())
                {
                    int c = reader.ReadInt32();

                    for(int i = 0; i < c; i++)
                    {
                        reader.ReadLongString(); // dir name
                        char sc = reader.ReadChar();
                        string symbol = sc.ToString(); // symbol
                        string title = reader.ReadLongString(); // title

                        var list = new PatternList(title, sc, reader.ReadStringArray());

                        if (patternBank.ContainsKey(symbol))
                        {
                            patternBank[symbol].Merge(list);
                        }
                        else
                        {
                            patternBank.Add(symbol, list);
                        }
                    }
                }

                if (magic == magicTV)
                {
                    if (reader.ReadBoolean())
                    {
                        int c = reader.ReadInt32();

                        for(int i = 0; i < c; i++)
                        {
                            string scriptName = reader.ReadLongString();
                            string scriptContent = reader.ReadLongString();
                            if (!patternScriptBank.ContainsKey(scriptName))
                            {
                                patternScriptBank.Add(scriptName, scriptContent);
                            }
                        }
                    }
                }
            }
        }

        public Dictionary<char, WordList> WordBank
        {
            get { return wordBank; }
        }

        public Dictionary<string, string> Macros
        {
            get { return macroBank; }
        }

        public Dictionary<string, string> Globals
        {
            get { return globalBank; }
        }

        public StringBuilder ErrorLog
        {
            get { return errorLog; }
        }

        public void AssignGlobals(Random rand)
        {
            foreach (KeyValuePair<string, string> entry in globalBank)
            {
                var ogc = new OutputCollection();
                Interpret(rand, ogc, entry.Value);
                globalValues[entry.Key] = ogc.ToString();
            }
        }

        public void SetGlobal(string name, string value)
        {
            if (globalBank.ContainsKey(name))
            {
                globalValues[name] = value;
            }
            else
            {
                globalBank.Add(name, "undefined");
                globalValues.Add(name, value);
            }
        }

        private void ChangeWeights(Random rand, int distSelect)
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

        private void GenerateFromOutline(Random rand, OutputCollection ogc, List<string> outlines)
        {
            errorLog.Clear();
            string outline = outlines[rand.Next(0, outlines.Count)];

            CharReader reader = new CharReader(outline, 0);

            while (!reader.EndOfString)
            {
                char c = reader.ReadChar();

                if (c == '|')
                {
                    ogc["main"].Append("\r\n");
                }
                else if (char.IsLetter(c))
                {
                    if (!patternBank.ContainsKey(c.ToString()))
                    {
                        ogc["main"].Append("<PatternNotFound: " + c.ToString() + ">");
                    }
                    else
                    {
                        GenerateFromPattern(rand, ogc, patternBank[c.ToString()]);
                    }
                }
                else
                {
                    ogc["main"].Append(c);
                }
            }
        }

        public string GenerateFromSymbol(Random rand, string type)
        {
            errorLog.Clear();
            var ogc = new OutputCollection();

            GenerateFromPattern(rand, ogc, patternBank[type]);

            return ogc.ToString();
        }

        public string GenerateFromPattern(Random random, string pattern)
        {
            errorLog.Clear();
            var ogc = new OutputCollection();

            Interpret(random, ogc, pattern);

            return ogc.ToString();
        }

        public OutputCollection GenerateOGC(Random random, string pattern)
        {
            errorLog.Clear();
            var ogc = new OutputCollection();

            Interpret(random, ogc, pattern);

            return ogc;
        }

        public string Expand(string pattern)
        {
            return TranslateDefs(pattern, "", "");
        }

        private string TranslateDefs(string rawPattern, string lastMacro, string lastGlobal)
        {
            CharReader pp = new CharReader(rawPattern, 0);
            string pattern = "";
            if (rawPattern.Contains("=") || rawPattern.Contains("&"))
            {
                while (!pp.EndOfString)
                {
                    char d = pp.ReadChar();
                    if (d == '=')
                    {
                        if (pp.PeekChar() != '[')
                        {
                            Error("Missing '[' in macro call", pp);
                            return "";
                        }

                        pp.Position++; // Skip [
                        int endIndex = rawPattern.IndexOf(']', pp.Position);

                        if (endIndex == -1)
                        {
                            Error("Missing ']' in macro call", pp);
                            return "";
                        }

                        string macroName = pp.ReadString(endIndex - pp.Position);

                        if (macroName == "")
                        {
                            Error("Empty macro.", pp);
                            return "";
                        }
                        if (macroName.Contains("+"))
                        {
                            string[] macroParts = macroName.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                            if (macroParts.Length == 0)
                            {
                                Error("Empty macro.", pp);
                                return "";
                            }
                            for (int i = 0; i < macroParts.Length; i++)
                            {
                                string macroPart = macroParts[i].Trim();
                                if (!macroBank.ContainsKey(macroPart))
                                {
                                    Error("Macro \"" + macroPart + "\" doesn't exist.", pp);
                                    return "";
                                }
                                pattern += TranslateDefs(macroBank[macroPart], lastMacro, lastGlobal);
                            }
                        }
                        else
                        {
                            if (macroName == lastMacro)
                            {
                                Error("Macro error: Cannot create a macro that references itself. (" + macroName + ")", pp);
                                return "";
                            }                            
                            if (!macroBank.ContainsKey(macroName))
                            {
                                Error("Macro \"" + macroName + "\" doesn't exist.", pp);
                                return "";
                            }
                            pattern += TranslateDefs(macroBank[macroName], macroName, lastGlobal);
                        }
                        pp.Position++; // Skip ]  
                    }
                    else if (d == '&') // Bracket check in case we hit a flag
                    {
                        if (pp.PeekChar() != '[')
                        {
                            Error("Missing '[' in global call", pp);
                            return "";
                        }
                        pp.Position++; // Skip [
                        int endIndex = rawPattern.IndexOf(']', pp.Position);

                        if (endIndex == -1)
                        {
                            Error("Missing ']' in global call", pp);
                            return "";
                        }

                        string globalName = pp.ReadString(endIndex - pp.Position);
                        if (globalName == lastGlobal)
                        {
                            Error("Global error: Cannot create a global that references itself. (" + globalName + ")", pp);
                            return "";
                        }
                        pp.Position++; // Skip ]
                        if (!globalValues.ContainsKey(globalName))
                        {
                            Error("Global \"" + globalName + "\" doesn't exist.", pp);
                            return "";
                        }
                        pattern += TranslateDefs(globalValues[globalName], lastMacro, globalName);
                    }
                    else
                    {
                        pattern += d;
                    }
                }
            }
            else
            {
                pattern = rawPattern;
            }
            return pattern;
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

        private void Interpret(Random rand, OutputCollection stream, string rawPattern)
        {
            CharReader reader = new CharReader(TranslateDefs(rawPattern, "", ""), 0);

            // Output stuff
            WordFormat currentFormat = WordFormat.None;
            string buffer = "";

            List<OutputGroup> outputGroups = new List<OutputGroup>(new[] { new OutputGroup("main", 0, reader.Source.Length) });

            // Input stuff
            char c = ' ';
            char prev = ' '; // This is only used by the Proper format mode to determine the start of a word

            // A/An conversion stuff
            int anIndex = -1;
            WordFormat anFormat = WordFormat.None;

            // Selector stuff
            int uniformSeedSalt = rand.Next(0, 1000000);
            List<int> activeSelectors = new List<int>();
            List<int> selectorUniformIDs = new List<int>();
            int currentUniformID = -1;

            // Word lock stuff
            Dictionary<string, Dictionary<string, int>> locks = new Dictionary<string, Dictionary<string, int>>();

            // Repeater stuff
            List<RepeaterInstance> repeaters = new List<RepeaterInstance>();

            while (!reader.EndOfString) // Read through pattern until we reach the end
            {
                prev = c;
                c = reader.ReadChar();
                
                if (c == '\\' && !reader.EndOfString) // Escape character
                {
                    buffer = reader.ReadChar().ToString();
                    goto dobuffer;
                }

                #region Output groups
                else if (c == '<')
                {
                    int beginIndex = reader.Source.IndexOf(':', reader.Position);
                    if (beginIndex == -1)
                    {
                        Error("Couldn't find output group name terminator.", reader);
                        return;
                    }

                    string groupName = reader.ReadTo(beginIndex).ToLower();
                    reader.ReadChar(); // skip ':'

                    int endIndex = reader.Source.FindClosingTriangleBracket(reader.Position);
                    if (endIndex == -1)
                    {
                        Error(String.Format("Closing bracket couldn't be found for output group '{0}'.", groupName), reader);
                        return;
                    }

                    outputGroups.Add(new OutputGroup(groupName, beginIndex + 1, endIndex));
                }
                else if (c == '>')
                {
                    var group = outputGroups.LastOrDefault(gi => gi.Name.ToLower() != "main");
                    if (group == null)
                    {
                        Error("Output group closure found with no associated instance.", reader);
                        return;
                    }
                    outputGroups.RemoveAt(outputGroups.Count - 1);
                }
                #endregion

                #region Frequency indicators
                else if (Char.IsNumber(c)) // Check if frequency indicator is here. Example: 40%{ +n[plural] are +A. }
                {
                    int oldPos = reader.Position;
                    int percentIndex = reader.Source.IndexOf('%', reader.Position);
                    int nextSpace = reader.Source.IndexOf(' ', reader.Position);
                    if (percentIndex > -1 && (percentIndex < nextSpace || nextSpace == -1))
                    {
                        reader.Position--; // Revert reading of first digit
                        string percentStr = reader.ReadString(percentIndex - reader.Position);
                        int percent;
                        if (!int.TryParse(percentStr, out percent))
                        {
                            reader.Position = oldPos;
                            goto fiSkip;
                        }

                        if (percent > 100)
                        {
                            percent = 100;
                        }
                        else if (percent <= 0)
                        {
                            Error("0% frequency indicator detected. Why is this here?", reader);
                            return;
                        }

                        reader.Position++; // Skip past '%'

                        if ((char)reader.PeekChar() == '[') // Make sure this bitch is tight
                        {
                            reader.Position++; // Skip past '['
                            int closure = reader.Source.FindClosingSquareBracket(reader.Position);

                            if (closure < 0)
                            {
                                Error("Missing closing bracket in frequency indicator.", reader);
                                return;
                            }

                            if (rand.Next(0, 101) > percent)
                            {
                                reader.Position = closure;
                            }

                            continue;
                        }


                        fiSkip:
                        reader.Position = oldPos; // Fall back to beginning of number if there is a false positive and it's just a number
                                            
                    }
                }
                #endregion

                #region Selectors
                if (c == '{') // Selector. Picks random item inside brackets. Example: {First/second/third/fourth/...}
                {
                    int end = reader.Source.FindClosingCurlyBracket(reader.Position);
                    if (end == -1)
                    {
                        Error("Incomplete curly brackets. Did you forget to close a selector?", reader);
                        return;
                    }
                    int[] startIndices = reader.Source.GetSelectorSubs(reader.Position);
                    if (startIndices.Length < 2)
                    {
                        Error("Selector is empty or only has one option.", reader);
                        return;
                    }
                    activeSelectors.Add(end + 1);
                    selectorUniformIDs.Add(currentUniformID);
                    if (currentUniformID > -1)
                    {
                        Random uniRand = new Random(uniformSeedSalt + currentUniformID);
                        reader.Position = startIndices[uniRand.Next(0, startIndices.Length)];
                    }
                    else
                    {
                        reader.Position = startIndices[rand.Next(0, startIndices.Length)];
                    }
                    currentUniformID = -1;
                }
                else if (c == '}')
                {
                    if (activeSelectors.Count == 0)
                    {
                        Error("Unexpected '}' found in pattern.", reader);
                        return;
                    }

                    activeSelectors.RemoveAt(activeSelectors.Count - 1);
                    selectorUniformIDs.RemoveAt(selectorUniformIDs.Count - 1);
                    continue;
                }
                else if (c == '/')
                {
                    if (activeSelectors.Count == 0)
                    {
                        Error("Unexpected '/' found in pattern.", reader);
                        return;
                    }
                    reader.Position = activeSelectors[activeSelectors.Count - 1];
                    activeSelectors.RemoveAt(activeSelectors.Count - 1);
                    selectorUniformIDs.RemoveAt(selectorUniformIDs.Count - 1);
                    continue;
                }
                else if (c == '*')
                {
                    int bracketIndex = reader.Source.IndexOf("{", reader.Position);
                    if (bracketIndex <= reader.Position)
                    {
                        Error("Uniform operator could not find a selector to associate with.", reader);
                        return;
                    }
                    string strUID = reader.ReadString(bracketIndex - reader.Position);
                    int uid;
                    if (!int.TryParse(strUID, out uid))
                    {
                        Error("Uniform ID was not a number.", reader);
                        return;
                    }
                    else if (uid < 0)
                    {
                        Error("Uniform ID's cannot be negative.", reader);
                        return;
                    }
                    currentUniformID = uid;
                    continue;
                }
                #endregion

                #region Repeaters
                if (c == '^')
                {
                    // iteration range
                    if (reader.PeekChar() != '[')
                    {
                        Error("Repeater iterations parameter did not have an opening bracket.", reader);
                        return;
                    }
                    reader.ReadChar(); // skip [
                    int rightRangeBracketIndex = reader.Source.FindClosingSquareBracket(reader.Position);
                    if (rightRangeBracketIndex < 0)
                    {
                        Error("Repeater iterations parameter did not have a closing bracket.", reader);
                        return;
                    }
                    string strRangeParameter = reader.ReadString(rightRangeBracketIndex - reader.Position).Trim();
                    reader.ReadChar(); // skip ]
                    int constantParam = 0;
                    int min = 0;
                    int max = 0;
                    if (!int.TryParse(strRangeParameter, out constantParam))
                    {
                        string[] parts = strRangeParameter.Split(new char[] { '-' });
                        if (parts.Length != 2)
                        {
                            Error("Repeater range parameter must be a pair of two numbers.", reader);
                            return;
                        }
                        if (!int.TryParse(parts[0], out min) || !int.TryParse(parts[1], out max))
                        {
                            Error("Repeater range parameter did not contain valid numbers.", reader);
                            return;
                        }
                        if (min > max || min == 0 || max == 0)
                        {
                            Error("Repeater range must be greater than zero, and max > min.", reader);
                            return;
                        }
                        constantParam = rand.Next(min, max);
                    }
                    // separator
                    if (reader.ReadChar() != '[')
                    {
                        Error("Repeater separator parameter did not have an opening bracket.", reader);
                        return;
                    }
                    int sepIndex = reader.Position;
                    int rightSepBracketIndex = reader.Source.FindClosingSquareBracket(reader.Position);
                    if (rightSepBracketIndex < 0)
                    {
                        Error("Repeater separator parameter did not have a closing bracket.", reader);
                    }
                    string strSepParameter = reader.ReadString(rightSepBracketIndex - reader.Position);
                    int sepEnd = reader.Position;
                    reader.ReadChar(); // skip ]

                    // content
                    if (reader.ReadChar() != '[')
                    {
                        Error("Repeater content parameter did not have an opening bracket.", reader);
                        return;
                    }
                    int rightContentBracketIndex = reader.Source.FindClosingSquareBracket(reader.Position);
                    if (rightSepBracketIndex < 0)
                    {
                        Error("Repeater content parameter did not have a closing bracket.", reader);
                    }
                    int pStart = reader.Position;

                    reader.ReadString(rightContentBracketIndex - reader.Position);

                    int pEnd = reader.Position;

                    repeaters.Add(new RepeaterInstance(pStart, pEnd, sepIndex, sepEnd, strSepParameter, constantParam));

                    reader.Position = pStart;

                    SetLocalFlag("odd_" + repeaters.Count);
                    SetLocalFlag("first_" + repeaters.Count);
                }
                else if (c == ']' && repeaters.Count > 0) // End of repeater iteration?
                {
                    int repeaterCount = repeaters.Count;
                    int last = repeaterCount - 1;
                    RepeaterInstance rep = repeaters[last];
                    if (reader.Position - 1 != rep.ContentEndIndex && reader.Position - 1 != rep.SeparatorEndIndex)
                    {
                        continue;
                    }

                    if (repeaters[last].Iterations == 0)
                    {
                        SetLocalFlag("first_" + repeaterCount);
                        UnsetLocalFlag("last_" + repeaterCount);
                    }
                    else if (repeaters[last].Iterations == repeaters[last].MaxIterations - 1)
                    {
                        UnsetLocalFlag("first_" + repeaterCount);
                        SetLocalFlag("last_" + repeaterCount);
                    }

                    if (rep.OnSeparator) // Currently writing separator?
                    {
                        rep.OnSeparator = false;
                        reader.Position = rep.ContentStartIndex;
                    }
                    else // Currently writing content?
                    {                        
                        if (repeaters[last].Elapse())
                        {
                            UnsetLocalFlag("odd_" + repeaterCount);
                            UnsetLocalFlag("even_" + repeaterCount);
                            repeaters.RemoveAt(last); // Remove the last repeater if it's finished
                            continue;
                        }
                        else
                        {
                            if ((repeaters[last].Iterations + 1) % 2 == 0)
                            {
                                UnsetLocalFlag("odd_" + repeaterCount);
                                SetLocalFlag("even_" + repeaterCount);
                            }
                            else
                            {
                                SetLocalFlag("odd_" + repeaterCount);
                                UnsetLocalFlag("even_" + repeaterCount);
                            }

                            rep.OnSeparator = true;
                            reader.Position = rep.SeparatorStartIndex; // Add separator if not
                        }
                    }
                }
                #endregion

                #region Flags
                else if (c == '$')
                {
                    int leftBracket = reader.Source.IndexOf("[", reader.Position);
                    if (leftBracket < 0)
                    {
                        Error("Missing '[' on flag call.", reader);
                        return;
                    }
                    string func = reader.ReadString(leftBracket - reader.Position).ToLower();
                    reader.ReadChar(); // skip [
                    if (func.Contains(' '))
                    {
                        Error("Invalid flag function.", reader);
                        return;
                    }
                    int rightBracket = reader.Source.FindClosingSquareBracket(reader.Position);
                    if (rightBracket < leftBracket)
                    {
                        Error("Missing ']' on flag call.", reader);
                        return;
                    }
                    string firstParam = reader.ReadString(rightBracket - reader.Position);
                    reader.ReadChar(); // skip ]
                    if (func == "ls")
                    {
                        SetLocalFlag(firstParam);
                        continue;
                    }
                    else if (func == "lu")
                    {
                        UnsetLocalFlag(firstParam);
                        continue;
                    }
                    else if (func == "l?")
                    {
                        if (reader.ReadChar() != '[')
                        {
                            Error("Missing '[' in IF body.", reader);
                            return;
                        }
                        int rightBodyBracket = reader.Source.FindClosingSquareBracket(reader.Position);
                        if (rightBodyBracket < 0)
                        {
                            Error("Missing ']' in IF body.", reader);
                            return;
                        }
                        if (!CheckLocalFlag(firstParam))
                        {
                            reader.Position = rightBodyBracket;
                        }
                        continue;
                    }
                    else if (func == "l!")
                    {
                        if (reader.ReadChar() != '[')
                        {
                            Error("Missing '[' in IF body.", reader);
                            return;
                        }
                        int rightBodyBracket = reader.Source.FindClosingSquareBracket(reader.Position);
                        if (rightBodyBracket < 0)
                        {
                            Error("Missing ']' in IF body.", reader);
                            return;
                        }
                        if (CheckLocalFlag(firstParam))
                        {
                            reader.Position = rightBodyBracket;
                        }
                        continue;
                    }
                    else if (func == "gs")
                    {
                        SetGlobalFlag(firstParam);
                        continue;
                    }
                    else if (func == "gu")
                    {
                        UnsetGlobalFlag(firstParam);
                        continue;
                    }
                    else if (func == "g?")
                    {
                        if (reader.ReadChar() != '[')
                        {
                            Error("Missing '[' in IF body.", reader);
                            return;
                        }
                        int rightBodyBracket = reader.Source.IndexOf("]", reader.Position);
                        if (rightBodyBracket < 0)
                        {
                            Error("Missing ']' in IF body.", reader);
                            return;
                        }
                        if (!CheckGlobalFlag(firstParam))
                        {
                            reader.Position = rightBodyBracket;
                        }
                        continue;
                    }
                    else if (func == "g!")
                    {
                        if (reader.ReadChar() != '[')
                        {
                            Error("Missing '[' in IF body.", reader);
                            return;
                        }
                        int rightBodyBracket = reader.Source.IndexOf("]", reader.Position);
                        if (rightBodyBracket < 0)
                        {
                            Error("Missing ']' in IF body.", reader);
                            return;
                        }
                        if (CheckGlobalFlag(firstParam))
                        {
                            reader.Position = rightBodyBracket;
                        }
                        continue;
                    }
                    else
                    {
                        Error("Unrecognized flag function.", reader);
                        return;
                    }
                }
                #endregion

                #region Random word
                else if (c == '+') // Random word
                {
                    var match = regWordCall.Match(reader.Source, reader.Position);
                    if (!(match.Success && match.Index == reader.Position))
                    {
                        Warning("Invalid word call", reader);
                        continue;
                    }

                    var groups = match.Groups;
                    string className = groups["class"].Value.Trim();
                    string subtype = groups["subtype"].Value.Trim();
                    string carrier = groups["carrier"].Value.Trim();
                    char symbol = groups["symbol"].Value[0];

                    reader.Position = match.Index + match.Length;

                    if (!wordBank.ContainsKey(symbol)) // Make sure the symbol is registered
                    {
                        Warning("Word symbol not found: '" + symbol.ToString() + "'", reader);
                    }
                    else if (carrier != "")
                    {
                        string carrierKey = String.Format("{0}:{1}", className, symbol);
                        Dictionary<string, int> cd;
                        if (!locks.TryGetValue(carrierKey, out cd))
                        {
                            cd = new Dictionary<string,int>();
                            cd.Add(carrier, wordBank[symbol].GetRandomIndex(rand, className));        
                            locks.Add(carrierKey, cd);
                        }
                        else if (!cd.ContainsKey(carrier))
                        {
                            cd.Add(carrier, wordBank[symbol].GetRandomIndex(rand, className));                            
                        }
                        buffer = wordBank[symbol].GetWordByIndex(locks[carrierKey][carrier], subtype, currentFormat);
                    }
                    else
                    {
                        if (className.Contains(","))
                        {
                            string[] mcNames = className.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (mcNames.Length < 2)
                            {
                                Error("A multi-class expression must include more than one class name in its parameters.", reader);
                                return;
                            }
                            for (int i = 0; i < mcNames.Length; i++)
                            {
                                mcNames[i] = mcNames[i].Trim(); // this is to get rid of spaces between the class names
                            }
                            for (int i = 0; i < mcNames.Length; i++)
                            {
                                if (!ClassExists(symbol, mcNames[i]))
                                {
                                    Error("Bad multiclass", reader);
                                    return;
                                }
                            }
                            buffer = wordBank[symbol].GetRandomWordMultiClass(rand, subtype, currentFormat, mcNames);
                        }
                        else if (!ClassExists(symbol, className))
                        {
                            Warning("Class not found: " + symbol.ToString() + " -> " + className, reader);
                        }
                        else
                        {
                            int index = wordBank[symbol].GetRandomIndex(rand, className);
                            buffer = wordBank[symbol].GetWordByIndex(index, subtype, currentFormat);
                        }
                    }
                    buffer = buffer.Capitalize(currentFormat);
                    if (anIndex > -1 && buffer.StartsWithVowel())
                    {
                        if (anFormat == WordFormat.AllCaps)
                        {
                            stream[outputGroups[outputGroups.Count - 1].Name].Insert(anIndex, "N");
                        }
                        else
                        {
                            stream[outputGroups[outputGroups.Count - 1].Name].Insert(anIndex, "n");
                        }
                    }

                    if (currentFormat == WordFormat.Capitalized)
                    {
                        currentFormat = WordFormat.None;
                    }

                    anIndex = -1;
                    anFormat = WordFormat.None;
                }
                #endregion

                else if (c == '|' || c == '\n') // Line break
                {
                    buffer = "\r\n";
                }
                else if (c == '~') // Capitalize
                {
                    if (reader.PeekChar() == '~')
                    {
                        reader.ReadChar();
                        currentFormat = WordFormat.Capitalized;
                    }
                    else if (currentFormat == WordFormat.Proper)
                    {
                        currentFormat = WordFormat.None;
                    }
                    else
                    {
                        currentFormat = WordFormat.Proper;
                    }
                }
                else if (c == '@') // Capslock
                {
                    if (currentFormat == WordFormat.AllCaps)
                    {
                        currentFormat = WordFormat.None;
                    }
                    else
                    {
                        currentFormat = WordFormat.AllCaps;
                    }
                }
                else if (c == '#') // Random number
                {
                    if (reader.PeekChar() == '[')
                    {
                        reader.Position++;

                        int closure = reader.Source.IndexOf(']', reader.Position);
                        if (closure < 0)
                        {
                            Error("Incomplete parenthases in random number range.", reader);
                            return;
                        }

                        string rangeStr = reader.ReadString(closure - reader.Position);
                        reader.Position++; // Skip past ']'

                        string[] rangeParts = rangeStr.Split('-');

                        if (rangeParts.Length != 2)
                        {
                            Error("Invalid number of range elements for random number. Got " + rangeParts.Length + ", need 2.", reader);
                            return;
                        }

                        int min, max;

                        if (!int.TryParse(rangeParts[0], out min))
                        {
                            Error("Invalid minimum value for random number.", reader);
                            return;
                        }

                        if (!int.TryParse(rangeParts[1], out max))
                        {
                            Error("Invalid maximum value for random number.", reader);
                            return;
                        }

                        if (currentFormat == WordFormat.Capitalized)
                        {
                            currentFormat = WordFormat.None;
                        }

                        buffer = rand.Next(min, max).ToString();
                    }
                }
                else if (!"{}[]<>".Contains(c)) // Covers all other characters except brackets
                {
                    if (prev == ' ' && c == 'a' && !char.IsLetterOrDigit((char)reader.PeekChar())) // YES! YES!
                    {
                        anIndex = stream[outputGroups[outputGroups.Count - 1].Name].Length + 1;
                        anFormat = currentFormat;
                    }

                    if (currentFormat == WordFormat.AllCaps || (currentFormat == WordFormat.Proper && !Char.IsLetterOrDigit(prev) && prev.PermitsCap()))
                    {
                        buffer = c.ToString().ToUpper();
                    }
                    else if (currentFormat == WordFormat.Capitalized)
                    {
                        buffer = c.ToString().ToUpper();
                        currentFormat = WordFormat.None;
                    }
                    else
                    {
                        buffer = c.ToString();
                    }
                }

            dobuffer:

                int groupCount = outputGroups.Count;
                var currentGroup = outputGroups[groupCount - 1];
                var gVis = currentGroup.Visibility;

                switch(gVis)
                {
                    case GroupVisibility.Public:
                        {
                            foreach(var group in outputGroups)
                            {
                                stream[group.Name].Append(buffer);
                            }
                        }
                        break;
                    case GroupVisibility.Internal:
                        {
                            OutputGroup group = null;
                            for(int i = 0; i < outputGroups.Count; i++)
                            {
                                group = outputGroups[groupCount - (i + 1)];
                                if (group.Visibility != GroupVisibility.Internal) break;
                                stream[group.Name].Append(buffer);
                            }
                        }
                        break;
                    case GroupVisibility.Private:
                        {
                            stream[currentGroup.Name].Append(buffer);
                        }
                        break;
                }

                buffer = "";
            }
            flagsLocal.Clear();
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

        private void GenerateFromPattern(Random rand, OutputCollection ogc, PatternList patterns)
        {
            int which = rand.Next(0, patterns.Patterns.Length);
            string rawPattern = patterns.Patterns[which];
            Interpret(rand, ogc, rawPattern);
        }

        private void LoadMacroList(string[] entries)
        {
            foreach(string entry in entries)
            {
                if (entry.StartsWith("//")) continue;
                string[] parts = entry.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;
                if (macroBank.ContainsKey(parts[0]))
                {
                    macroBank[parts[0]] = parts[1];
                }
                else
                {
                    macroBank.Add(parts[0], parts[1]);
                }
            }
        }

        private void LoadGlobalList(string[] entries)
        {
            foreach (string entry in entries)
            {
                if (entry.StartsWith("//")) continue;
                string[] parts = entry.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;
                if (globalBank.ContainsKey(parts[0]))
                {
                    globalBank[parts[0]] = parts[1];
                }
                else
                {
                    globalBank.Add(parts[0], parts[1]);
                }
            }
        }
    }
}
