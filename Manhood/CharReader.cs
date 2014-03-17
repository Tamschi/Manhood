using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manhood
{
    public class CharReader
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
