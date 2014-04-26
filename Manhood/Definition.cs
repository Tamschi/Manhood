using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyIO;

namespace Manhood
{
    /// <summary>
    /// Represents a named code snippet.
    /// </summary>
    public class Definition
    {
        private DefinitionType _type;
        private string _name, _body, _state;
        private List<string> _params;

        /// <summary>
        /// Gets or sets the definition type.
        /// </summary>
        public DefinitionType Type
        {
            get { return _type; }
            set { _type = value; }
        }

        /// <summary>
        /// Gets or sets the name of the definition.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { _name = VerifyName(value); }
        }

        /// <summary>
        /// Gets or sets the definition body.
        /// </summary>
        public string Body
        {
            get { return _body; }
            set { _body = value; }
        }

        /// <summary>
        /// Gets or sets the parameters accepted by this definition (Macros only).
        /// </summary>
        public List<string> Parameters
        {
            get { return _params; }
            set { _params = value ?? new List<string>(); }
        }

        /// <summary>
        /// Gets or sets the current state of this definition (Globals only).
        /// </summary>
        public string State
        {
            get { return _state ?? "UNDEFINED"; }
            set { _state = value; }
        }

        /// <summary>
        /// Initializes a new instance of the Manhood.Definition class with the specified parameters.
        /// </summary>
        /// <param name="type">The definition type.</param>
        /// <param name="name">The name of the definition.</param>
        /// <param name="body">The definition body.</param>
        public Definition(DefinitionType type, string name, string body, List<string> parameters = null)
        {
            _type = type;
            _name = VerifyName(name);
            _body = body;
            _params = parameters ?? new List<string>();
        }

        /// <summary>
        /// Creates a new definition from a stream of bytes.
        /// </summary>
        /// <param name="reader">The stream to read from.</param>
        /// <returns></returns>
        public static Definition FromStream(EasyReader reader)
        {
            var type = reader.ReadEnum<DefinitionType>();
            string name = reader.ReadString();
            string body = reader.ReadString();
            return new Definition(type, name, body);
        }

        private static string VerifyName(string name)
        {
            if (!IsValidName(name))
            {
                throw new FormatException("Illegal def name. Definition names must be at least 1 character long and can only contain letters, numbers, dashes and underscores.");
            }
            return name;
        }

        /// <summary>
        /// Returns a boolean value indicating if the specified string would be valid as a definition name or parameter.
        /// </summary>
        /// <param name="name">The string to test.</param>
        /// <returns></returns>
        public static bool IsValidName(string name)
        {
            foreach (char c in name)
            {
                if (!Char.IsLetterOrDigit(c) && !"-_".Contains(c))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Writes the definition to a stream.
        /// </summary>
        /// <param name="writer">The stream to write to.</param>
        public void WriteToStream(EasyWriter writer)
        {
            writer.Write(_type).Write(_name).Write(_body);
        }

        /// <summary>
        /// Returns a string representation of the definition.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("{0} ({1})", this.Name, this.Type);
        }

        /// <summary>
        /// Returns an exact copy of this definition as a separate instance.
        /// </summary>
        /// <returns></returns>
        public Definition Clone()
        {
            return new Definition(this.Type, this.Name, this.Body, new List<string>(this.Parameters));
        }
    }

    /// <summary>
    /// Indicates definition type.
    /// </summary>
    public enum DefinitionType : byte
    {
        /// <summary>
        /// Macro definition. Reinterpreted every call.
        /// </summary>
        Macro = 0x00,
        /// <summary>
        /// Global definition. Interpreted manually, same output every call.
        /// </summary>
        Global = 0x01
    }
}
