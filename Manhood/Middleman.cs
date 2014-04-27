using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    /// <summary>
    /// Provides thread-safe resources for the Manhood engine.
    /// </summary>
    internal static class Middleman
    {
        [ThreadStatic]
        private static EngineState _stateObject = new EngineState();

        /// <summary>
        /// Gets the EngineState object for the current thread.
        /// </summary>
        public static EngineState State
        {
            get { return _stateObject; }
        }
    }
}
