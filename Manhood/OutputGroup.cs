using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manhood
{
    /// <summary>
    /// Represents an output group.
    /// </summary>
    public class OutputGroup : IEnumerable
    {
        private readonly Dictionary<string, StringBuilder> _dict;

        /// <summary>
        /// Initializes a new instance of the Manhood.OutputGroup class.
        /// </summary>
        public OutputGroup()
        {
            _dict = new Dictionary<string, StringBuilder>();
        }

        /// <summary>
        /// Gets the keys for this output group.
        /// </summary>
        public ICollection<string> Keys
        {
            get { return _dict.Keys; }
        }

        /// <summary>
        /// Gets the values for this output group.
        /// </summary>
        public ICollection<StringBuilder> Values
        {
            get { return _dict.Values; }
        }

        /// <summary>
        /// Returns the main output string for this collection.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this["main"].ToString();
        }

        /// <summary>
        /// Gets the value for a specified output name.
        /// </summary>
        /// <param name="key">The name of the output.</param>
        /// <returns></returns>
        public StringBuilder this[string key]
        {
            get
            {
                StringBuilder v;
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

        /// <summary>
        /// Clears all outputs.
        /// </summary>
        public void Clear()
        {
            _dict.Clear();
        }

        /// <summary>
        /// Returns a boolean indicating if a particular output is contained in the group.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(KeyValuePair<string, StringBuilder> item)
        {
            return _dict.Contains(item);
        }

        /// <summary>
        /// Returns a boolean indicating if the specified key is contained in the group.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool ContainsKey(string item)
        {
            return _dict.ContainsKey(item);
        }

        /// <summary>
        /// Gets the number of outputs in the group.
        /// </summary>
        public int Count
        {
            get { return _dict.Count; }
        }

        /// <summary>
        /// Gets the enumerator for this instance.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<string, StringBuilder>> GetEnumerator()
        {
            return _dict.GetEnumerator();
        }

        IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _dict.GetEnumerator();
        }
    }
}
