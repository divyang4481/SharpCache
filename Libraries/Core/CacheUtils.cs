using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Codeology.SharpCache
{

    internal static class CacheUtils
    {

        #region Methods

        public static string HashString(string value)
        {
            // Get string bytes
            byte[] buffer = Encoding.GetEncoding("ISO-8859-1").GetBytes(value);

            // Hash bytes using SHA-1
            SHA1Managed sha = new SHA1Managed();

            buffer = sha.ComputeHash(buffer);

            // Convert SHA-1 back to a string as hex
            StringBuilder builder = new StringBuilder();

            foreach(byte b in buffer) builder.Append(b.ToString("x2"));

            // Return
            return builder.ToString();
        }

        #endregion

    }

}
