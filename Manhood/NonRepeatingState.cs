using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    class NonRepeatingState
    {
        int[] _stage;
        int _index;

        public NonRepeatingState(long seed, int items)
        {
            _index = 0;
            _stage = new int[items];
            FillSelectorStage(seed, items);
            ScrambleSelectorStage(seed);
        }

        public int Next()
        {
            if (_index >= _stage.Length) return -1;
            return _stage[_index++];
        }

        public void FillSelectorStage(long seed, int itemCount)
        {            
            for (int i = 0; i < itemCount; _stage[i] = i++) { }
        }

        public void ScrambleSelectorStage(long seed)
        {
            int t, s;
            ManRandom rand = new ManRandom(seed);

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
