using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ash3.ArsTurdgeBop {
    internal abstract record Instruction(string Code);

    internal record SendMessage(string Name, string Message, int PeerId) : Instruction("SendMessage");
}
