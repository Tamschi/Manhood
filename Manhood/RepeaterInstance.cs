namespace Manhood
{
    class RepeaterInstance
    {
        public RepeaterInstance(int start, int end, int sepStart, int sepEnd, string separator, int loops)
        {
            ContentStartIndex = start;
            ContentEndIndex = end;
            SeparatorStartIndex = sepStart;
            SeparatorEndIndex = sepEnd;
            Separator = separator;
            MaxIterations = loops;
            Iterations = 0;
        }

        public int ContentStartIndex { get; private set; }

        public int SeparatorStartIndex { get; private set; }

        public int SeparatorEndIndex { get; private set; }

        public int ContentEndIndex { get; private set; }

        public string Separator { get; private set; }

        public int MaxIterations { get; private set; }

        public int Iterations { get; private set; }

        public bool OnSeparator { get; set; }

        public bool Elapse()
        {
            Iterations++;
            return Iterations >= MaxIterations;
        }
    }
}
