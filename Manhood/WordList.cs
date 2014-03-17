using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Manhood
{
    public class WordList
    {
        public char Symbol;

        public string Title;

        public string Description;

        public string[] Subtypes;

        public Dictionary<string, List<int>> Classes;


        public string[][] Words; //d1 = word, d2 = subtype        

        private int[] Weights;

        private int[] DistWeights;

        public WordList(string path, ref int total)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                Load(reader, ref total);
            }
        }

        public WordList(BinaryReader reader, ref int total)
        {
            Load(reader, ref total);
        }

        private void Load(BinaryReader reader, ref int total) // Load binary
        {
            this.Classes = new Dictionary<string, List<int>>();
            this.Symbol = reader.ReadChar();
            this.Title = reader.ReadLongString();
            this.Description = reader.ReadLongString();
            this.Subtypes = reader.ReadStringArray();
            int itemCount = reader.ReadInt32();

            this.Words = new string[itemCount][];
            this.Weights = new int[itemCount];
            this.DistWeights = new int[itemCount];

            for (int i = 0; i < itemCount; i++)
            {
                int entryWeight = reader.ReadInt32();
                string[] entrySubtypeArray = reader.ReadStringArray();
                string[] entryClassArray = reader.ReadStringArray();
                if (this.Subtypes.Length > 0)
                {
                    if (entrySubtypeArray.Length != this.Subtypes.Length)
                    {
                        throw new InvalidDataException(this.Title + " - One or more entries do not have the correct number of items for the subtypes given.");
                    }
                }
                foreach (string entryClassName in entryClassArray)
                {
                    if (entryClassName != "")
                    {
                        if (!this.Classes.ContainsKey(entryClassName))
                        {
                            this.Classes.Add(entryClassName, new List<int>());
                        }
                        this.Classes[entryClassName].Add(i);
                    }
                }
                this.Words[i] = entrySubtypeArray;
                this.Weights[i] = entryWeight;
            }
            total += itemCount;
        }

        public bool Merge(WordList list)
        {
            if (list.Subtypes.Length != this.Subtypes.Length)
            {
                return false;
            }

            int o = this.Words.Length;
            int s = list.Words.Length + this.Words.Length;

            if (o == s)
            {
                return true;
            }

            Array.Resize<string[]>(ref this.Words, s);
            Array.Resize<int>(ref this.Weights, s);
            Array.Resize<int>(ref this.DistWeights, s);

            Array.Copy(list.Words, 0, this.Words, o, list.Words.Length);
            Array.Copy(list.Weights, 0, this.Weights, o, list.Weights.Length);
            Array.Copy(list.DistWeights, 0, this.DistWeights, o, list.DistWeights.Length);

            foreach(KeyValuePair<string, List<int>> pair in list.Classes)
            {
                if (this.Classes.ContainsKey(pair.Key))
                {
                    this.Classes[pair.Key].AddRange(pair.Value);
                }
                else
                {
                    this.Classes.Add(pair.Key, pair.Value);
                }
            }

            return true;
        }

        public void RandomizeDistWeights(Random rand, int factor)
        {
            for (int i = 0; i < this.DistWeights.Length; i++)
            {
                if (rand.Next(0, factor * 3 + 1) == 0)
                {
                    this.DistWeights[i] = rand.Next(1, 20) * factor;
                }
                else
                {
                    this.DistWeights[i] = 0;
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

        public int GetRandomIndex(Random rand, string className)
        {
            if (className == "")
            {
                return rand.Next(0, this.Words.Length);
            }
            else
            {
                if (!this.Classes.ContainsKey(className))
                {
                    return -1;
                }

                return this.Classes[className][rand.Next(0, this.Classes[className].Count)];
            }
        }

        public string GetWordByIndex(int index, string subtype, WordFormat format)
        {
            int subIndex = LookForSubtype(subtype);

            if (subIndex == -1)
            {
                return Error("SubtypeNotFound->" + subtype);
            }
            if (subIndex > Words[index].Length - 1)
            {
                return Error("InadequateSubtypeCount");
            }
            return Format(Words[index][subIndex], format);
        }

        public string GetRandomWord(Random rand, string subtype, string className, WordFormat format)
        {           
            int subIndex = LookForSubtype(subtype);
            if (subIndex == -1)
            {
                return Error("SubtypeNotFound->"+subtype);
            }
            int index = PickByWeight(className, rand);
            if (className == "")
            {                
                if (subIndex > Words[index].Length - 1)
                {
                    return Error("SubtypeOutOfRange");
                }
                return Format(Words[index][subIndex], format);
            }
            else
            {
                if (!this.Classes.ContainsKey(className))
                {
                    return Error("ClassNotFound->"+className);
                }
                return Format(Words[this.Classes[className][index]][subIndex], format);
            }
        }

        public string GetRandomWordMultiClass(Random rand, string subtype, WordFormat format, params string[] classNames)
        {
            int subIndex = LookForSubtype(subtype);
            if (subIndex == -1)
            {
                return Error("SubtypeNotFound->" + subtype);
            }
            for (int i = 0; i < classNames.Length; i++)
            {
                if (!this.Classes.ContainsKey(classNames[i]))
                {
                    return Error("ClassNotFound->" + classNames[i]);
                }
            }
            List<int> mcList = GetMultiClassList(classNames);
            if (mcList.Count == 0)
            {
                return Error("EmptyMultiClass");
            }
            int index = PickByWeight(mcList, rand);
            return Format(Words[mcList[index]][subIndex], format);
        }

        private List<int> GetMultiClassList(params string[] classNames)
        {
            List<int> words = new List<int>();
            List<int> firstClass = this.Classes[classNames[0]];
            for (int i = 0; i < firstClass.Count; i++) // loop through every item of the first class
            {
                int matchCount = 0;
                for (int j = 1; j < classNames.Length; j++) // loop through all the other classes
                {
                    List<int> currentClass = this.Classes[classNames[j]];
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

        private int PickByWeight(List<int> items, Random rand)
        {
            int total = TotalWeights(items);
            int randomNumber = rand.Next(0, total);
            int selectedIndex = 0;
            int count = items.Count;
            for (int i = 0; i < count; i++)
            {
                if (randomNumber < this.Weights[items[i]] + this.DistWeights[items[i]])
                {
                    selectedIndex = i;
                    break;
                }
                randomNumber -= this.Weights[items[i]] + this.DistWeights[items[i]];
            }
            return selectedIndex;
        }

        private int PickByWeight(string className, Random rand)
        {
            int total = TotalWeights(className);
            int randomNumber = rand.Next(0, total);
            int selectedIndex = 0;
            if (className == "") // This will go through all the words
            {
                for (int i = 0; i < this.Weights.Length; i++)
                {
                    if (randomNumber < this.Weights[i] + this.DistWeights[i])
                    {
                        selectedIndex = i;
                        break;
                    }
                    randomNumber -= this.Weights[i] + this.DistWeights[i];
                }
            }
            else
            {
                List<int> c = this.Classes[className];
                int count = c.Count;
                for (int i = 0; i < count; i++)
                {
                    if (randomNumber < this.Weights[c[i]] + this.DistWeights[c[i]])
                    {
                        selectedIndex = i;
                        break;
                    }
                    randomNumber -= this.Weights[c[i]] + this.DistWeights[c[i]];
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
                length = this.Weights.Length;
                for (int i = 0; i < this.Weights.Length; i++)
                {
                    sum += this.Weights[i] + this.DistWeights[i];
                }
                return sum;
            }
            else
            {
                List<int> list = this.Classes[className];
                length = list.Count;
                for (int i = 0; i < length; i++)
                {
                    sum += this.Weights[list[i]] + this.Weights[list[i]];
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
                sum += this.Weights[items[i]] + this.Weights[items[i]];
            }
            return sum;
        }

        private string Format(string input, WordFormat format)
        {
            switch (format)
            {
                case WordFormat.AllCaps:
                    return input.ToUpper();
                case WordFormat.Proper:
                    return input.Substring(0, 1).ToUpper() + input.Substring(1);
                case WordFormat.None:
                default:
                    return input;
            }
        }

        private string Error(string type)
        {
            return "<" + this.Symbol + ":" + type + ">";
        }
    }    
}
