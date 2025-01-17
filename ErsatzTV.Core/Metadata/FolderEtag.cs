﻿using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ErsatzTV.Core.Interfaces.Metadata;
using static LanguageExt.Prelude;

namespace ErsatzTV.Core.Metadata
{
    public static class FolderEtag
    {
        private static readonly MD5CryptoServiceProvider Crypto = new();

        public static string Calculate(string folder, ILocalFileSystem localFileSystem)
        {
            IEnumerable<string> allFiles = localFileSystem.ListFiles(folder);

            var sb = new StringBuilder();
            foreach (string file in allFiles.OrderBy(identity))
            {
                sb.Append(file);
                sb.Append(localFileSystem.GetLastWriteTime(file).Ticks);
            }

            var hash = new StringBuilder();
            byte[] bytes = Crypto.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            foreach (byte t in bytes)
            {
                hash.Append(t.ToString("x2"));
            }

            return hash.ToString();
        }
    }
}
