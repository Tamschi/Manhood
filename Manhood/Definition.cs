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
            set { _name = value; }
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
        /// Gets or sets the current state of this definition (Global use only).
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
        public Definition(DefinitionType type, string name, string body)
        {
            _type = type;
            _name = name;
            _body = body;
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
            return new Definition(this.Type, this.Name, this.Body);
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
