using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EasyIO;

namespace Manhood
{
    /// <summary>
    /// Represents a list of words.
    /// </summary>
    public class WordList
    {
        #region Non-public fields
        internal char _symbol;

        internal string _title;

        internal string _description;

        internal List<Subtype> _subtypes;

        internal Dictionary<string, List<int>> _classes;

        internal List<Word> _words;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the symbol associated with this list.
        /// </summary>
        public char Symbol
        {
            get { return _symbol; }
            set { _symbol = value; }
        }

        /// <summary>
        /// Gets or sets the title of this list.
        /// </summary>
        public string Title
        {
            get { return _title; }
            set { _title = value; }
        }

        /// <summary>
        /// Gets or sets the description of this list.
        /// </summary>
        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        /// <summary>
        /// Gets the entry collection for this list.
        /// </summary>
        public List<Word> Words
        {
            get { return _words; }
        }

        /// <summary>
        /// Gets the class index table for this list.
        /// </summary>
        public Dictionary<string, List<int>> Classes
        {
            get { return _classes; }
        }

        /// <summary>
        /// Gets the subtypes for this list.
        /// </summary>
        public List<Subtype> Subtypes
        {
            get { return _subtypes; }
        }

        #endregion

        /// <summary>
        /// Creates an empty word list.
        /// </summary>
        public WordList()
        {
            _subtypes = new List<Subtype>();
            _description = "";
            _title = "";
            _symbol = 'A';
            _classes = new Dictionary<string, List<int>>();
            _words = new List<Word>();
        }

        /// <summary>
        /// Loads a word list in the modern format.
        /// </summary>
        /// <param name="reader">The EasyReader to read data from.</param>
        /// <returns></returns>
        public static WordList LoadModernList(EasyReader reader)
        {
            var list = new WordList();
            list.LoadModern(reader);
            return list;
        }

        /// <summary>
        /// Loads a word list in the legacy format.
        /// </summary>
        /// <param name="reader">The BinaryReader to read data from.</param>
        /// <returns></returns>
        [Obsolete]
        public static WordList LoadLegacyList(BinaryReader reader)
        {
            var list = new WordList();
            list.LoadLegacy(reader);
            return list;
        }

        /// <summary>
        /// Retrieves a random word index from the specified class.
        /// </summary>
        /// <param name="rand">The random number generator to pass to the engine.</param>
        /// <param name="className">The class to get a word from.</param>
        /// <returns></returns>
        public int GetRandomIndex(ManRandom rand, string className)
        {
            if (className == "")
            {
                return rand.Next(0, _words.Count);
            }
            else
            {
                if (!this._classes.ContainsKey(className))
                {
                    return -1;
                }

                return this._classes[className][rand.Next(0, this._classes[className].Count)];
            }
        }

        /// <summary>
        /// Gets the word at the specified index and of the specified subtype.
        /// </summary>
        /// <param name="index">The index of the entry.</param>
        /// <param name="subtype">The desired subtype.</param>
        /// <param name="format">The formatting to use.</param>
        /// <returns></returns>
        public string GetWordByIndex(int index, string subtype, WordCase format = WordCase.None)
        {
            int subIndex = LookForSubtype(subtype);

            if (subIndex == -1)
            {
                return Error("SubtypeNotFound({0})", subtype);
            }
            if (subIndex > _words[index].SubCount - 1)
            {
                return Error("InadequateSubtypeCount");
            }
            return _words[index].WordSet[subIndex].Capitalize(format);
        }

        /// <summary>
        /// Gets a random word of the specified subtype, class and format.
        /// </summary>
        /// <param name="rand">The random number generator to pass to the engine.</param>
        /// <param name="subtype">The desired subtype.</param>
        /// <param name="className">The class to get a word from.</param>
        /// <param name="format">The formatting to use.</param>
        /// <returns></returns>
        public string GetRandomWord(ManRandom rand, string subtype, string className, WordCase format = WordCase.None)
        {
            int subIndex = LookForSubtype(subtype);
            if (subIndex == -1)
            {
                return Error("SubtypeNotFound({0})", subtype);
            }
            int index = PickByWeight(className, rand);
            if (className == "")
            {
                if (subIndex > _words[index].SubCount - 1)
                {
                    return Error("SubtypeOutOfRange");
                }
                return _words[index]
                    .WordSet[subIndex]
                    .Capitalize(format);
            }
            else
            {
                if (!this._classes.ContainsKey(className))
                {
                    return Error("ClassNotFound({0})", className);
                }
                return _words[this._classes[className][index]]
                    .WordSet[subIndex]
                    .Capitalize(format);
            }
        }

