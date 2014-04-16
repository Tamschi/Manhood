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
        private SelectorType _type;
        private int _itemCount;
        private int _start, _end;

        private ManRandom _rand;

        public long Hash
        {
            get { return _hash; }
            set { _hash = value; }
        }

        public SelectorType Type
        {
            get { return _type; }
            set { _type = value; }
        }

        public int ItemCount
        {
            get { return _itemCount; }
        }

        public int Start
        {
            get { return _start; }
        }

        public int End
        {
            get { return _end; }
        }

        public int GetIndex()
        {
            switch(_type)
            {
                case SelectorType.Uniform:
                    return _rand.PeekAt(_hash, _itemCount);                
                case SelectorType.Random:
                default:
                    return _rand.Next(_itemCount);
            }
        }

        public SelectorInfo(ManRandom rand, int start, int end, int itemCount, long hash, SelectorType flags)
        {
            _hash = hash;
            _type = flags;
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
            _type = flags;
            _itemCount = itemCount;
            _start = start;
            _end = end;
            _rand = rand;
        }
    }
}
