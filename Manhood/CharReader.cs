using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manhood
{
    internal class CharReader
    {
        string src;

        public string Source
        {
            get { return src; }
        }

        public int Position
        {
            get;
            set;
        }

        public bool EndOfString
        {
            get { return this.Position >= src.Length; }
        }

        public CharReader(string source, int start)
        {
            src = source;
            this.Position = start;
        }

        public void Insert(int index, string content)
        {
            src = src.Insert(index, content);
        }

        public char ReadChar()
        {
            return src[this.Position++];
        }

        public int PeekChar()
        {
            if (this.EndOfString) return -1;
            return src[this.Position];
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
            start = this.Position;
            int close = src.FindClosingSquareBracket(this.Position);
            if (close < 0)
            {
                body = "";
                return false;
            }
            body = ReadTo(close);
            this.Position++;
            return true;
        }

        public string ReadString(int length)
        {
            string temp = src.Substring(this.Position, length);
            this.Position += length;
            return temp;
        }

        public string ReadTo(int index)
        {
            return ReadString(index - this.Position);
        }
    }
}
