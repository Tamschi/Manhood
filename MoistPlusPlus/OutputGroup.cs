using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manhood
{
    class OutputGroup
    {
        public string Name;
        public int Start, End;
        public GroupVisibility Visibility;

        public OutputGroup(string name, int start, int end)
        {
            if (name.StartsWith("."))
            {
                name = name.Substring(1);
                this.Visibility = GroupVisibility.Private;
            }
            else if (name.StartsWith("_"))
            {
                name = name.Substring(1);
                this.Visibility = GroupVisibility.Internal;
            }
            else
            {
                this.Visibility = GroupVisibility.Public;
            }
            this.Name = name;
            this.Start = start;
            this.End = end;
        }
    }

    enum GroupVisibility
    {
        Public,
        Internal,
        Private
    }
}
