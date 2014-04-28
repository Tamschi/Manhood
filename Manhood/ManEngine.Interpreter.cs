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

        private void Interpret(ManRandom rand, OutputGroup output, string pattern)
        {
            pattern = RegComments.Replace(pattern, "");
            Middleman.State.Errors = new ErrorLog(pattern, "");
            pattern = Regex.Replace(pattern, @"[\r\n\t]", "", RegexOptions.ExplicitCapture);

            var patExp = TranslateDefs(pattern);
            Middleman.State.Errors.PatternExpanded = patExp;
            Middleman.State.Start(rand, output, patExp);

            while (!Middleman.State.Reader.EndOfString) // Read through pattern until we reach the end
            {
                Middleman.State.ReadChar();
                if (Middleman.State.CurrentChar == '\\' && !Middleman.State.Reader.EndOfString) // Escape character
                {
                    Middleman.State.WriteBuffer(Escape.GetChar(Middleman.State.ReadChar()));
                    DoBuffer();
                    continue;
                }

                switch (Middleman.State.CurrentChar)
                {
                    case '<':
                        if (!DoOutputStart()) return;
                        break;
                    case '>':
                        if (!DoOutputEnd()) return;
                        break;
                    case '*':
                    case '{':
                        if (!DoSelectorStart()) return;
                        break;
                    case '}':
                        if (DoSelectorEnd())
                        {
                            continue;
                        }
                        return;
                    case '/':
                        if (DoSelectorItemEnd())
                        {
                            continue;
                        }
                        return;
                    case '^':
                        if (!DoRepeaterStart()) return;
                        break;
                    case ']':
                        if (Middleman.State.Repeaters.Count > 0)
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
                        Middleman.State.WriteBuffer("\r\n");
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
                        if (Char.IsNumber(Middleman.State.CurrentChar)) // Check if frequency indicator is here. Example: 40%{ +n[plural] are +A. }
                        {
                            if (!DoFrequency())
                            {
                                return;
                            }
                        }
                        
                        if (!"{}[]<>".Contains(Middleman.State.CurrentChar)) // Covers all other characters except brackets
                        {
                            DoNonBrackets();
                        }
                    }
                        break;
                }

                DoBuffer();
            }
            _flagsLocal.Clear();
        }

        private static void DoCapsLock()
        {
            Middleman.State.CurrentFormat = Middleman.State.CurrentFormat == WordCase.AllCaps ? WordCase.None : WordCase.AllCaps;
        }

        private static void DoCapitalize()
        {
            if (Middleman.State.Reader.PeekChar() == '~')
            {
                Middleman.State.Reader.ReadChar();
                Middleman.State.CurrentFormat = WordCase.Capitalized;
            }
            else if (Middleman.State.CurrentFormat == WordCase.Proper)
            {
                Middleman.State.CurrentFormat = WordCase.None;
            }
            else
            {
                Middleman.State.CurrentFormat = WordCase.Proper;
            }
        }

        private static bool DoFrequency()
        {
            int oldPos = Middleman.State.ReadPos;
            int percentIndex = Middleman.State.Reader.Find('%', Middleman.State.ReadPos);
            int nextSpace = Middleman.State.Reader.Find(' ', Middleman.State.ReadPos);
            if (percentIndex > -1 && (percentIndex < nextSpace || nextSpace == -1))
            {
                Middleman.State.ReadPos--; // Revert reading of first digit
                var percentStr = Middleman.State.Reader.ReadTo(percentIndex);
                int percent;
                if (!int.TryParse(percentStr, out percent))
                {
                    Middleman.State.ReadPos = oldPos;
                }
                else
                {
                    if (percent > 100)
                    {
                        percent = 100;
                    }
                    else if (percent <= 0)
                    {
                        Error("0% frequency indicator detected. Why is this here?");
                        return false;
                    }

                    Middleman.State.ReadPos++; // Skip past '%'

                    if ((char)Middleman.State.Reader.PeekChar() == '[')
                    {
                        Middleman.State.ReadPos++; // Skip past '['
                        int closure = Middleman.State.Reader.Source.FindClosingSquareBracket(Middleman.State.ReadPos);

                        if (closure < 0)
                        {
                            Error("Missing closing bracket in frequency indicator.");
                            return false;
                        }

                        if (Middleman.State.RNG.Next(0, 101) > percent)
                        {
                            Middleman.State.ReadPos = closure;
                        }

                        return true;
                    }
                }
            }
            return true;
        }

        private static bool DoOutputStart()
        {
            int beginIndex = Middleman.State.Reader.Find(':', Middleman.State.ReadPos);
            if (beginIndex == -1)
            {
                Error("Couldn't find output name terminator.");
                return false;
            }

            var groupName = Middleman.State.Reader.ReadTo(beginIndex).ToLower();
            Middleman.State.Reader.ReadChar(); // skip ':'

            int endIndex = Middleman.State.Reader.Source.FindClosingTriangleBracket(Middleman.State.ReadPos);
            if (endIndex == -1)
            {
                Error(String.Format("Closing bracket couldn't be found for output '{0}'.", groupName));
                return false;
            }

            Middleman.State.ActiveOutputs.Add(new Output(groupName, beginIndex + 1, endIndex));
            return true;
        }

        private static bool DoOutputEnd()
        {
            var group = Middleman.State.ActiveOutputs.LastOrDefault(gi => gi.Name.ToLower() != "main");
            if (group == null)
            {
                Error("Output closure found with no associated instance.");
                return false;
            }
            Middleman.State.ActiveOutputs.RemoveAt(Middleman.State.ActiveOutputs.Count - 1);
            return true;
        }

        private static void DoNonBrackets()
        {
            if (Middleman.State.PrevChar == ' ' && Middleman.State.CurrentChar == 'a' && !char.IsLetterOrDigit((char)Middleman.State.Reader.PeekChar())) // YES! YES!
            {
                Middleman.State.AnIndex = Middleman.State.Output[Middleman.State.ActiveOutputs[Middleman.State.ActiveOutputs.Count - 1].Name].Length + 1;
                Middleman.State.AnFormat = Middleman.State.CurrentFormat;
            }

            if (Middleman.State.CurrentFormat == WordCase.AllCaps || (Middleman.State.CurrentFormat == WordCase.Proper && !Char.IsLetterOrDigit(Middleman.State.PrevChar) && Middleman.State.PrevChar.PermitsCap()))
            {
                Middleman.State.WriteBuffer(Middleman.State.CurrentChar.ToString(CultureInfo.InvariantCulture).ToUpper());
            }
            else if (Middleman.State.CurrentFormat == WordCase.Capitalized)
            {
                Middleman.State.WriteBuffer(Middleman.State.CurrentChar.ToString(CultureInfo.InvariantCulture).ToUpper());
                Middleman.State.CurrentFormat = WordCase.None;
            }
            else
            {
                Middleman.State.WriteBuffer(Middleman.State.CurrentChar);
            }
        }

        private static bool DoRandomNumber()
        {
            string rnBody;
            int rnStart;
            if (!Middleman.State.Reader.ReadSquareBlock(out rnBody, out rnStart)) return true;
            var m = Regex.Match(rnBody, @"(?<min>\d+)\-(?<max>\d+)", RegexOptions.ExplicitCapture);
            if (!m.Success) return false;

            if (Middleman.State.CurrentFormat == WordCase.Capitalized)
            {
                Middleman.State.CurrentFormat = WordCase.None;
            }

            var min = Int32.Parse(m.Groups["min"].Value);
            var max = Int32.Parse(m.Groups["max"].Value) + 1;

            Middleman.State.WriteBuffer(Middleman.State.RNG.Next(Math.Min(min, max), Math.Max(min, max)).ToString(CultureInfo.InvariantCulture));
            return true;
        }

        private static bool DoSelectorItemEnd()
        {
            if (Middleman.State.CurrentSelector == null)
            {
                Error("Unexpected '/' found in pattern.");
                return false;
            }
            Middleman.State.ReadPos = Middleman.State.CurrentSelector.End + 1;
            Middleman.State.Selectors.RemoveAt(Middleman.State.Selectors.Count - 1);
            return true;
        }

        private static bool DoSelectorEnd()
        {
            if (Middleman.State.CurrentSelector == null)
            {
                Error("Unexpected '}' found in pattern.");
                return false;
            }

            Middleman.State.Selectors.RemoveAt(Middleman.State.Selectors.Count - 1);
            return true;
        }

        private bool DoRepeaterStart()
        {
            // iteration range
            if (Middleman.State.Reader.PeekChar() != '[')
            {
                Error("Repeater iterations parameter did not have an opening bracket.");
                return false;
            }
            Middleman.State.Reader.ReadChar(); // skip [
            var rightRangeBracketIndex = Middleman.State.Reader.Source.FindClosingSquareBracket(Middleman.State.ReadPos);
            if (rightRangeBracketIndex < 0)
            {
                Error("Repeater iterations parameter did not have a closing bracket.");
                return false;
            }
            var strRangeParameter = Middleman.State.Reader.ReadString(rightRangeBracketIndex - Middleman.State.ReadPos).Trim();
            Middleman.State.Reader.ReadChar(); // skip ]
            int constantParam;
            if (!int.TryParse(strRangeParameter, out constantParam))
            {
                var parts = strRangeParameter.Split(new[] { '-' });
                if (parts.Length != 2)
                {
                    Error("Repeater range parameter must be a pair of two numbers.");
                    return false;
                }
                int max;
                int min;
                if (!int.TryParse(parts[0], out min) || !int.TryParse(parts[1], out max))
                {
                    Error("Repeater range parameter did not contain valid numbers.");
                    return false;
                }
                if (min > max || min == 0 || max == 0)
                {
                    Error("Repeater range must be greater than zero, and max > min.");
                    return false;
                }
                constantParam = Middleman.State.RNG.Next(min, max + 1);
            }
            // separator
            if (Middleman.State.Reader.ReadChar() != '[')
            {
                Error("Repeater separator parameter did not have an opening bracket.");
                return false;
            }
            var sepIndex = Middleman.State.ReadPos;
            var rightSepBracketIndex = Middleman.State.Reader.Source.FindClosingSquareBracket(Middleman.State.ReadPos);
            if (rightSepBracketIndex < 0)
            {
                Error("Repeater separator parameter did not have a closing bracket.");
            }
            var strSepParameter = Middleman.State.Reader.ReadString(rightSepBracketIndex - Middleman.State.ReadPos);
            var sepEnd = Middleman.State.ReadPos;
            Middleman.State.Reader.ReadChar(); // skip ]

            // content
            if (Middleman.State.Reader.ReadChar() != '[')
            {
                Error("Repeater content parameter did not have an opening bracket.");
                return false;
            }
            var rightContentBracketIndex = Middleman.State.Reader.Source.FindClosingSquareBracket(Middleman.State.ReadPos);
            if (rightSepBracketIndex < 0)
            {
                Error("Repeater content parameter did not have a closing bracket.");
            }
            var pStart = Middleman.State.ReadPos;

            Middleman.State.Reader.ReadString(rightContentBracketIndex - Middleman.State.ReadPos);

            var pEnd = Middleman.State.ReadPos;

            Middleman.State.Repeaters.Add(new RepeaterInstance(pStart, pEnd, sepIndex, sepEnd, strSepParameter, constantParam));

            Middleman.State.ReadPos = pStart;

            SetLocalFlag("odd_" + Middleman.State.Repeaters.Count);
            SetLocalFlag("first_" + Middleman.State.Repeaters.Count);
            return true;
        }

        private bool DoRepeaterEnd()
        {
            var repeaterCount = Middleman.State.Repeaters.Count;
            var last = repeaterCount - 1;
            var rep = Middleman.State.Repeaters[last];
            if (Middleman.State.ReadPos - 1 != rep.ContentEndIndex && Middleman.State.ReadPos - 1 != rep.SeparatorEndIndex)
            {
                return true;
            }

            UnsetLocalFlag("last_" + repeaterCount);
            UnsetLocalFlag("first_" + repeaterCount);
            if (Middleman.State.Repeaters[last].Iterations == 0)
            {
                SetLocalFlag("first_" + repeaterCount);
            }
            else if (Middleman.State.Repeaters[last].Iterations == Middleman.State.Repeaters[last].MaxIterations - 1)
            {
                SetLocalFlag("last_" + repeaterCount);
            }

            if (rep.OnSeparator) // Currently writing separator?
            {
                rep.OnSeparator = false;
                Middleman.State.ReadPos = rep.ContentStartIndex;
            }
            else // Currently writing content?
            {
                if (Middleman.State.Repeaters[last].Elapse())
                {
                    UnsetLocalFlag("odd_" + repeaterCount);
                    UnsetLocalFlag("even_" + repeaterCount);
                    Middleman.State.Repeaters.RemoveAt(last); // Remove the last repeater if it's finished
                    return true;
                }
                if ((Middleman.State.Repeaters[last].Iterations + 1) % 2 == 0)
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
                Middleman.State.ReadPos = rep.SeparatorStartIndex; // Add separator if not
            }
            return false;
        }

        private bool DoWordCall()
        {
            var match = RegWordCallModern.Match(Middleman.State.Reader.Source, Middleman.State.ReadPos);
            int endIndex = match.Groups["end"].Index;
            if (!(match.Success && match.Index == Middleman.State.ReadPos))
            {
                Error("Invalid word call");
                return false;
            }

            var groups = match.Groups;
            var className = groups["class"].Value.Trim();
            var subtype = groups["subtype"].Value.Trim();
            var carrier = groups["carrier"].Value;
            var symbol = groups["symbol"].Value[0];

            Middleman.State.ReadPos = endIndex;

            if (!_wordBank.ContainsKey(symbol)) // Make sure the symbol is registered
            {
                Error("Word symbol not found: '" + symbol + "'");
            }
            else if (carrier != "")
            {
                var carrierKey = String.Format("{0}:{1}", className, symbol);
                Dictionary<string, int> cd;
                if (!Middleman.State.Carriers.TryGetValue(carrierKey, out cd))
                {
                    cd = new Dictionary<string, int>
                    {
                        {carrier, _wordBank[symbol].GetRandomIndex(Middleman.State.RNG, className)}
                    };
                    Middleman.State.Carriers.Add(carrierKey, cd);
                }
                else if (!cd.ContainsKey(carrier))
                {
                    cd.Add(carrier, _wordBank[symbol].GetRandomIndex(Middleman.State.RNG, className));
                }
                Middleman.State.WriteBuffer(_wordBank[symbol].GetWordByIndex(Middleman.State.Carriers[carrierKey][carrier], subtype, Middleman.State.CurrentFormat));
            }
            else
            {
                if (className.Contains(","))
                {
                    var mcNames = className.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (mcNames.Length < 2)
                    {
                        Error("A multi-class expression must include more than one class name in its parameters.");
                        return false;
                    }
                    for (int i = 0; i < mcNames.Length; i++)
                    {
                        mcNames[i] = mcNames[i].Trim(); // this is to get rid of spaces between the class names
                    }
                    if (mcNames.Any(t => !ClassExists(symbol, t)))
                    {
                        Error("Bad multiclass");
                        return false;
                    }
                    Middleman.State.WriteBuffer(_wordBank[symbol].GetRandomWordMultiClass(Middleman.State.RNG, subtype, Middleman.State.CurrentFormat, mcNames));
                }
                else if (!ClassExists(symbol, className))
                {
                    Error("Class not found: " + symbol + " -> " + className);
                }
                else
                {
                    int index = _wordBank[symbol].GetRandomIndex(Middleman.State.RNG, className);
                    Middleman.State.WriteBuffer(_wordBank[symbol].GetWordByIndex(index, subtype, Middleman.State.CurrentFormat));
                }
            }
            Middleman.State.FormatBuffer(Middleman.State.CurrentFormat);

            if (Middleman.State.AnIndex > -1 && Middleman.State.Buffer.ToString().StartsWithVowel())
            {
                if (Middleman.State.AnFormat == WordCase.AllCaps)
                {
                    Middleman.State.Output[Middleman.State.ActiveOutputs[Middleman.State.ActiveOutputs.Count - 1].Name].Insert(Middleman.State.AnIndex, "N");
                }
                else
                {
                    Middleman.State.Output[Middleman.State.ActiveOutputs[Middleman.State.ActiveOutputs.Count - 1].Name].Insert(Middleman.State.AnIndex, "n");
                }
            }

            if (Middleman.State.CurrentFormat == WordCase.Capitalized)
            {
                Middleman.State.CurrentFormat = WordCase.None;
            }

            Middleman.State.AnIndex = -1;
            Middleman.State.AnFormat = WordCase.None;
            return true;
        }

        private bool DoFunction()
        {
            int leftBracket = Middleman.State.Reader.Find("[", Middleman.State.ReadPos);
            if (leftBracket < 0)
            {
                Error("Missing '[' on function call.");
                return false;
            }
            string func = Middleman.State.Reader.ReadTo(leftBracket).ToLower(),
                param1,
                param2;

            int
                param1Start,
                param2Start;

            if ((func = func.ToLower()).Contains(' '))
            {
                Error("Function names cannot contain spaces.");
                return false;
            }

            if (!Middleman.State.Reader.ReadSquareBlock(out param1, out param1Start))
            {
                Error("Invalid parameter block.");
                return false;
            }
            Middleman.State.Reader.ReadSquareBlock(out param2, out param2Start);

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
                        Middleman.State.ReadPos = param2Start;
                    }
                    break;
                case "l!":
                    if (!CheckLocalFlag(param1))
                    {
                        Middleman.State.ReadPos = param2Start;
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
                        Middleman.State.ReadPos = param2Start;
                    }
                    break;
                case "g!":
                    if (!CheckGlobalFlag(param1))
                    {
                        Middleman.State.ReadPos = param2Start;
                    }
                    break;
                default:
                    if (_customFuncs.ContainsKey(func))
                    {
                        try
                        {
                            Middleman.State.Buffer.Append(_customFuncs[func](Middleman.State.RNG));
                        }
                        catch(Exception ex)
                        {
                            Error("Custom function '" + func + "' threw an exception: " + ex);
                        }
                    }
                    else
                    {
                        Error("Unrecognized flag function.");
                        return false;
                    }
                    break;
            }
            return true;
        }

        private static void DoBuffer()
        {
            var groupCount = Middleman.State.ActiveOutputs.Count;
            var currentGroup = Middleman.State.ActiveOutputs[groupCount - 1];
            var gVis = currentGroup.Visibility;

            switch (gVis)
            {
                case OutputVisibility.Public:
                    {
                        foreach (var group in Middleman.State.ActiveOutputs)
                        {
                            Middleman.State.Output[group.Name].Append(Middleman.State.BufferText);
                        }
                    }
                    break;
                case OutputVisibility.Internal:
                    {
                        for (var i = 0; i < Middleman.State.ActiveOutputs.Count; i++)
                        {
                            var group = Middleman.State.ActiveOutputs[groupCount - (i + 1)];
                            if (group.Visibility != OutputVisibility.Internal) break;
                            Middleman.State.Output[group.Name].Append(Middleman.State.BufferText);
                        }
                    }
                    break;
                case OutputVisibility.Private:
                    {
                        Middleman.State.Output[currentGroup.Name].Append(Middleman.State.BufferText);
                    }
                    break;
            }

            Middleman.State.ClearBuffer();
        }

        private string TranslateDefs(string rawPattern)
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
                            PreError(pp.Position, "Bad def call");
                            return "";
                        }

                        if (macroCall == "")
                        {
                            PreError(start, "Empty def.");
                            return "";
                        }

                        var macroParts = RegMacroCall.Match(macroCall);
                        if (macroParts.Groups.Count == 0 || !macroParts.Success)
                        {
                            PreError(start, "Invalid or empty def.");
                            return "";
                        }

                        var macroName = macroParts.Groups["name"].Value;
                        
                        if (!_defBank.ContainsKey(macroName))
                        {
                            PreError(start, "Def \"" + macroName + "\" doesn't exist.");
                            return "";
                        }
                        
                        var def = _defBank[macroName];
                        var macroBody = def.Body;
                        
                        if (def.Parameters.Count > 0 && def.Type == DefinitionType.Macro)
                        {
                            if (macroParts.Length == 1)
                            {
                                PreError(pp.Position, "Def error: This macro requires parameters, but none were specified.");
                                return "";
                            }
                            List<string> macroParams;
                            if (!macroParts.Groups["parameters"].Value.ParseParameterList(out macroParams))
                            {
                                PreError(pp.Position, "Def error: Invalid parameter list.");
                                return "";
                            }
                            var pCount = macroParams.Count;
                            if (pCount != def.Parameters.Count)
                            {
                                PreError(pp.Position, "Def error: Parameter count mismatch. Expected " + def.Parameters.Count + ", got " + pCount + ".");
                                return "";
                            }

                            for(var i = 0; i < pCount; i++)
                            {
                                macroBody = Regex.Replace(macroBody, "\\&" + Regex.Escape(def.Parameters[i]) + "\\&", macroParams[i]);
                            }
                        }

                        if (def.Type == DefinitionType.Macro)
                        {
                            pattern += TranslateDefs(macroBody);
                        }
                        else
                        {
                            pattern += TranslateDefs(def.State);
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

        private static void Error(string problem)
        {
            Middleman.State.Errors.AddFromState(ErrorType.Interpreter, Middleman.State.Reader.Position, problem);
        }

        private static void PreError(int pos, string problem)
        {
            Middleman.State.Errors.AddFromState(ErrorType.Preprocessor, pos, problem);
        }

        private static bool DoSelectorStart()
        {
            Middleman.State.ReadPos--;
            var match = RegSelector.Match(Middleman.State.Reader.Source, Middleman.State.Reader.Position);
            if (!match.Success)
            {
                Error("Invalid selector. Please check that your syntax is correct.");
                return false;
            }
            var start = match.Groups["start"].Index;
            var typestr = match.Groups["type"].Value;
            var seed = match.Groups["seed"].Value;
            Middleman.State.ReadPos = start + 1;
            var end = Middleman.State.Reader.Source.FindClosingCurlyBracket(Middleman.State.ReadPos);

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
                    Error("Unrecognized selector type: " + typestr);
                    return false;
            }

            if (end == -1)
            {
                Error("Selector is missing a closing bracket.");
                return false;
            }

            var startIndices = Middleman.State.Reader.Source.GetSelectorSubs(Middleman.State.ReadPos);
            if (startIndices.Length < 2)
            {
                Error("Selector is empty or only has one option.");
                return false;
            }

            Middleman.State.Selectors.Add(new SelectorInfo(Middleman.State.RNG, start, end, startIndices.Length, seed, type));
            if (type != SelectorType.Deck && type != SelectorType.CyclicDeck)
            {
                Middleman.State.ReadPos = startIndices[Middleman.State.CurrentSelector.GetIndex()];
            }
            else
            {
                DeckSelectorState nrs;
                if (!Middleman.State.NonRepeatingSelectorStates.TryGetValue(Middleman.State.CurrentSelector.Hash, out nrs))
                {
                    Middleman.State.NonRepeatingSelectorStates.Add(Middleman.State.CurrentSelector.Hash, nrs = new DeckSelectorState(Middleman.State.CurrentSelector.Hash + Middleman.State.RNG.Seed, startIndices.Length, type == SelectorType.CyclicDeck));
                }
                Middleman.State.ReadPos = startIndices[nrs.Next()];
            }
            return true;
        }
    }
}
