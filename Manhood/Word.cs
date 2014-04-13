using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    /// <summary>
    /// Represents a word list entry.
    /// </summary>
    public class Word
    {
        private List<string> _wordSet;
        private List<string> _classes;
        private int _weight, _distrWeight;

        /// <summary>
        /// Gets or sets the subtype values for this entry.
        /// </summary>
        public List<string> WordSet
        {
            get { return _wordSet; }
            set { _wordSet = value; }
        }

        /// <summary>
        /// Gets the number of subtype values for this entry.
        /// </summary>
        public int SubCount
        {
            get { return _wordSet.Count; }
        }

        /// <summary>
        /// Gets or sets the base weight for this entry.
        /// </summary>
        public int Weight
        {
            get { return _weight; }
            set { _weight = value; }
        }

        /// <summary>
        /// Gets or sets the weight offset for this entry.
        /// </summary>
        public int WeightOffset
        {
            get { return _distrWeight; }
            set { _distrWeight = value; }
        }

        /// <summary>
        /// Gets or sets the classes assigned to this entry.
        /// </summary>
        public List<string> Classes
        {
            get { return _classes; }
            set { _classes = value; }
        }

        /// <summary>
        /// Gets the total weight for this entry.
        /// </summary>
        public int TotalWeight
        {
            get { return _weight + _distrWeight; }
        }

        /// <summary>
        /// Creates a new word entry.
        /// </summary>
        /// <param name="wordSet">The subtype values for this word.</param>
        /// <param name="weight">The weight to assign to this word.</param>
        /// <param name="classes">The classes to assign to this word.</param>
        public Word(List<string> wordSet, int weight, List<string> classes = null)
        {
            _wordSet = wordSet;
            _weight = weight;
            _distrWeight = 0;
            _classes = classes ?? new List<string>();
        }
    }
}
