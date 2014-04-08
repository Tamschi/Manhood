using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyIO;

namespace Manhood
{
    public class Definition
    {
        private DefinitionType _type;
        private string _name, _body;

        public DefinitionType Type
        {
            get { return _type; }
            set { _type = value; }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public string Body
        {
            get { return _body; }
            set { _body = value; }
        }

        public Definition(DefinitionType type, string name, string body)
        {
            _type = type;
            _name = name;
            _body = body;
        }

        public static Definition FromStream(EasyReader reader)
        {
            var type = reader.ReadEnum<DefinitionType>();
            string name = reader.ReadString();
            string body = reader.ReadString();
            return new Definition(type, name, body);
        }

        public void WriteToStream(EasyWriter writer)
        {
            writer.Write(_type).Write(_name).Write(_body);
        }
    }

    public enum DefinitionType : byte
    {
        Macro = 0x00,
        Global = 0x01
    }
}
