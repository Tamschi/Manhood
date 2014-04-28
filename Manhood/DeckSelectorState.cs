namespace Manhood
{
    class DeckSelectorState
    {
        readonly int[] _stage;
        int _index;
        readonly bool _cyclic;
        readonly ManRandom _rand;

        public DeckSelectorState(long seed, int items, bool cyclic)
        {
            _rand = new ManRandom(seed);
            _index = 0;
            _stage = new int[items];
            _cyclic = cyclic;
            FillSelectorStage(seed, items);
            ScrambleSelectorStage();
        }

        public int Next()
        {
            if (_index < _stage.Length) return _stage[_index++];
            _index = 0;
            if (!_cyclic)
            {
                ScrambleSelectorStage();
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
                    s = _rand.Next(_stage.Length);
                }
                while (s == i);

                t = _stage[i];
                _stage[i] = _stage[s];
                _stage[s] = t;
            }
        }
    }
}
