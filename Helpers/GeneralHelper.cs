using AnkiSharp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AnkiSharp.Helpers
{
    internal static class GeneralHelper
    {
        internal static Dictionary<string, string> extensionTag = new Dictionary<string, string>()
        {
            { ".mp3", "[sound:{0}]" },
            { ".gif", "<img src=\"{0}\"/>" }
        };

        internal static string ConcatFields(FieldList flds, AnkiItem item, string separator, MediaInfo info)
        {
            var matchedFields = (from t in flds select item[t.Name]).ToArray();

            if (info == null) return String.Join(separator, matchedFields);

            if (info.FrontTextField != null)
            {
                int indexOfFrontAudioField = Array.IndexOf(item.Keys.ToArray(), info.FrontAudioField);
                if (indexOfFrontAudioField != -1)
                    matchedFields[indexOfFrontAudioField] = String.Format(extensionTag[info.Extension], matchedFields[indexOfFrontAudioField]);
            }

            if (info.BackTextField != null)
            {
                int indexOfBackAudioField = Array.IndexOf(item.Keys.ToArray(), info.BackAudioField);
                if (indexOfBackAudioField != -1)
                    matchedFields[indexOfBackAudioField] = String.Format(extensionTag[info.Extension], matchedFields[indexOfBackAudioField]);
            }

            return String.Join(separator, matchedFields);
        }

        internal static string ReadResource(string path)
        {
            return new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(path)).ReadToEnd();
        }

        internal static string CheckSum(string sfld)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var l = sfld.Length >= 9 ? 8 : sfld.Length;
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(sfld));
                var sb = new StringBuilder(hash.Length);

                foreach (byte b in hash)
                {
                    sb.Append(b.ToString());
                }

                return sb.ToString().Substring(0, 10);
            }
        }

    }
}
