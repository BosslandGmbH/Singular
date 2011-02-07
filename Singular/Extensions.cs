using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Singular
{
    static class Extensions
    {
        public static string CamelToSpaced(this string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var c in str)
            {
                if (char.IsUpper(c))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
