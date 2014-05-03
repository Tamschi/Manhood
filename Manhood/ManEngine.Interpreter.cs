using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Manhood
{
    public partial class ManEngine
    {
        // Word call
        // +s[subtype of class for carrier]
        const string PatWordCallModern = @"((?<symbol>\w)(?:\[\s*(?<subtype>\w+)?(\s*of\s*(?<class>[\w\&\,]+))?(\s*for\s*((?<carrier>\w+)|(?:\"")(?<carrier>[\w\s]+)(?:\"")))?\s*\])?)(?<end>$|[^\<][^\>]|.)";

        static readonly Regex RegWordCallModern = new Regex(PatWordCallModern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
        
        // Selector
        // *x.seed
        const string PatSelector = @"((?<type>\w+)\.(?<seed>\w+))?(?<start>\{)";

        static readonly Regex RegSelector = new Regex(PatSelector, RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        // Parameterized macro body
        // MacroName [param1] [param2] ...
        const string PatMacroCall = @"(?<name>[\w_\-]+)(?<parameters>\s*\[.*)?";

        static readonly Regex RegMacroCall = new Regex(PatMacroCall, RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        const string PatComments = @"(//([^\r\n]+)(?:\r\n|\n|$)|/\*.*\*/|/\*.*)";

        static readonly Regex RegComments = new Regex(PatComments, RegexOptions.Singleline);

        private void InterpretAndCheck(ManRandom rand, OutputGroup output, string pattern)
        {
            var state = new EngineState();
            _activeStates.Add(state);
            Interpret(rand, output, pattern, state);
            _activeStates.Remove(state);
            CheckForErrors(state);
        }

        private void Interpret(ManRandom rand, OutputGroup output, string pattern, EngineState state)
        {
            pattern = RegComments.Replace(pattern, "");
            state.Errors = new ErrorLog(pattern, "");
            pattern = Regex.Replace(pattern, @"[\r\n\t]", "", RegexOptions.ExplicitCapture);

            var patExp = TranslateDefs(state, pattern);
            state.Errors.PatternExpanded = patExp;
            state.Start(rand, output, patExp);

            while (!state.Reader.EndOfString) // Read through pattern until we reach the end
            {
                state.ReadChar();
                if (state.CurrentChar == '\\' && !state.Reader.EndOfString) // Escape character
                {
                    state.WriteBuffer(Escape.GetChar(state.ReadChar()));
                    DoBuffer(state);
                    continue;
                }

                switch (state.CurrentChar)
                {
                    case '<':
                        if (!DoOutputStart(state)) return;
                        break;
                    case '>':
                        if (!DoOutputEnd(state)) return;
                        break;
                    case '*':
                    case '{':
                        if (!DoSelectorStart(state)) return;
                        break;
                    case '}':
                        if (DoSelectorEnd(state))
                        {
                            continue;
                        }
                        return;
                    case '/':
                        if (DoSelectorItemEnd(state))
                        {
                            continue;
                        }
                        return;
                    case '^':
                        if (!DoRepeaterStart(state)) return;
                        break;
                    case ']':
                        if (state.Repeaters.Count > 0)
                        {
                            if (DoRepeaterEnd(state)) continue;
                        }
                        break;
                    case '$':
                        if (!DoFunction(state)) return;
                        break;
                    case '+':
                        if (!DoWordCall(state)) return;
                        break;
                    case '|':
                        state.WriteBuffer("\r\n");
                        break;
                    case '~':
                        DoCapitalize(state);
                        break;
                    case '@':
                        DoCapsLock(state);
                        break;
                    case '#':
                        if (!DoRandomNumber(state)) continue;
                        break;
                    default:
                    {
                        if (Char.IsNumber(state.CurrentChar)) // Check if frequency indicator is here. Example: 40%{ +n[plural] are +A. }
                        {
                            if (DoFrequency(state))
                            {
                                continue;
                            }
                            return;
                        }
                        
                        if (!"{}[]<>".Contains(state.CurrentChar)) // Covers all other characters except brackets
                        {
                            DoNonBrackets(state);
                        }
                    }
                        break;
                }

                DoBuffer(state);
            }
            _flagsLocal.Clear();
        }

        private static void DoCapsLock(EngineState state)
        {
            state.CurrentFormat = state.CurrentFormat == WordCase.AllCaps ? WordCase.None : WordCase.AllCaps;
        }

        private void DoCapitalize(EngineState state)
        {
            if (state.Reader.PeekChar() == '~')
            {
                state.Reader.ReadChar();
                state.CurrentFormat = WordCase.Capitalized;
            }
            else if (state.CurrentFormat == WordCase.Proper)
            {
                state.CurrentFormat = WordCase.None;
            }
            else
            {
                state.CurrentFormat = WordCase.Proper;
            }
        }

        private bool DoFrequency(EngineState state)
        {
            int oldPos = state.ReadPos;
            int percentIndex = state.Reader.Find('%', state.ReadPos);
            int nextSpace = state.Reader.Find(' ', state.ReadPos);
            if (percentIndex > -1 && (percentIndex < nextSpace || nextSpace == -1))
            {
                state.ReadPos--; // Revert reading of first digit
                var percentStr = state.Reader.ReadTo(percentIndex);
                int percent;
                if (!int.TryParse(percentStr, out percent))
                {
                    state.ReadPos = oldPos;
                }
                else
                {
                    if (percent > 100)
                    {
                        percent = 100;
                    }
                    else if (percent <= 0)
                    {
                        Error(state, "0% frequency indicator detected. Why is this here?");
                        return false;
                    }

                    state.ReadPos++; // Skip past '%'

                    if ((char)state.Reader.PeekChar() == '[')
                    {
                        state.ReadPos++; // Skip past '['
                        int closure = state.Reader.Source.FindClosingSquareBracket(state.ReadPos);

                        if (closure < 0)
                        {
                            Error(state, "Missing closing bracket in frequency indicator.");
                            return false;
                        }

                        if (state.RNG.Next(0, 101) > percent)
                        {
                            state.ReadPos = closure;
                        }

                        return true;
                    }
                }
            }
            DoNonBrackets(state);
            DoBuffer(state);
            return true;
        }

        private bool DoOutputStart(EngineState state)
        {
            int beginIndex = state.Reader.Find(':', state.ReadPos);
            if (beginIndex == -1)
            {
                Error(state, "Couldn't find output name terminator.");
                return false;
            }

            var groupName = state.Reader.ReadTo(beginIndex).ToLower();
            state.Reader.ReadChar(); // skip ':'

            int endIndex = state.Reader.Source.FindClosingTriangleBracket(state.ReadPos);
            if (endIndex == -1)
            {
                Error(state, String.Format("Closing bracket couldn't be found for output '{0}'.", groupName));
                return false;
            }

            state.ActiveOutputs.Add(new Output(groupName, beginIndex + 1, endIndex));
            return true;
        }

        private bool DoOutputEnd(EngineState state)
        {
            var group = state.ActiveOutputs.LastOrDefault(gi => gi.Name.ToLower() != "main");
            if (group == null)
            {
                Error(state, "Output closure found with no associated instance.");
                return false;
            }
            state.ActiveOutputs.RemoveAt(state.ActiveOutputs.Count - 1);
            return true;
        }

        private void DoNonBrackets(EngineState state)
        {
            if (state.PrevChar == ' ' && state.CurrentChar == 'a' && !char.IsLetterOrDigit((char)state.Reader.PeekChar())) // YES! YES!
            {
                state.AnIndex = state.Output[state.ActiveOutputs[state.ActiveOutputs.Count - 1].Name].Length + 1;
                state.AnFormat = state.CurrentFormat;
            }

            if (state.CurrentFormat == WordCase.AllCaps || (state.CurrentFormat == WordCase.Proper && !Char.IsLetterOrDigit(state.PrevChar) && state.PrevChar.PermitsCap()))
            {
                state.WriteBuffer(state.CurrentChar.ToString(CultureInfo.InvariantCulture).ToUpper());
            }
            else if (state.CurrentFormat == WordCase.Capitalized)
            {
                state.WriteBuffer(state.CurrentChar.ToString(CultureInfo.InvariantCulture).ToUpper());
                state.CurrentFormat = WordCase.None;
            }
            else
            {
                state.WriteBuffer(state.CurrentChar);
            }
        }

        private bool DoRandomNumber(EngineState state)
        {
            string rnBody;
            int rnStart;
            if (!state.Reader.ReadSquareBlock(out rnBody, out rnStart)) return true;
            var m = Regex.Match(rnBody, @"(?<min>\d+)\-(?<max>\d+)", RegexOptions.ExplicitCapture);
            if (!m.Success) return false;

            if (state.CurrentFormat == WordCase.Capitalized)
            {
                state.CurrentFormat = WordCase.None;
            }

            var min = Int32.Parse(m.Groups["min"].Value);
            var max = Int32.Parse(m.Groups["max"].Value) + 1;

            state.WriteBuffer(state.RNG.Next(Math.Min(min, max), Math.Max(min, max)).ToString(CultureInfo.InvariantCulture));
            return true;
        }

        private bool DoSelectorItemEnd(EngineState state)
        {
            if (state.CurrentSelector == null)
            {
                Error(state, "Unexpected '/' found in pattern.");
                return false;
            }
            state.ReadPos = state.CurrentSelector.End + 1;
            state.Selectors.RemoveAt(state.Selectors.Count - 1);
            return true;
        }

        private bool DoSelectorEnd(EngineState state)
        {
            if (state.CurrentSelector == null)
            {
                Error(state, "Unexpected '}' found in pattern.");
                return false;
            }

            state.Selectors.RemoveAt(state.Selectors.Count - 1);
            return true;
        }

        private bool DoRepeaterStart(EngineState state)
        {
            // iteration range
            if (state.Reader.PeekChar() != '[')
            {
                Error(state, "Repeater iterations parameter did not have an opening bracket.");
                return false;
            }
            state.Reader.ReadChar(); // skip [
            var rightRangeBracketIndex = state.Reader.Source.FindClosingSquareBracket(state.ReadPos);
            if (rightRangeBracketIndex < 0)
            {
                Error(state, "Repeater iterations parameter did not have a closing bracket.");
                return false;
            }
            var strRangeParameter = state.Reader.ReadString(rightRangeBracketIndex - state.ReadPos).Trim();
            state.Reader.ReadChar(); // skip ]
            int constantParam;
            if (!int.TryParse(strRangeParameter, out constantParam))
            {
                var parts = strRangeParameter.Split(new[] { '-' });
                if (parts.Length != 2)
                {
                    Error(state, "Repeater range parameter must be a pair of two numbers.");
                    return false;
                }
                int max;
                int min;
                if (!int.TryParse(parts[0], out min) || !int.TryParse(parts[1], out max))
                {
                    Error(state, "Repeater range parameter did not contain valid numbers.");
                    return false;
                }
                if (min > max || min == 0 || max == 0)
                {
                    Error(state, "Repeater range must be greater than zero, and max > min.");
                    return false;
                }
                constantParam = state.RNG.Next(min, max + 1);
            }
            // separator
            if (state.Reader.ReadChar() != '[')
            {
                Error(state, "Repeater separator parameter did not have an opening bracket.");
                return false;
            }
            var sepIndex = state.ReadPos;
            var rightSepBracketIndex = state.Reader.Source.FindClosingSquareBracket(state.ReadPos);
            if (rightSepBracketIndex < 0)
            {
                Error(state, "Repeater separator parameter did not have a closing bracket.");
            }
            var strSepParameter = state.Reader.ReadString(rightSepBracketIndex - state.ReadPos);
            var sepEnd = state.ReadPos;
            state.Reader.ReadChar(); // skip ]

            // content
            if (state.Reader.ReadChar() != '[')
            {
                Error(state, "Repeater content parameter did not have an opening bracket.");
                return false;
            }
            var rightContentBracketIndex = state.Reader.Source.FindClosingSquareBracket(state.ReadPos);
            if (rightSepBracketIndex < 0)
            {
                Error(state, "Repeater content parameter did not have a closing bracket.");
            }
            var pStart = state.ReadPos;

            state.Reader.ReadString(rightContentBracketIndex - state.ReadPos);

            var pEnd = state.ReadPos;

            state.Repeaters.Add(new RepeaterInstance(pStart, pEnd, sepIndex, sepEnd, strSepParameter, constantParam));

            state.ReadPos = pStart;

            SetLocalFlag("odd_" + state.Repeaters.Count);
            SetLocalFlag("first_" + state.Repeaters.Count);
            return true;
        }

        private bool DoRepeaterEnd(EngineState state)
        {
            var repeaterCount = state.Repeaters.Count;
            var last = repeaterCount - 1;
            var rep = state.Repeaters[last];
            if (state.ReadPos - 1 != rep.ContentEndIndex && state.ReadPos - 1 != rep.SeparatorEndIndex)
            {
                return true;
            }

            UnsetLocalFlag("last_" + repeaterCount);
            UnsetLocalFlag("first_" + repeaterCount);
            if (state.Repeaters[last].Iterations == 0)
            {
                SetLocalFlag("first_" + repeaterCount);
            }
            else if (state.Repeaters[last].Iterations == state.Repeaters[last].MaxIterations - 1)
            {
                SetLocalFlag("last_" + repeaterCount);
            }

            if (rep.OnSeparator) // Currently writing separator?
            {
                rep.OnSeparator = false;
                state.ReadPos = rep.ContentStartIndex;
            }
            else // Currently writing content?
            {
                if (state.Repeaters[last].Elapse())
                {
                    UnsetLocalFlag("odd_" + repeaterCount);
                    UnsetLocalFlag("even_" + repeaterCount);
                    state.Repeaters.RemoveAt(last); // Remove the last repeater if it's finished
                    return true;
                }
                if ((state.Repeaters[last].Iterations + 1) % 2 == 0)
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
                state.ReadPos = rep.SeparatorStartIndex; // Add separator if not
            }
            return false;
        }

        private bool DoWordCall(EngineState state)
        {
            var match = RegWordCallModern.Match(state.Reader.Source, state.ReadPos);
            int endIndex = match.Groups["end"].Index;
            if (!(match.Success && match.Index == state.ReadPos))
            {
                Error(state, "Invalid word call");
                return false;
            }

            var groups = match.Groups;
            var className = groups["class"].Value.Trim();
            var subtype = groups["subtype"].Value.Trim();
            var carrier = groups["carrier"].Value;
            var symbol = groups["symbol"].Value[0];

            state.ReadPos = endIndex;

            if (!_wordBank.ContainsKey(symbol)) // Make sure the symbol is registered
            {
                Error(state, "Word symbol not found: '" + symbol + "'");
            }
            else if (carrier != "")
            {
                var carrierKey = String.Format("{0}:{1}", className, symbol);
                Dictionary<string, int> cd;
                if (!state.Carriers.TryGetValue(carrierKey, out cd))
                {
                    cd = new Dictionary<string, int>
                    {
                        {carrier, _wordBank[symbol].GetRandomIndex(state.RNG, className)}
                    };
                    state.Carriers.Add(carrierKey, cd);
                }
                else if (!cd.ContainsKey(carrier))
                {
                    cd.Add(carrier, _wordBank[symbol].GetRandomIndex(state.RNG, className));
                }
                state.WriteBuffer(_wordBank[symbol].GetWordByIndex(state.Carriers[carrierKey][carrier], subtype, state.CurrentFormat));
            }
            else
            {
                if (className.Contains(","))
                {
                    var mcNames = className.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (mcNames.Length < 2)
                    {
                        Error(state, "A multi-class expression must include more than one class name in its parameters.");
                        return false;
                    }
                    for (int i = 0; i < mcNames.Length; i++)
                    {
                        mcNames[i] = mcNames[i].Trim(); // this is to get rid of spaces between the class names
                    }
                    if (mcNames.Any(t => !ClassExists(symbol, t)))
                    {
                        Error(state, "Bad multiclass");
                        return false;
                    }
                    state.WriteBuffer(_wordBank[symbol].GetRandomWordMultiClass(state.RNG, subtype, state.CurrentFormat, mcNames));
                }
                else if (!ClassExists(symbol, className))
                {
                    Error(state, "Class not found: " + symbol + " -> " + className);
                }
                else
                {
                    int index = _wordBank[symbol].GetRandomIndex(state.RNG, className);
                    state.WriteBuffer(_wordBank[symbol].GetWordByIndex(index, subtype, state.CurrentFormat));
                }
            }
            state.FormatBuffer(state.CurrentFormat);

            if (state.AnIndex > -1 && state.Buffer.ToString().StartsWithVowel())
            {
                state.Output[state.ActiveOutputs[state.ActiveOutputs.Count - 1].Name]
                    .Insert(state.AnIndex, state.AnFormat == WordCase.AllCaps ? "N" : "n");
            }

            if (state.CurrentFormat == WordCase.Capitalized)
            {
                state.CurrentFormat = WordCase.None;
            }

            state.AnIndex = -1;
            state.AnFormat = WordCase.None;
            return true;
        }

        private bool DoFunction(EngineState state)
        {
            int leftBracket = state.Reader.Find("[", state.ReadPos);
            if (leftBracket < 0)
            {
                Error(state, "Missing '[' on function call.");
                return false;
            }
            string func = state.Reader.ReadTo(leftBracket).ToLower(),
                param1,
                param2;

            int
                param1Start,
                param2Start;

            if ((func = func.ToLower()).Contains(' '))
            {
                Error(state, "Function names cannot contain spaces.");
                return false;
            }

            if (!state.Reader.ReadSquareBlock(out param1, out param1Start))
            {
                Error(state, "Invalid parameter block.");
                return false;
            }
            state.Reader.ReadSquareBlock(out param2, out param2Start);

            switch (func)
            {
                case "ls":
                    SetLocalFlag(param1);
                    break;
                case "lu":
                    UnsetLocalFlag(param1);
                    break;
                case "l?":
                    if (CheckLocalFlag(param1))
                    {
                        state.ReadPos = param2Start;
                    }
                    break;
                case "l!":
                    if (!CheckLocalFlag(param1))
                    {
                        state.ReadPos = param2Start;
                    }
                    break;
                case "gs":
                    SetGlobalFlag(param1);
                    break;
                case "gu":
                    UnsetGlobalFlag(param1);
                    break;
                case "g?":
                    if (CheckGlobalFlag(param1))
                    {
                        state.ReadPos = param2Start;
                    }
                    break;
                case "g!":
                    if (!CheckGlobalFlag(param1))
                    {
                        state.ReadPos = param2Start;
                    }
                    break;
                default:
                    if (_customFuncs.ContainsKey(func))
                    {
                        try
                        {
                            state.WriteBuffer(_customFuncs[func](state.RNG));
                        }
                        catch(Exception ex)
                        {
                            Error(state, "Custom function '" + func + "' threw an exception: " + ex);
                        }
                    }
                    else
                    {
                        Error(state, "Unrecognized flag function.");
                        return false;
                    }
                    break;
            }
            return true;
        }

        private static void DoBuffer(EngineState state)
        {
            var groupCount = state.ActiveOutputs.Count;
            var currentGroup = state.ActiveOutputs[groupCount - 1];
            var gVis = currentGroup.Visibility;

            switch (gVis)
            {
                case OutputVisibility.Public:
                    {
                        foreach (var group in state.ActiveOutputs)
                        {
                            state.Output[group.Name].Append(state.BufferText);
                        }
                    }
                    break;
                case OutputVisibility.Internal:
                    {
                        for (var i = 0; i < state.ActiveOutputs.Count; i++)
                        {
                            var group = state.ActiveOutputs[groupCount - (i + 1)];
                            if (group.Visibility != OutputVisibility.Internal) break;
                            state.Output[group.Name].Append(state.BufferText);
                        }
                    }
                    break;
                case OutputVisibility.Private:
                    {
                        state.Output[currentGroup.Name].Append(state.BufferText);
                    }
                    break;
            }

            state.ClearBuffer();
        }

        private string TranslateDefs(EngineState state, string rawPattern)
        {
            rawPattern = Regex.Replace(rawPattern, @"[\r\n\t]", "");
            var pp = new CharReader(rawPattern, 0);
            var pattern = "";
            var c = '\0';
            char prev;
            if (rawPattern.Contains("="))
            {
                while (!pp.EndOfString)
                {
                    prev = c;
                    c = pp.ReadChar();
                    if (c == '=' && prev != '\\')
                    {
                        string macroCall;                        
                        int start;
                        if (!pp.ReadSquareBlock(out macroCall, out start))
                        {
                            PreError(state, pp.Position, "Bad def call");
                            return "";
                        }

                        if (macroCall == "")
                        {
                            PreError(state, start, "Empty def.");
                            return "";
                        }

                        var macroParts = RegMacroCall.Match(macroCall);
                        if (macroParts.Groups.Count == 0 || !macroParts.Success)
                        {
                            PreError(state, start, "Invalid or empty def.");
                            return "";
                        }

                        var macroName = macroParts.Groups["name"].Value;
                        
                        if (!_defBank.ContainsKey(macroName))
                        {
                            PreError(state, start, "Def \"" + macroName + "\" doesn't exist.");
                            return "";
                        }
                        
                        var def = _defBank[macroName];
                        var macroBody = def.Body;
                        
                        if (def.Parameters.Count > 0 && def.Type == DefinitionType.Macro)
                        {
                            if (macroParts.Length == 1)
                            {
                                PreError(state, pp.Position, "Def error: This macro requires parameters, but none were specified.");
                                return "";
                            }
                            List<string> macroParams;
                            if (!macroParts.Groups["parameters"].Value.ParseParameterList(out macroParams))
                            {
                                PreError(state, pp.Position, "Def error: Invalid parameter list.");
                                return "";
                            }
                            var pCount = macroParams.Count;
                            if (pCount != def.Parameters.Count)
                            {
                                PreError(state, pp.Position, "Def error: Parameter count mismatch. Expected " + def.Parameters.Count + ", got " + pCount + ".");
                                return "";
                            }

                            for(var i = 0; i < pCount; i++)
                            {
                                macroBody = Regex.Replace(macroBody, "\\&" + Regex.Escape(def.Parameters[i]) + "\\&", macroParams[i]);
                            }
                        }

                        if (def.Type == DefinitionType.Macro)
                        {
                            pattern += TranslateDefs(state, macroBody);
                        }
                        else
                        {
                            pattern += TranslateDefs(state, def.State);
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

        private static void Error(EngineState state, string problem)
        {
            state.Errors.AddFromState(ErrorType.Interpreter, state.Reader.Position, problem);
        }

        private static void PreError(EngineState state, int pos, string problem)
        {
            state.Errors.AddFromState(ErrorType.Preprocessor, pos, problem);
        }

        private static bool DoSelectorStart(EngineState state)
        {
            state.ReadPos--;
            var match = RegSelector.Match(state.Reader.Source, state.Reader.Position);
            if (!match.Success)
            {
                Error(state, "Invalid selector. Please check that your syntax is correct.");
                return false;
            }
            var start = match.Groups["start"].Index;
            var typestr = match.Groups["type"].Value;
            var seed = match.Groups["seed"].Value;
            state.ReadPos = start + 1;
            var end = state.Reader.Source.FindClosingCurlyBracket(state.ReadPos);

            SelectorType type;
            switch (typestr)
            {
                case "u":
                    type = SelectorType.Uniform;
                    break;
                case "d":
                    type = SelectorType.Deck;
                    break;
                case "cd":
                    type = SelectorType.CyclicDeck;
                    break;
                case "":
                    type = SelectorType.Random;
                    break;
                default:
                    Error(state, "Unrecognized selector type: " + typestr);
                    return false;
            }

            if (end == -1)
            {
                Error(state, "Selector is missing a closing bracket.");
                return false;
            }

            var startIndices = state.Reader.Source.GetSelectorSubs(state.ReadPos);
            if (startIndices.Length < 2)
            {
                Error(state, "Selector is empty or only has one option.");
                return false;
            }

            state.Selectors.Add(new SelectorInfo(state.RNG, start, end, startIndices.Length, seed, type));
            if (type != SelectorType.Deck && type != SelectorType.CyclicDeck)
            {
                state.ReadPos = startIndices[state.CurrentSelector.GetIndex()];
            }
            else
            {
                DeckSelectorState nrs;
                if (!state.NonRepeatingSelectorStates.TryGetValue(state.CurrentSelector.Hash, out nrs))
                {
                    state.NonRepeatingSelectorStates.Add(state.CurrentSelector.Hash, nrs = new DeckSelectorState(state.CurrentSelector.Hash + state.RNG.Seed, startIndices.Length, type == SelectorType.CyclicDeck));
                }
                state.ReadPos = startIndices[nrs.Next()];
            }
            return true;
        }
    }
}
