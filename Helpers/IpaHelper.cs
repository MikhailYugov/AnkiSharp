using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace AnkiSharp.Helpers
{
    internal static class IpaHelper
    {
        private static readonly List<KeyValuePair<string, XDocument>> Xmls = new List<KeyValuePair<string, XDocument>>();

        public static string CreateIpa(string text, string cultureInfo)
        {
            var ipaDocument = GetIpaDocument(cultureInfo);

            var ipaText = GetWordIpa(ipaDocument, text);
            if (ipaText != String.Empty) return ipaText;

            var wordList = GetWordList(text);
            return wordList.Aggregate(ipaText, (current, word) => current + " " + GetWordIpa(ipaDocument, word));
        }

        private static string GetAssemblyPath()
        {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        private static XDocument GetIpaDocument(string cultureInfo)
        {
            var xDocument = Xmls.Where(v => v.Key == cultureInfo).Select(x => x.Value).FirstOrDefault();
            if (xDocument != null)
            {
                return xDocument;
            }

            var ipaDirectory = GetAssemblyPath() + "\\IPA\\";
            if (!Directory.Exists(ipaDirectory)) return new XDocument();

            string ipaFileName = cultureInfo + ".xml";
            var ipaFilePath = Path.Combine(ipaDirectory, ipaFileName);
            xDocument = !File.Exists(ipaFilePath) ? new XDocument() : XDocument.Load(ipaFilePath);
            Xmls.Add(new KeyValuePair<string, XDocument>(cultureInfo, xDocument));

            return xDocument;
        }

        private static IEnumerable<string> GetWordList(string text)
        {
            char[] delimiterChars = { ' ', ',', '.', ':', '\t' };
            var words = text.Split(delimiterChars).Select(x => x.Trim()).Where(x => x != "").ToList();
            return words.ToList();
        }

        private static string GetWordIpa(XContainer ipaDocument, string word)
        {
            var wordIpa = ipaDocument.Element("IPADict")
                ?.Element("WordList")
                ?.Elements("IpaEntry")
                .Where(d => d.Element("Item")?.Value.ToString() == word)
                .Select(d => d.Element("Ipa")?.Value.ToString())
                .DefaultIfEmpty(String.Empty).FirstOrDefault();
            return wordIpa;
        }
    }
}
