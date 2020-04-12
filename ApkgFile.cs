using System;
using System.IO;

namespace AnkiSharp
{
    public class ApkgFile
    {
        private string _path;

        public string Path()
        {
            return _path;
        }

        public ApkgFile(string path)
        {
            if (path.Contains(".apkg") == false)
                throw new Exception("Need apkg file");
            if (File.Exists(path) == false)
                throw new Exception("Need existing file");

            _path = path;
        }
    }
}
