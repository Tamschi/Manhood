using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    class DeckSelectorState
    {
        int[] _stage;
        int _index;
        bool _cyclic;
        ManRandom rand;

        public DeckSelectorState(long seed, int items, bool cyclic)
        {
            rand = new ManRandom(seed);
            _index = 0;
            _stage = new int[items];
            _cyclic = cyclic;
            FillSelectorStage(seed, items);
            ScrambleSelectorStage();
        }

        public int Next()
        {
            if (_index >= _stage.Length)
            {
                _index = 0;
                if (!_cyclic)
                {
                    ScrambleSelectorStage();
                }
            }
            return _stage[_index++];
        }

        public void FillSelectorStage(long seed, int itemCount)
        {            
            for (int i = 0; i < itemCount; _stage[i] = i++) { }
        }

        public void ScrambleSelectorStage()
        {
            int t, s;

            for (int i = 0; i < _stage.Length; i++)
            {
                do
                {
                    s = rand.Next(_stage.Length);
                }
                while (s == i);

                t = _stage[i];
                _stage[i] = _stage[s];
                _stage[s] = t;
            }
        }
    }
}
