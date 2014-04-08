using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    public class Pattern
    {
        private string _patternText, _title;

        public string Title
        {
            get { return _title; }
            set { _title = value; }
        }

        public string PatternText
        {
            get { return _patternText; }
            set { _patternText = value; }
        }

        public Pattern(string title, string patternText)
        {
            _title = title;
            _patternText = patternText;
        }

        public override string ToString()
        {
            return _title.Length == 0 ? "Untitled Pattern" : _title;
        }
    }
}
