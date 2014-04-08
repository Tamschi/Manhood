using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    public class Word
    {
        private string[] _wordSet;
        private int _weight, _distrWeight;

        public string[] WordSet
        {
            get { return _wordSet; }
            set { _wordSet = value; }
        }

        public int SubCount
        {
            get { return _wordSet.Length; }
        }

        public int Weight
        {
            get { return _weight; }
            set { _weight = value; }
        }

        public int WeightOffset
        {
            get { return _distrWeight; }
            set { _distrWeight = value; }
        }

        public int TotalWeight
        {
            get { return _weight + _distrWeight; }
        }

        public Word(string[] wordSet, int weight)
        {
            _wordSet = wordSet;
            _weight = weight;
            _distrWeight = 0;
        }
    }
}
