﻿using System;
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
        const string patWordCallModern = @"((?<symbol>\w)(?:\[\s*(?<subtype>\w+)?(\s*of\s*(?<class>[\w\&\,]+))?(\s*for\s*((?<carrier>\w+)|(?:\"")(?<carrier>[\w\s]+)(?:\"")))?\s*\])?)(?<end>[^\<][^\>]|$)";

        static readonly Regex regWordCallLegacy = new Regex(patWordCallLegacy, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        static readonly Regex regWordCallModern = new Regex(patWordCallModern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);


        private void Interpret(ManRandom rand, OutputCollection stream, string rawPattern)
        {
            errorLog.Clear();

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
                        ManRandom uniRand = new ManRandom(uniformSeedSalt + currentUniformID);
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

                    UnsetLocalFlag("last_" + repeaterCount);
                    UnsetLocalFlag("first_" + repeaterCount);
                    if (repeaters[last].Iterations == 0)
                    {
                        SetLocalFlag("first_" + repeaterCount);
                    }
                    else if (repeaters[last].Iterations == repeaters[last].MaxIterations - 1)
                    {
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

                #region ManRandom word
                else if (c == '+') // ManRandom word
                {
                    var match = regWordCallModern.Match(reader.Source, reader.Position);
                    int endIndex = match.Groups["end"].Index;
                    if (!(match.Success && match.Index == reader.Position))
                    {
                        match = regWordCallLegacy.Match(reader.Source, reader.Position); // Fall back to legacy and re-test
                        endIndex = match.Index + match.Length;
                        if (!(match.Success && match.Index == reader.Position))
                        {
                            Warning("Invalid word call", reader);
                            continue;
                        }
                    }

                    var groups = match.Groups;
                    string className = groups["class"].Value.Trim();
                    string subtype = groups["subtype"].Value.Trim();
                    string carrier = groups["carrier"].Value;
                    char symbol = groups["symbol"].Value[0];

                    reader.Position = endIndex;

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
                            cd = new Dictionary<string, int>();
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
                else if (c == '#') // ManRandom number
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

                switch (gVis)
                {
                    case GroupVisibility.Public:
                        {
                            foreach (var group in outputGroups)
                            {
                                stream[group.Name].Append(buffer);
                            }
                        }
                        break;
                    case GroupVisibility.Internal:
                        {
                            OutputGroup group = null;
                            for (int i = 0; i < outputGroups.Count; i++)
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
    }
}
