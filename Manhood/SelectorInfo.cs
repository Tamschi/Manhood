using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    class SelectorInfo
    {
        private long _hash;
        private SelectorType _flags;
        private int _index, _itemCount;
        private int _start, _end;

        private ManRandom _rand;

        public long Hash
        {
            get { return _hash; }
            set { _hash = value; }
        }

        public SelectorType Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public int ItemCount
        {
            get { return _itemCount; }
        }

        public int Index
        {
            get { return _index; }
        }

        public int Start
        {
            get { return _start; }
        }

        public int End
        {
            get { return _end; }
        }

        public int NextIndex()
        {
            switch(_flags)
            {
                case SelectorType.Uniform:
                    return _rand.PeekAt(_hash, _itemCount);
                case SelectorType.NonRepeating:
                    {
                        return _index++;
                    }
                default:
                case SelectorType.None:
                    return _rand.Next(_itemCount);
            }
        }

        public SelectorInfo(ManRandom rand, int start, int end, int itemCount, long hash, SelectorType flags)
        {
            _hash = hash;
            _flags = flags;
            _index = 0;
            _itemCount = itemCount;
            _start = start;
            _end = end;
            _rand = rand;
        }

        public SelectorInfo(ManRandom rand, int start, int end, int itemCount, string hashSeed, SelectorType flags)
        {
            _hash = 12345;
            foreach(char c in hashSeed)
            {
                _hash *= c + 19;
                _hash += 11;
            }
            _flags = flags;
            _index = 0;
            _itemCount = itemCount;
            _start = start;
            _end = end;
            _rand = rand;
        }

        
    }
}
