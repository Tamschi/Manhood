using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    class Output
    {
        public string Name;
        public int Start, End;
        public OutputVisibility Visibility;

        public Output(string name, int start, int end)
        {
            if (name.StartsWith("."))
            {
                name = name.Substring(1);
                this.Visibility = OutputVisibility.Private;
            }
            else if (name.StartsWith("_"))
            {
                name = name.Substring(1);
                this.Visibility = OutputVisibility.Internal;
            }
            else
            {
                this.Visibility = OutputVisibility.Public;
            }
            this.Name = name;
            this.Start = start;
            this.End = end;
        }
    }

    enum OutputVisibility
    {
        Public,
        Internal,
        Private
    }
}
