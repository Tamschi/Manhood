using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Manhood
{
    internal class PatternList
    {
        public char Symbol;
        public string Title;

        private List<Pattern> _patterns;

        public List<Pattern> Patterns
        {
            get { return _patterns; }
        }

        public PatternList(string title, char symbol, List<Pattern> patterns)
        {
            this.Title = title;
            this.Symbol = symbol;
            this._patterns = patterns;
        }

        public bool Merge(PatternList list)
        {
            if (list.Symbol != this.Symbol)
            {
                return false;
            }

            _patterns.AddRange(list._patterns);

            return true;
        }

        public Pattern GetPattern(ManRandom r)
        {
            return _patterns[r.Next(this._patterns.Count)];
        }
    }
}
