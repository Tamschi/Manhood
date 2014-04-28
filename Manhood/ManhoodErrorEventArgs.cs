using System;

namespace Manhood
{
    /// <summary>
    /// Contains information related to the ManEngine.Errors event.
    /// </summary>
    public class ManhoodErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the error log passed from the engine.
        /// </summary>
        public ErrorLog Errors { get; private set; }

        internal ManhoodErrorEventArgs(ErrorLog log)
        {
            Errors = log;
        }
    }
}
