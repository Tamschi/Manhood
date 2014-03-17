using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manhood
{
    class RepeaterInstance
    {
        int _start;
        int _end;
        string _separator;
        int _sepStartIndex;
        int _sepEndIndex;
        int _loops;
        int _currentIteration;
        bool _writeSeparator;

        public RepeaterInstance(int start, int end, int sepStart, int sepEnd, string separator, int loops)
        {
            _start = start;
            _end = end;
            _sepStartIndex = sepStart;
            _sepEndIndex = sepEnd;
            _separator = separator;
            _loops = loops;
            _currentIteration = 0;
        }

        public int ContentStartIndex
        {
            get { return _start; }
        }

        public int SeparatorStartIndex
        {
            get { return _sepStartIndex; }
        }

        public int SeparatorEndIndex
        {
            get { return _sepEndIndex; }
        }

        public int ContentEndIndex
        {
            get { return _end; }
        }

        public string Separator
        {
            get { return _separator; }
        }

        public int Loops
        {
            get { return _loops; }
        }

        public bool OnSeparator
        {
            get { return _writeSeparator; }
            set { _writeSeparator = value; }
        }

        public bool Elapse()
        {
            _currentIteration++;
            return _currentIteration >= _loops;
        }
    }
}
