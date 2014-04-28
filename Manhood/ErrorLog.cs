using System;
using System.Collections.Generic;

namespace Manhood
{
    /// <summary>
    /// Stores a collection of Manhood interpreter errors, as well as a copy of the expanded pattern to which the errors pertain.
    /// </summary>
    public class ErrorLog : IEnumerable<Error>
    {
        private readonly List<Error> _errorLog;
        private string _patternOrig;
        private string _patternExp;
        private string[] _patternExpLines, _patternOrigLines;

        internal ErrorLog(string patternOriginal, string patternExpanded)
        {
            _errorLog = new List<Error>();
            _patternOrig = patternOriginal;
            _patternExp = patternExpanded;
            _patternExpLines = patternExpanded.Split(new[] { '\n' });
            _patternOrigLines = patternOriginal.Split(new[] { '\n' });
        }

        /// <summary>
        /// Gets the original pattern passed to the interpreter with all definitions unfolded.
        /// </summary>
        public string PatternExpanded
        {
            get { return _patternExp; }
            internal set
            {
                _patternExp = value;
                _patternExpLines = _patternExp.Split(new[] { '\n' });
            }
        }

        /// <summary>
        /// Gets a string array containing the separate lines of the PatternExpanded property.
        /// </summary>
        public string[] PatternExpandedLines
        {
            get { return _patternExpLines; }
        }

        /// <summary>
        /// Gets the original pattern passed to the interpreter.
        /// </summary>
        public string PatternOriginal
        {
            get { return _patternOrig; }
            internal set
            {
                _patternOrig = value;
                _patternOrigLines = _patternOrig.Split(new[] { '\n' });
            }
        }

        internal void AddFromState(ErrorType type, int index, string msg, params object[] args)
        {
            int col;
            var line = _patternExp.GetLineNumberFromIndex(index, out col);
            _errorLog.Add(new Error(type, line, col, index, msg, args));
        }

        /// <summary>
        /// Gets a visual representation of where the error occurs in the expanded pattern, with a caret pointing to the problematic part of the line in question.
        /// </summary>
        /// <param name="index">The index of the error to process.</param>
        /// <returns></returns>
        public string GetVisualError(int index)
        {
            var error = this[index];
            var line = error.Type == ErrorType.Interpreter ? _patternExpLines[error.Line] : _patternOrigLines[error.Line];
            var arrow = new string(' ', error.Column) + "^";
            return String.Concat(error.Message, "\r\n", line, "\r\n", arrow);
        }

        /// <summary>
        /// Gets a visual representation of where the error occurs in the expanded pattern, with a caret pointing to the problematic part of the line in question.
        /// </summary>
        /// <param name="error">The error to process.</param>
        /// <returns></returns>
        public string GetVisualError(Error error)
        {
            if (!_errorLog.Contains(error))
            {
                throw new KeyNotFoundException("The specified error does not exist in the log.");
            }
            return GetVisualError(_errorLog.IndexOf(error));
        }

        #region Collection implementation

        internal void Add(Error item)
        {
            _errorLog.Add(item);
        }

        internal void Clear()
        {
            _errorLog.Clear();
        }

        /// <summary>
        /// Determines whether an element exists within the log.
        /// </summary>
        /// <param name="item">The item to search for.</param>
        /// <returns></returns>
        public bool Contains(Error item)
        {
            return _errorLog.Contains(item);
        }

        /// <summary>
        /// Copies the contents of the log to an array, starting at the specified index.
        /// </summary>
        /// <param name="array">The array to copy to.</param>
        /// <param name="arrayIndex">The index at which to begin copying items into the array.</param>
        public void CopyTo(Error[] array, int arrayIndex)
        {
            _errorLog.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the number of items contained in the error log.
        /// </summary>
        public int Count
        {
            get { return _errorLog.Count; }
        }

        internal bool IsReadOnly
        {
            get { return false; }
        }

        internal bool Remove(Error item)
        {
            return _errorLog.Remove(item);
        }

        /// <summary>
        /// Returns an enumerator that iterates through this error log.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Error> GetEnumerator()
        {
            return _errorLog.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through this error log.
        /// </summary>
        /// <returns></returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _errorLog.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through this error log.
        /// </summary>
        /// <returns></returns>
        IEnumerator<Error> IEnumerable<Error>.GetEnumerator()
        {
            return _errorLog.GetEnumerator();
        }

        /// <summary>
        /// Searches for the specified item and returns a zero-based index of the first occurrence in the log. Returns -1 if not found.
        /// </summary>
        /// <param name="item">The item to search for.</param>
        /// <returns></returns>
        public int IndexOf(Error item)
        {
            return _errorLog.IndexOf(item);
        }

        internal void Insert(int index, Error item)
        {
            _errorLog.Insert(index, item);
        }

        internal void RemoveAt(int index)
        {
            _errorLog.RemoveAt(index);
        }

        /// <summary>
        /// Gets or sets the item at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to access.</param>
        /// <returns></returns>
        public Error this[int index]
        {
            get
            {
                return _errorLog[index];
            }
            internal set
            {
                _errorLog[index] = value;
            }
        }
        #endregion
    }
}
