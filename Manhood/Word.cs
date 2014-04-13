using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    public class Word
    {
        private List<string> _wordSet;
        private List<string> _classes;
        private int _weight, _distrWeight;

        public List<string> WordSet
        {
            get { return _wordSet; }
            set { _wordSet = value; }
        }

        public int SubCount
        {
            get { return _wordSet.Count; }
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

        public List<string> Classes
        {
            get { return _classes; }
            set { _classes = value; }
        }

        public int TotalWeight
        {
            get { return _weight + _distrWeight; }
        }

        public Word(List<string> wordSet, int weight, List<string> classes = null)
        {
            _wordSet = wordSet;
            _weight = weight;
            _distrWeight = 0;
            _classes = classes ?? new List<string>();
        }
    }
}
