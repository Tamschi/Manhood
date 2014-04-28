namespace Manhood
{
    class Output
    {
        public string Name;
        public int Start, End;
        public OutputVisibility Visibility;

        public Output(string name, int start, int end)
        {
            if (name.StartsWith("."))
            {
                name = name.Substring(1);
                Visibility = OutputVisibility.Private;
            }
            else if (name.StartsWith("_"))
            {
                name = name.Substring(1);
                Visibility = OutputVisibility.Internal;
            }
            else
            {
                Visibility = OutputVisibility.Public;
            }
            Name = name;
            Start = start;
            End = end;
        }
    }

    enum OutputVisibility
    {
        Public,
        Internal,
        Private
    }
}
