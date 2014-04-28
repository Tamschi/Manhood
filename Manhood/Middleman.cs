using System;

namespace Manhood
{
    /// <summary>
    /// Provides thread-safe resources for the Manhood engine.
    /// </summary>
    internal static class Middleman
    {
        [ThreadStatic]
        private static EngineState _stateObject;

        /// <summary>
        /// Gets the EngineState object for the current thread.
        /// </summary>
        public static EngineState State
        {
            get { return _stateObject ?? (_stateObject = new EngineState()); }
        }
    }
}
