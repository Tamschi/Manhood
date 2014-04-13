using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Manhood
{
    public partial class ManEngine
    {
        // +[class]s[subtype]<carrier>
        const string patWordCallLegacy = @"((?:\[)(?<class>\w+)(?:\]))?(?<symbol>\w)((?:\[)(?<subtype>\w+)(?:\]))?((?:\<)(?<carrier>[\w\s]+)(?:\>))?";

        // +s[subtype of class for carrier]
        const string patWordCallModern = @"((?<symbol>\w)(?:\[\s*(?<subtype>\w+)?(\s*of\s*(?<class>[\w\&\,]+))?(\s*for\s*((?<carrier>\w+)|(?:\"")(?<carrier>[\w\s]+)(?:\"")))?\s*\])?)(?<end>$|[^\<][^\>]|.)";

        static readonly Regex regWordCallLegacy = new Regex(patWordCallLegacy, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        static readonly Regex regWordCallModern = new Regex(patWordCallModern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        internal class EngineState
        {
            public CharReader Reader;
            public WordFormat CurrentFormat = WordFormat.None;
            public WordFormat AnFormat = WordFormat.None;
            public int AnIndex = -1;
            public List<OutputGroup> OutputGroups = new List<OutputGroup>();
            public StringBuilder Buffer;
            public OutputCollection Output;
            public ManRandom RNG;

            private char
                currentChar = '\0',
                prevChar = '\0';

            // Selector stuff
            public int UniformSeedSalt = 0;
            public List<int> ActiveSelectors = new List<int>();
            public List<int> SelectorUniformIDs = new List<int>();
            public int CurrentUID = -1;

            // Carrier stuff
            public Dictionary<string, Dictionary<string, int>> Carriers = new Dictionary<string, Dictionary<string, int>>();

            // Repeater stuff
            public List<RepeaterInstance> Repeaters = new List<RepeaterInstance>();

            public EngineState()
            {
                Reader = null;
                OutputGroups = new List<OutputGroup>();
                Buffer = new StringBuilder();
                Output = new OutputCollection();
            }

            public void Start(ManRandom rng, OutputCollection oc, string pattern)
            {
                RNG = rng;
                Output = oc;
                Reader = new CharReader(pattern, 0);                
                UniformSeedSalt = rng.Next(0, 1000000);

                ActiveSelectors.Clear();
                SelectorUniformIDs.Clear();
                Carriers.Clear();
                Repeaters.Clear();
                OutputGroups.Clear();
                OutputGroups.Add(new OutputGroup("main", 0, Reader.Source.Length));

                CurrentUID = -1;                
            }

            public void WriteBuffer(string content)
            {
                Buffer.Append(content);
            }

            public void WriteBuffer(char content)
            {
                Buffer.Append(content);
            }

            public void FormatBuffer(WordFormat format)
            {
                string str = Buffer.ToString();
                Buffer.Clear();
                Buffer.Append(str.Capitalize(format));
            }

            public void ClearBuffer()
            {
                Buffer.Clear();
            }

            public char ReadChar()
            {
                prevChar = currentChar;
                currentChar = Reader.ReadChar();
                return currentChar;
            }

            public char CurrentChar
            {
                get { return currentChar; }
            }

            public char PrevChar
            {
                get { return prevChar; }
            }

            public int ReadPos
            {
                get { return Reader.Position; }
                set { Reader.Position = value; }
            }

            public string BufferText
            {
                get { return Buffer.ToString(); }
            }
        }

        internal EngineState State;

        private void Interpret(ManRandom rand, OutputCollection output, string rawPattern)
        {
            errorLog.Clear();

            this.State.Start(rand, output, TranslateDefs(rawPattern, ""));

            while (!State.Reader.EndOfString) // Read through pattern until we reach the end
            {
                State.ReadChar();
                if (State.CurrentChar == '\\' && !State.Reader.EndOfString) // Escape character
                {
                    State.WriteBuffer(Escape.GetChar(State.ReadChar()));
                    DoBuffer();
                    continue;
                }

                switch (State.CurrentChar)
                {
                    case '<':
                        if (!DoOutputGroupStart()) return;
                        break;
                    case '>':
                        if (!DoOutputGroupEnd()) return;
                        break;
                    case '{':
                        if (!DoSelectorStart()) return;
                        break;
                    case '}':
                        if (DoSelectorEnd())
                        {
                            continue;
                        }
                        else
                        {
                            return;
                        }
                    case '/':
                        if (DoSelectorItemEnd())
                        {
                            continue;
                        }
                        else
                        {
                            return;
                        }
                    case '*':
                        if (DoSelectorUniform())
                        {
                            continue;
                        }
                        else
                        {
                            return;
                        }
                    case '^':
                        if (!DoRepeaterStart()) return;
                        break;
                    case ']':
                        if (State.Repeaters.Count > 0)
                        {
                            if (DoRepeaterEnd()) continue;
                        }
                        break;
                    case '$':
                        if (!DoFunction()) return;
                        break;
                    case '+':
                        if (!DoWordCall()) return;
                        break;
                    case '|':
                        State.WriteBuffer("\r\n");
                        break;
                    case '~':
                        DoCapitalize();
                        break;
                    case '@':
                        DoCapsLock();
                        break;
                    case '#':
                        if (!DoRandomNumber()) continue;
                        break;
                    default:
                        {
                            if (Char.IsNumber(State.CurrentChar)) // Check if frequency indicator is here. Example: 40%{ +n[plural] are +A. }
                            {
                                if (DoFrequency())
                                {
                                    continue;
                                }
                                else
                                {
                                    return;
                                }
                            }
                            else if (!"{}[]<>".Contains(State.CurrentChar)) // Covers all other characters except brackets
                            {
                                DoNonBrackets();
                            }
                        }
                        break;
                }

                DoBuffer();
            }
            flagsLocal.Clear();
        }

        private void DoCapsLock()
        {
            if (State.CurrentFormat == WordFormat.AllCaps)
            {
                State.CurrentFormat = WordFormat.None;
            }
            else
            {
                State.CurrentFormat = WordFormat.AllCaps;
            }
        }

        private void DoCapitalize()
        {
            if (State.Reader.PeekChar() == '~')
            {
                State.Reader.ReadChar();
                State.CurrentFormat = WordFormat.Capitalized;
            }
            else if (State.CurrentFormat == WordFormat.Proper)
            {
                State.CurrentFormat = WordFormat.None;
            }
            else
            {
                State.CurrentFormat = WordFormat.Proper;
            }
        }

        private bool DoFrequency()
        {
            int oldPos = State.ReadPos;
            int percentIndex = State.Reader.Find('%', State.ReadPos);
            int nextSpace = State.Reader.Find(' ', State.ReadPos);
            if (percentIndex > -1 && (percentIndex < nextSpace || nextSpace == -1))
            {
                State.ReadPos--; // Revert reading of first digit
                string percentStr = State.Reader.ReadString(percentIndex - State.ReadPos);
                int percent;
                if (!int.TryParse(percentStr, out percent))
                {
                    State.ReadPos = oldPos;
                }
                else
                {
                    if (percent > 100)
                    {
                        percent = 100;
                    }
                    else if (percent <= 0)
                    {
                        Error("0% frequency indicator detected. Why is this here?", State.Reader);
                        return false;
                    }

                    State.ReadPos++; // Skip past '%'

                    if ((char)State.Reader.PeekChar() == '[')
                    {
                        State.ReadPos++; // Skip past '['
                        int closure = State.Reader.Source.FindClosingSquareBracket(State.ReadPos);

                        if (closure < 0)
                        {
                            Error("Missing closing bracket in frequency indicator.", State.Reader);
                            return false;
                        }

                        if (State.RNG.Next(0, 101) > percent)
                        {
                            State.ReadPos = closure;
                        }

                        return true;
                    }
                }
            }
            DoBuffer();
            return true;
        }

        private bool DoOutputGroupStart()
        {
            int beginIndex = State.Reader.Find(':', State.ReadPos);
            if (beginIndex == -1)
            {
                Error("Couldn't find output group name terminator.", State.Reader);
                return false;
            }

            string groupName = State.Reader.ReadTo(beginIndex).ToLower();
            State.Reader.ReadChar(); // skip ':'

            int endIndex = State.Reader.Source.FindClosingTriangleBracket(State.ReadPos);
            if (endIndex == -1)
            {
                Error(String.Format("Closing bracket couldn't be found for output group '{0}'.", groupName), State.Reader);
                return false;
            }

            State.OutputGroups.Add(new OutputGroup(groupName, beginIndex + 1, endIndex));
            return true;
        }

        private bool DoOutputGroupEnd()
        {
            var group = State.OutputGroups.LastOrDefault(gi => gi.Name.ToLower() != "main");
            if (group == null)
            {
                Error("Output group closure found with no associated instance.", State.Reader);
                return false;
            }
            State.OutputGroups.RemoveAt(State.OutputGroups.Count - 1);
            return true;
        }

        private void DoNonBrackets()
        {
            if (State.PrevChar == ' ' && State.CurrentChar == 'a' && !char.IsLetterOrDigit((char)State.Reader.PeekChar())) // YES! YES!
            {
                State.AnIndex = State.Output[State.OutputGroups[State.OutputGroups.Count - 1].Name].Length + 1;
                State.AnFormat = State.CurrentFormat;
            }

            if (State.CurrentFormat == WordFormat.AllCaps || (State.CurrentFormat == WordFormat.Proper && !Char.IsLetterOrDigit(State.PrevChar) && State.PrevChar.PermitsCap()))
            {
                State.WriteBuffer(State.CurrentChar.ToString().ToUpper());
            }
            else if (State.CurrentFormat == WordFormat.Capitalized)
            {
                State.WriteBuffer(State.CurrentChar.ToString().ToUpper());
                State.CurrentFormat = WordFormat.None;
            }
            else
            {
                State.WriteBuffer(State.CurrentChar);
            }
        }

        private bool DoRandomNumber()
        {
            string rnBody;
            int rnStart;
            if (State.Reader.ReadSquareBlock(out rnBody, out rnStart))
            {
                var m = Regex.Match(rnBody, @"(?<min>\d+)\-(?<max>\d+)", RegexOptions.ExplicitCapture);
                if (!m.Success) return false;

                if (State.CurrentFormat == WordFormat.Capitalized)
                {
                    State.CurrentFormat = WordFormat.None;
                }

                int min = Int32.Parse(m.Groups["min"].Value);
                int max = Int32.Parse(m.Groups["max"].Value);

                State.WriteBuffer(State.RNG.Next(Math.Min(min, max), Math.Max(min, max)).ToString());
            }
            return true;
        }

        private bool DoSelectorStart()
        {
            int end = State.Reader.Source.FindClosingCurlyBracket(State.ReadPos);
            if (end == -1)
            {
                Error("Incomplete curly brackets. Did you forget to close a selector?", State.Reader);
                return false;
            }
            int[] startIndices = State.Reader.Source.GetSelectorSubs(State.ReadPos);
            if (startIndices.Length < 2)
            {
                Error("Selector is empty or only has one option.", State.Reader);
                return false;
            }
            State.ActiveSelectors.Add(end + 1);
            State.SelectorUniformIDs.Add(State.CurrentUID);
            if (State.CurrentUID > -1)
            {
                ManRandom uniRand = new ManRandom(State.UniformSeedSalt + State.CurrentUID);
                State.ReadPos = startIndices[uniRand.Next(0, startIndices.Length)];
            }
            else
            {
                State.ReadPos = startIndices[State.RNG.Next(0, startIndices.Length)];
            }
            State.CurrentUID = -1;
            return true;
        }

        private bool DoSelectorItemEnd()
        {
            if (State.ActiveSelectors.Count == 0)
            {
                Error("Unexpected '/' found in pattern.", State.Reader);
                return false;
            }
            State.ReadPos = State.ActiveSelectors[State.ActiveSelectors.Count - 1];
            State.ActiveSelectors.RemoveAt(State.ActiveSelectors.Count - 1);
            State.SelectorUniformIDs.RemoveAt(State.SelectorUniformIDs.Count - 1);
            return true;
        }

        private bool DoSelectorEnd()
        {
            if (State.ActiveSelectors.Count == 0)
            {
                Error("Unexpected '}' found in pattern.", State.Reader);
                return false;
            }

            State.ActiveSelectors.RemoveAt(State.ActiveSelectors.Count - 1);
            State.SelectorUniformIDs.RemoveAt(State.SelectorUniformIDs.Count - 1);
            return true;
        }

        private bool DoSelectorUniform()
        {
            int bracketIndex = State.Reader.Find("{", State.ReadPos);
            if (bracketIndex <= State.ReadPos)
            {
                Error("Uniform operator could not find a selector to associate with.", State.Reader);
                return false;
            }
            string strUID = State.Reader.ReadString(bracketIndex - State.ReadPos);
            int uid;
            if (!int.TryParse(strUID, out uid))
            {
                Error("Uniform ID was not a number.", State.Reader);
                return false;
            }
            else if (uid < 0)
            {
                Error("Uniform ID's cannot be negative.", State.Reader);
                return false;
            }
            State.CurrentUID = uid;
            return true;
        }

        private bool DoRepeaterStart()
        {
            // iteration range
            if (State.Reader.PeekChar() != '[')
            {
                Error("Repeater iterations parameter did not have an opening bracket.", State.Reader);
                return false;
            }
            State.Reader.ReadChar(); // skip [
            int rightRangeBracketIndex = State.Reader.Source.FindClosingSquareBracket(State.ReadPos);
            if (rightRangeBracketIndex < 0)
            {
                Error("Repeater iterations parameter did not have a closing bracket.", State.Reader);
                return false;
            }
            string strRangeParameter = State.Reader.ReadString(rightRangeBracketIndex - State.ReadPos).Trim();
            State.Reader.ReadChar(); // skip ]
            int constantParam = 0;
            int min = 0;
            int max = 0;
            if (!int.TryParse(strRangeParameter, out constantParam))
            {
                string[] parts = strRangeParameter.Split(new char[] { '-' });
                if (parts.Length != 2)
                {
                    Error("Repeater range parameter must be a pair of two numbers.", State.Reader);
                    return false;
                }
                if (!int.TryParse(parts[0], out min) || !int.TryParse(parts[1], out max))
                {
                    Error("Repeater range parameter did not contain valid numbers.", State.Reader);
                    return false;
                }
                if (min > max || min == 0 || max == 0)
                {
                    Error("Repeater range must be greater than zero, and max > min.", State.Reader);
                    return false;
                }
                constantParam = State.RNG.Next(min, max);
            }
            // separator
            if (State.Reader.ReadChar() != '[')
            {
                Error("Repeater separator parameter did not have an opening bracket.", State.Reader);
                return false;
            }
            int sepIndex = State.ReadPos;
            int rightSepBracketIndex = State.Reader.Source.FindClosingSquareBracket(State.ReadPos);
            if (rightSepBracketIndex < 0)
            {
                Error("Repeater separator parameter did not have a closing bracket.", State.Reader);
            }
            string strSepParameter = State.Reader.ReadString(rightSepBracketIndex - State.ReadPos);
            int sepEnd = State.ReadPos;
            State.Reader.ReadChar(); // skip ]

            // content
            if (State.Reader.ReadChar() != '[')
            {
                Error("Repeater content parameter did not have an opening bracket.", State.Reader);
                return false;
            }
            int rightContentBracketIndex = State.Reader.Source.FindClosingSquareBracket(State.ReadPos);
            if (rightSepBracketIndex < 0)
            {
                Error("Repeater content parameter did not have a closing bracket.", State.Reader);
            }
            int pStart = State.ReadPos;

            State.Reader.ReadString(rightContentBracketIndex - State.ReadPos);

            int pEnd = State.ReadPos;

            State.Repeaters.Add(new RepeaterInstance(pStart, pEnd, sepIndex, sepEnd, strSepParameter, constantParam));

            State.ReadPos = pStart;

            SetLocalFlag("odd_" + State.Repeaters.Count);
            SetLocalFlag("first_" + State.Repeaters.Count);
            return true;
        }

        private bool DoRepeaterEnd()
        {
            int repeaterCount = State.Repeaters.Count;
            int last = repeaterCount - 1;
            RepeaterInstance rep = State.Repeaters[last];
            if (State.ReadPos - 1 != rep.ContentEndIndex && State.ReadPos - 1 != rep.SeparatorEndIndex)
            {
                return true;
            }

            UnsetLocalFlag("last_" + repeaterCount);
            UnsetLocalFlag("first_" + repeaterCount);
            if (State.Repeaters[last].Iterations == 0)
            {
                SetLocalFlag("first_" + repeaterCount);
            }
            else if (State.Repeaters[last].Iterations == State.Repeaters[last].MaxIterations - 1)
            {
                SetLocalFlag("last_" + repeaterCount);
            }

            if (rep.OnSeparator) // Currently writing separator?
            {
                rep.OnSeparator = false;
                State.ReadPos = rep.ContentStartIndex;
            }
            else // Currently writing content?
            {
                if (State.Repeaters[last].Elapse())
                {
                    UnsetLocalFlag("odd_" + repeaterCount);
                    UnsetLocalFlag("even_" + repeaterCount);
                    State.Repeaters.RemoveAt(last); // Remove the last repeater if it's finished
                    return true;
                }
                else
                {
                    if ((State.Repeaters[last].Iterations + 1) % 2 == 0)
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
                    State.ReadPos = rep.SeparatorStartIndex; // Add separator if not
                }
            }
            return false;
        }

        private bool DoWordCall()
        {
            var match = regWordCallModern.Match(State.Reader.Source, State.ReadPos);
            int endIndex = match.Groups["end"].Index;
            if (!(match.Success && match.Index == State.ReadPos))
            {
                match = regWordCallLegacy.Match(State.Reader.Source, State.ReadPos); // Fall back to legacy and re-test
                endIndex = match.Index + match.Length;
                if (!(match.Success && match.Index == State.ReadPos))
                {
                    Warning("Invalid word call", State.Reader);
                    return false;
                }
            }

            var groups = match.Groups;
            string className = groups["class"].Value.Trim();
            string subtype = groups["subtype"].Value.Trim();
            string carrier = groups["carrier"].Value;
            char symbol = groups["symbol"].Value[0];

            State.ReadPos = endIndex;

            if (!wordBank.ContainsKey(symbol)) // Make sure the symbol is registered
            {
                Warning("Word symbol not found: '" + symbol.ToString() + "'", State.Reader);
            }
            else if (carrier != "")
            {
                string carrierKey = String.Format("{0}:{1}", className, symbol);
                Dictionary<string, int> cd;
                if (!State.Carriers.TryGetValue(carrierKey, out cd))
                {
                    cd = new Dictionary<string, int>();
                    cd.Add(carrier, wordBank[symbol].GetRandomIndex(State.RNG, className));
                    State.Carriers.Add(carrierKey, cd);
                }
                else if (!cd.ContainsKey(carrier))
                {
                    cd.Add(carrier, wordBank[symbol].GetRandomIndex(State.RNG, className));
                }
                State.WriteBuffer(wordBank[symbol].GetWordByIndex(State.Carriers[carrierKey][carrier], subtype, State.CurrentFormat));
            }
            else
            {
                if (className.Contains(","))
                {
                    string[] mcNames = className.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (mcNames.Length < 2)
                    {
                        Error("A multi-class expression must include more than one class name in its parameters.", State.Reader);
                        return false;
                    }
                    for (int i = 0; i < mcNames.Length; i++)
                    {
                        mcNames[i] = mcNames[i].Trim(); // this is to get rid of spaces between the class names
                    }
                    for (int i = 0; i < mcNames.Length; i++)
                    {
                        if (!ClassExists(symbol, mcNames[i]))
                        {
                            Error("Bad multiclass", State.Reader);
                            return false;
                        }
                    }
                    State.WriteBuffer(wordBank[symbol].GetRandomWordMultiClass(State.RNG, subtype, State.CurrentFormat, mcNames));
                }
                else if (!ClassExists(symbol, className))
                {
                    Warning("Class not found: " + symbol.ToString() + " -> " + className, State.Reader);
                }
                else
                {
                    int index = wordBank[symbol].GetRandomIndex(State.RNG, className);
                    State.WriteBuffer(wordBank[symbol].GetWordByIndex(index, subtype, State.CurrentFormat));
                }
            }
            State.FormatBuffer(State.CurrentFormat);

            if (State.AnIndex > -1 && State.Buffer.ToString().StartsWithVowel())
            {
                if (State.AnFormat == WordFormat.AllCaps)
                {
                    State.Output[State.OutputGroups[State.OutputGroups.Count - 1].Name].Insert(State.AnIndex, "N");
                }
                else
                {
                    State.Output[State.OutputGroups[State.OutputGroups.Count - 1].Name].Insert(State.AnIndex, "n");
                }
            }

            if (State.CurrentFormat == WordFormat.Capitalized)
            {
                State.CurrentFormat = WordFormat.None;
            }

            State.AnIndex = -1;
            State.AnFormat = WordFormat.None;
            return true;
        }

        private bool DoFunction()
        {
            int leftBracket = State.Reader.Find("[", State.ReadPos);
            if (leftBracket < 0)
            {
                Error("Missing '[' on function call.", State.Reader);
                return false;
            }
            string func = State.Reader.ReadTo(leftBracket).ToLower(),
                param1 = "",
                param2 = "";

            int
                param1start,
                param2start;

            if ((func = func.ToLower()).Contains(' '))
            {
                Error("Function names cannot contain spaces.", State.Reader);
                return false;
            }

            if (!State.Reader.ReadSquareBlock(out param1, out param1start))
            {
                Error("Invalid parameter block.", State.Reader);
                return false;
            }
            State.Reader.ReadSquareBlock(out param2, out param2start);

            if (func == "ls")
            {
                SetLocalFlag(param1);
            }
            else if (func == "lu")
            {
                UnsetLocalFlag(param1);
            }
            else if (func == "l?")
            {
                if (CheckLocalFlag(param1))
                {
                    State.ReadPos = param2start;
                }
            }
            else if (func == "l!")
            {
                if (!CheckLocalFlag(param1))
                {
                    State.ReadPos = param2start;
                }
            }
            else if (func == "gs")
            {
                SetGlobalFlag(param1);
            }
            else if (func == "gu")
            {
                UnsetGlobalFlag(param1);
            }
            else if (func == "g?")
            {
                if (CheckGlobalFlag(param1))
                {
                    State.ReadPos = param2start;
                }
            }
            else if (func == "g!")
            {
                if (!CheckGlobalFlag(param1))
                {
                    State.ReadPos = param2start;
                }
            }
            else
            {
                Error("Unrecognized flag function.", State.Reader);
                return false;
            }
            return true;
        }

        private void DoBuffer()
        {
            int groupCount = State.OutputGroups.Count;
            var currentGroup = State.OutputGroups[groupCount - 1];
            var gVis = currentGroup.Visibility;

            switch (gVis)
            {
                case GroupVisibility.Public:
                    {
                        foreach (var group in State.OutputGroups)
                        {
                            State.Output[group.Name].Append(State.BufferText);
                        }
                    }
                    break;
                case GroupVisibility.Internal:
                    {
                        OutputGroup group = null;
                        for (int i = 0; i < State.OutputGroups.Count; i++)
                        {
                            group = State.OutputGroups[groupCount - (i + 1)];
                            if (group.Visibility != GroupVisibility.Internal) break;
                            State.Output[group.Name].Append(State.BufferText);
                        }
                    }
                    break;
                case GroupVisibility.Private:
                    {
                        State.Output[currentGroup.Name].Append(State.BufferText);
                    }
                    break;
            }

            State.ClearBuffer();
        }

        private string TranslateDefs(string rawPattern, string last)
        {
            CharReader pp = new CharReader(rawPattern, 0);
            string pattern = "";
            char c = '\0';
            char prev = '\0';
            if (rawPattern.Contains("="))
            {
                while (!pp.EndOfString)
                {
                    prev = c;
                    c = pp.ReadChar();
                    if (c == '=' && prev != '\\')
                    {
                        string name;
                        int start;
                        if (!pp.ReadSquareBlock(out name, out start))
                        {
                            Error("Bad def call", pp);
                            return "";
                        }

                        if (name == "")
                        {
                            Error("Empty def.", pp);
                            return "";
                        }
                        if (name.Contains("+"))
                        {
                            string[] macroParts = name.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                            if (macroParts.Length == 0)
                            {
                                Error("Empty def.", pp);
                                return "";
                            }
                            for (int i = 0; i < macroParts.Length; i++)
                            {
                                string macroPart = macroParts[i].Trim();
                                if (!defBank.ContainsKey(macroPart))
                                {
                                    Error("Def \"" + macroPart + "\" doesn't exist.", pp);
                                    return "";
                                }
                                pattern += TranslateDefs(defBank[macroPart].Body, last);
                            }
                        }
                        else
                        {
                            if (name == last)
                            {
                                Error("Def error: Cannot create a definition that references itself. (" + name + ")", pp);
                                return "";
                            }
                            if (!defBank.ContainsKey(name))
                            {
                                Error("Def \"" + name + "\" doesn't exist.", pp);
                                return "";
                            }
                            var def = defBank[name];
                            if (def.Type == DefinitionType.Macro)
                            {
                                pattern += TranslateDefs(def.Body, name);
                            }
                            else
                            {
                                pattern += TranslateDefs(def.State, name);
                            }
                            
                        }
                    }
                    else
                    {
                        pattern += c;
                    }
                }
            }
            else
            {
                pattern = rawPattern;
            }
            return pattern;
        }
    }
}