        /// <summary>
        /// Gets a random word that belongs to a list of classes.
        /// </summary>
        /// <param name="rand">The random number generator to pass to the engine.</param>
        /// <param name="subtype">The desired subtype.</param>
        /// <param name="format">The formatting to use.</param>
        /// <param name="classNames">The classes from which to get the word.</param>
        /// <returns></returns>
        public string GetRandomWordMultiClass(ManRandom rand, string subtype, WordCase format, params string[] classNames)
        {
            int subIndex = LookForSubtype(subtype);
            if (subIndex == -1)
            {
                return Error("SubtypeNotFound({0})", subtype);
            }
            for (int i = 0; i < classNames.Length; i++)
            {
                if (!this._classes.ContainsKey(classNames[i]))
                {
                    return Error("ClassNotFound({0})", classNames[i]);
                }
            }
            List<int> mcList = GetMultiClassList(classNames);
            if (mcList.Count == 0)
            {
                return Error("EmptyMultiClass");
            }
            int index = PickByWeight(mcList, rand);
            return _words[mcList[index]]
                .WordSet[subIndex]
                .Capitalize(format);
        }

        /// <summary>
        /// Writes the data contained in this instance to a stream.
        /// </summary>
        /// <param name="writer"></param>
        public void WriteToStream(EasyWriter writer)
        {
            writer
                .Write((byte)_symbol)
                .Write(_title)
                .Write(_description)
                .Write(_subtypes.Select<Subtype, string>(sub => sub.Name).ToArray())
                .Write(_words.Count);

            foreach (Word word in _words)
            {
                writer
                    .Write(word.Weight)
                    .Write(word.WordSet.ToArray())
                    .Write(word.Classes.ToArray());
            }
        }

        /// <summary>
        /// Returns the string equivalent of this instance.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("{0} ({1})", _title.Length > 0 ? _title : "Untitled List", _symbol);
        }

        #region Non-public methods

        private void LoadLegacy(BinaryReader reader)
        {
            _classes = new Dictionary<string, List<int>>();
            _symbol = reader.ReadChar();
            _title = reader.ReadLongString();
            _description = reader.ReadLongString();
            _subtypes.AddRange(reader.ReadStringArray().Select<string, Subtype>(str => new Subtype(str)));

            int itemCount = reader.ReadInt32();

            _words = new List<Word>(itemCount);

            for (int i = 0; i < itemCount; i++)
            {                
                int entryWeight = reader.ReadInt32();
                string[] entrySubtypeArray = reader.ReadStringArray();
                string[] entryClassArray = reader.ReadStringArray();
                if (this.Subtypes.Count > 0)
                {
                    if (entrySubtypeArray.Length != this.Subtypes.Count)
                    {
                        throw new InvalidDataException(this._title + " - One or more entries do not have the correct number of items for the subtypes given.");
                    }
                }
                foreach (string entryClassName in entryClassArray)
                {
                    if (entryClassName != "")
                    {
                        if (!this._classes.ContainsKey(entryClassName))
                        {
                            this._classes.Add(entryClassName, new List<int>());
                        }
                        this._classes[entryClassName].Add(i);
                    }
                }

                _words.Add(new Word(entrySubtypeArray.ToList(), entryWeight, entryClassArray.ToList()));
            }
        }

