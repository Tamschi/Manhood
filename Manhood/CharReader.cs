using System;

namespace Manhood
{
    internal class CharReader
    {
        public string Source { get; private set; }

        public int Position
        {
            get;
            set;
        }

        public bool EndOfString
        {
            get { return Position >= Source.Length; }
        }

        public CharReader(string source, int start)
        {
            Source = source;
            Position = start;
        }

        public void Insert(int index, string content)
        {
            Source = Source.Insert(index, content);
        }

        public char ReadChar()
        {
            return Source[Position++];
        }

        public int PeekChar()
        {
            if (EndOfString) return -1;
            return Source[Position];
        }

        public bool ReadSquareBlock(out string body, out int start)
        {
            start = 0;
            if (PeekChar() != '[')
            {
                body = "";
                return false;
            }
            ReadChar();
            start = Position;
            var close = Source.FindClosingSquareBracket(Position);
            if (close < 0)
            {
                body = "";
                return false;
            }
            body = ReadTo(close);
            Position++;
            return true;
        }

        public int Find(string str, int start)
        {
            int ind;
            if ((ind = Source.IndexOf(str, start, StringComparison.Ordinal)) < 0) return -1;
            if (start == 0)
            {
                return ind;
            }
            if (Source[ind - 1] != '\\')
            {
                return ind;
            }

            return -1;
        }

        public int Find(char c, int start)
        {
            int ind;
            if ((ind = Source.IndexOf(c, start)) < 0) return -1;
            if (start == 0)
            {
                return ind;
            }
            if (Source[ind - 1] != '\\')
            {
                return ind;
            }

            return -1;
        }

        public string ReadString(int length)
        {
            var temp = Source.Substring(Position, length);
            Position += length;
            return temp;
        }

        public string ReadTo(int index)
        {
            return ReadString(index - Position);
        }
    }
}
