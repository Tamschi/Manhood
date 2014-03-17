using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    public class OutputCollection : IEnumerable
    {
        private Dictionary<string, StringBuilder> _dict;

        public OutputCollection()
        {
            _dict = new Dictionary<string, StringBuilder>();
        }

        public ICollection<string> Keys
        {
            get { return _dict.Keys; }
        }

        public ICollection<StringBuilder> Values
        {
            get { return _dict.Values; }
        }

        public override string ToString()
        {
            return this["main"].ToString();
        }

        public StringBuilder this[string key]
        {
            get
            {
                StringBuilder v = null;
                if (_dict.TryGetValue(key, out v))
                {
                    return v;
                }
                v = new StringBuilder();
                _dict.Add(key, v);
                return v;
            }
            set
            {
                if (_dict.ContainsKey(key))
                {
                    _dict[key] = value;
                }
                else
                {
                    _dict.Add(key, value);
                }
            }
        }

        public void Clear()
        {
            _dict.Clear();
        }

        public bool Contains(KeyValuePair<string, StringBuilder> item)
        {
            return _dict.Contains(item);
        }

        public bool ContainsKey(string item)
        {
            return _dict.ContainsKey(item);
        }

        public int Count
        {
            get { return _dict.Count; }
        }

        public IEnumerator<KeyValuePair<string, StringBuilder>> GetEnumerator()
        {
            return _dict.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _dict.GetEnumerator();
        }
    }
}
