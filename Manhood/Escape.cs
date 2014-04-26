using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    static class Escape
    {
        private static readonly Dictionary<char, char> seqs = new Dictionary<char, char>()
        {
            { 'n', '\n' },
            { 'r', '\r' },
            { 't', '\t' },
            { 's', ' ' },
        };

        public static char GetChar(char code)
        {
            if (!seqs.ContainsKey(code))
            {
                return code;
            }
            else
            {
                return seqs[code];
            }
        }
    }
}
