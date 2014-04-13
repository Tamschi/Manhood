using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manhood
{
    /// <summary>
    /// Privides case formatting options for words.
    /// </summary>
    public enum WordCase
    {
        /// <summary>
        /// No formatting.
        /// </summary>
        None,
        /// <summary>
        /// Capitalize the first letter of every word.
        /// </summary>
        Proper,
        /// <summary>
        /// Capitalize the first letter of the input.
        /// </summary>
        Capitalized,
        /// <summary>
        /// Capitalize every letter.
        /// </summary>
        AllCaps
    }
}
