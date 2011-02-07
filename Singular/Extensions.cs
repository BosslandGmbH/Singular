using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Singular
{
    static class Extensions
    {
        /// <summary>A string extension method that turns a Camel-case string into a spaced string. (Example: SomeCamelString -> Some Camel String)</summary>
        /// <remarks>Created 2/7/2011.</remarks>
        /// <param name="str">The string to act on.</param>
        /// <returns>.</returns>
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
