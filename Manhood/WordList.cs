using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EasyIO;

namespace Manhood
{
    public class WordList
    {
        #region Non-public fields
        internal char _symbol;

        internal string _title;

        internal string _description;

        internal string[] _subtypes;

        internal Dictionary<string, List<int>> _classes;

        internal List<Word> _words;

        #endregion

        #region Properties

        public char Symbol
        {
            get { return _symbol; }
            set { _symbol = value; }
        }

        public string Title
        {
            get { return _title; }
            set { _title = value; }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        public List<Word> Words
        {
            get { return _words; }
        }

        public Dictionary<string, List<int>> Classes
        {
            get { return _classes; }
        }

        public string[] Subtypes
        {
            get { return _subtypes; }
        }

        #endregion

        private WordList()
        {

        }

        public static WordList LoadModernList(EasyReader reader)
        {
            var list = new WordList();
            list.LoadModern(reader);
            return list;
        }

        public static WordList LoadLegacyList(BinaryReader reader)
        {
            var list = new WordList();
            list.LoadLegacy(reader);
            return list;
        }

        private void LoadLegacy(BinaryReader reader) // Load binary
        {
            _classes = new Dictionary<string, List<int>>();
            _symbol = reader.ReadChar();
            _title = reader.ReadLongString();
            _description = reader.ReadLongString();
            _subtypes = reader.ReadStringArray();

            int itemCount = reader.ReadInt32();

            _words = new List<Word>(itemCount);

            for (int i = 0; i < itemCount; i++)
            {                
                int entryWeight = reader.ReadInt32();
                string[] entrySubtypeArray = reader.ReadStringArray();
                string[] entryClassArray = reader.ReadStringArray();
                if (this.Subtypes.Length > 0)
                {
                    if (entrySubtypeArray.Length != this.Subtypes.Length)
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

            // Read metadata
            reader
                .ReadByte(out symbolByte)
                .ReadString(out _title)
                .ReadString(out _description)
                .ReadStringArray(out _subtypes)
                .ReadInt32(out itemCount);

            _symbol = (char)symbolByte;
            
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

        public void WriteToStream(EasyWriter writer)
        {
            writer
                .Write((byte)_symbol)
                .Write(_title)
                .Write(_description)
                .Write(_subtypes)
                .Write(_words.Count);
           
            foreach(Word word in _words)
            {
                writer
                    .Write(word.Weight)
                    .Write(word.WordSet.ToArray())                    
                    .Write(word.Classes.ToArray());
            }
        }

        public void RandomizeDistWeights(ManRandom rand, int factor)
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
            for (int i = 0; i < Subtypes.Length; i++)
            {
                if (Subtypes[i] == name)
                {
                    return i;
                }
            }
            return -1;
        }

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

        public string GetWordByIndex(int index, string subtype, WordFormat format)
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

        public string GetRandomWord(ManRandom rand, string subtype, string className, WordFormat format)
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

        public string GetRandomWordMultiClass(ManRandom rand, string subtype, WordFormat format, params string[] classNames)
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

        public override string ToString()
        {
            return String.Format("{0} ({1})", _title, _symbol);
        }
    }    
}
