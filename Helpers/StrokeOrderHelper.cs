using System;
using System.IO;
using System.Net;

namespace AnkiSharp.Helpers
{
    internal static class StrokeOrderHelper
    {
        internal static string BaseUrl = "https://raw.githubusercontent.com/nmarley/chinese-char-animations/master/images/";

        internal static void DownloadImage(string path, string text)
        {
            var code = $"U+{(int) text[0]:x4}".Replace("U+", "");
            var url = Path.Combine(BaseUrl, code + ".gif");

            using (WebClient client = new WebClient())
            {
                client.DownloadFile(new Uri(url), path);
            }
        }
    }
}