        private void LoadModern(EasyReader reader)
        {
            int itemCount;
            byte symbolByte;
            string[] subs;

            // Read metadata
            reader
                .ReadByte(out symbolByte)
                .ReadString(out _title)
                .ReadString(out _description)
                .ReadStringArray(out subs)
                .ReadInt32(out itemCount);

            _symbol = (char)symbolByte;
            _subtypes.AddRange(subs.Select<string, Subtype>(str => new Subtype(str)));
            
            // Read entries
            _words = new List<Word>(itemCount);
            _classes = new Dictionary<string, List<int>>();

            for(int i = 0; i < itemCount; i++)
            {
                int w = reader.ReadInt32();
                var entries = reader.ReadStringArray().ToList();
                var cl = reader.ReadStringArray().ToList();
                Word word = new Word(entries, w, cl);
                
                foreach(string c in cl)
                {
                    if (!_classes.ContainsKey(c))
                    {
                        _classes.Add(c, new List<int>());
                    }
                    _classes[c].Add(i);
                }
                _words.Add(word);
            }
        }

        internal void RandomizeDistWeights(ManRandom rand, int factor)
        {
            for (int i = 0; i < _words.Count; i++)
            {
                if (rand.Next(0, factor * 3 + 1) == 0)
                {
                    _words[i].WeightOffset = rand.Next(1, 20) * factor;
                }
                else
                {
                    _words[i].WeightOffset = 0;
                }
            }
        }

        private int LookForSubtype(string name)
        {
            if (name == "") return 0; // Default is first subtype for a blank name.
            for (int i = 0; i < Subtypes.Count; i++)
            {
                if (Subtypes[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        private List<int> GetMultiClassList(params string[] classNames)
        {
            List<int> words = new List<int>();
            List<int> firstClass = this._classes[classNames[0]];
            for (int i = 0; i < firstClass.Count; i++) // loop through every item of the first class
            {
                int matchCount = 0;
                for (int j = 1; j < classNames.Length; j++) // loop through all the other classes
                {
                    List<int> currentClass = this._classes[classNames[j]];
                    for (int k = 0; k < currentClass.Count; k++) // search their contents and match them up
                    {
                        if (firstClass[i] == currentClass[k])
                        {
                            matchCount++;
                        }
                    }
                }
                if (matchCount == classNames.Length - 1)
                {
                    words.Add(firstClass[i]);
                }
            }
            return words;
        }

        private int PickByWeight(List<int> items, ManRandom rand)
        {
            int total = TotalWeights(items);
            int randomNumber = rand.Next(0, total);
            int selectedIndex = 0;
            int count = items.Count;
            for (int i = 0; i < count; i++)
            {
                if (randomNumber < _words[items[i]].TotalWeight)
                {
                    selectedIndex = i;
                    break;
                }
                randomNumber -= _words[items[i]].TotalWeight;
            }
            return selectedIndex;
        }

        private int PickByWeight(string className, ManRandom rand)
        {
            int total = TotalWeights(className);
            int randomNumber = rand.Next(0, total);
            int selectedIndex = 0;
            if (className == "") // This will go through all the words
            {
                for (int i = 0; i < _words.Count; i++)
                {
                    if (randomNumber < _words[i].TotalWeight)
                    {
                        selectedIndex = i;
                        break;
                    }
                    randomNumber -= _words[i].TotalWeight;
                }
            }
            else
            {
                List<int> c = this._classes[className];
                int count = c.Count;
                for (int i = 0; i < count; i++)
                {
                    if (randomNumber < _words[c[i]].TotalWeight)
                    {
                        selectedIndex = i;
                        break;
                    }
                    randomNumber -= _words[c[i]].TotalWeight;
                }
            }
            return selectedIndex;
        }

        private int TotalWeights(string className)
        {
            int sum = 0;
            int length;
            if (className == "")
            {
                length = _words.Count;
                for (int i = 0; i < length; i++)
                {
                    sum += _words[i].TotalWeight;
                }
                return sum;
            }
            else
            {
                List<int> list = this._classes[className];
                length = list.Count;
                for (int i = 0; i < length; i++)
                {
                    sum += _words[list[i]].TotalWeight;
                }
                return sum;
            }
        }

        private int TotalWeights(List<int> items)
        {
            int sum = 0;
            int length= items.Count;
            for (int i = 0; i < length; i++)
            {
                sum += _words[items[i]].TotalWeight;
            }
            return sum;
        }

        private string Error(string type, params object[] args)
        {
            return "<" + this._symbol + ":" + String.Format(type, args) + ">";
        }

        #endregion
    }    
}
