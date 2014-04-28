using System;

namespace Manhood
{
    /// <summary>
    /// Represents a subtype for a word list entry.
    /// </summary>
    public class Subtype
    {
        private string _name;

        /// <summary>
        /// Gets or sets the name of the subtype.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        /// <summary>
        /// Creates a new subtype with the specified name.
        /// </summary>
        /// <param name="name">The name of the subtype.</param>
        public Subtype(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Returns a string representation of this subtype.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("[{0}]", this.Name);
        }
    }
}
