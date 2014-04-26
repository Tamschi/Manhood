using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    internal class EngineState
    {
        public CharReader Reader;
        public WordCase CurrentFormat = WordCase.None;
        public WordCase AnFormat = WordCase.None;
        public int AnIndex = -1;
        public List<Output> ActiveOutputs = new List<Output>();
        public StringBuilder Buffer;
        public OutputGroup Output;
        public ManRandom RNG;

        private char
            currentChar = '\0',
            prevChar = '\0';

        // Selector stuff
        public List<SelectorInfo> Selectors = new List<SelectorInfo>();
        public Dictionary<long, DeckSelectorState> NonRepeatingSelectorStates = new Dictionary<long, DeckSelectorState>();

        public SelectorInfo CurrentSelector
        {
            get
            {
                if (Selectors.Count == 0)
                {
                    return null;
                }
                else
                {
                    return Selectors[Selectors.Count - 1];
                }
            }
        }

        // Carrier stuff
        public Dictionary<string, Dictionary<string, int>> Carriers = new Dictionary<string, Dictionary<string, int>>();

        // Repeater stuff
        public List<RepeaterInstance> Repeaters = new List<RepeaterInstance>();

        public EngineState()
        {
            Reader = null;
            ActiveOutputs = new List<Output>();
            Buffer = new StringBuilder();
            Output = new OutputGroup();
        }

        public void Start(ManRandom rng, OutputGroup oc, string pattern)
        {
            RNG = rng;
            Output = oc;

            CurrentFormat = AnFormat = WordCase.None;

            Reader = new CharReader(pattern, 0);

            Selectors.Clear();
            NonRepeatingSelectorStates.Clear();
            Carriers.Clear();
            Repeaters.Clear();
            ActiveOutputs.Clear();
            ActiveOutputs.Add(new Output("main", 0, Reader.Source.Length));
        }

        public void WriteBuffer(string content)
        {
            Buffer.Append(content);
        }

        public void WriteBuffer(char content)
        {
            Buffer.Append(content);
        }

        public void FormatBuffer(WordCase format)
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
}
