using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WireTools
{
    internal static class ColumnHeaders
    {
        public static (int Enum, string Text) Name => (0, "Name");
        public static (int Enum, string Text) NickName => (1, "Nickname");
        public static (int Enum, string Text) Groups => (2, "Groups");
        public static (int Enum, string Text) DrawIcon => (3, "Draw Icon");
        public static (int Enum, string Text) WireDisplay => (4, "Wire Display");
        public static (int Enum, string Text) Icon => (5, "");
    }
}
