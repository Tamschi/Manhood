using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    /// <summary>
    /// Represents a Manhood pattern.
    /// </summary>
    public class Pattern
    {
        private string _patternText, _title;

        /// <summary>
        /// Gets or sets the pattern's title.
        /// </summary>
        public string Title
        {
            get { return _title; }
            set { _title = value; }
        }

        /// <summary>
        /// Gets or sets the body of the pattern.
        /// </summary>
        public string Body
        {
            get { return _patternText; }
            set { _patternText = value; }
        }

        /// <summary>
        /// Initializes a new instance of the Manhood.Pattern class with the specified parameters.
        /// </summary>
        /// <param name="title">The title of the pattern.</param>
        /// <param name="patternText">The body of the pattern.</param>
        public Pattern(string title, string patternText)
        {
            _title = title;
            _patternText = patternText;
        }

        /// <summary>
        /// Returns a string representation of this pattern.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _title.Length == 0 ? "Untitled Pattern" : _title;
        }

        /// <summary>
        /// Returns an exact copy of this pattern as a separate instance.
        /// </summary>
        /// <returns></returns>
        public Pattern Clone()
        {
            return new Pattern(this.Title, this.Body);
        }
    }
}
