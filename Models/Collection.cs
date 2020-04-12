using AnkiSharp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Info = System.Tuple<string, string, AnkiSharp.Models.FieldList>;

namespace AnkiSharp.Models
{
    internal class Collection
    {
        private const long Id = 1;
        internal string SqlQuery { get; }
        internal SQLiteParameter[] SqlParameters { get; }
        internal string DeckId { get; }
        
        public Collection(OrderedDictionary infoPerMid, List<AnkiItem> ankiItems, string name, string modelId = null)
        {
            
            if (modelId == null)
            {
                modelId = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            }

            var mod = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();

            var crt = GetDayStart();
            
            var confFileContent = GeneralHelper.ReadResource("AnkiSharp.AnkiData.conf.json");
            var conf = confFileContent.Replace("{MODEL}", modelId).Replace("\r\n", "");
            DeckId = name.GetHashCode().ToString();
            
            var modelsFileContent = GeneralHelper.ReadResource("AnkiSharp.AnkiData.models.json").Replace("{MOD}", mod);

            var models = new StringBuilder();

            var alreadyAdded = new List<string>();

            foreach (var key in infoPerMid.Keys.Cast<string>().ToList())
            {
                var (item1, item2, item3) = ((Info) infoPerMid[key]);

                if (alreadyAdded.Contains(item3.ToJson().Replace("hint:", "").Replace("type:", "")))
                    continue;

                if (models.Length > 0)
                    models.Append(", ");

                if (key == "DEFAULT")
                {
                    var newEntry = infoPerMid["DEFAULT"];

                    infoPerMid.Add(modelId, newEntry);
                    ankiItems.ForEach(x => x.Mid = x.Mid == "DEFAULT" ? modelId : x.Mid);
                    models.Append(modelsFileContent.Replace("{MID}", modelId));
                }
                else
                    models.Append(modelsFileContent.Replace("{MID}", key));

                models = models.Replace("{CSS}", JsonConvert.ToString(item2));
                models = models.Replace("{ID_DECK}", DeckId);

                var json = item3.ToJson();
                
                models = models.Replace("{FLDS}", json.Replace("hint:", "").Replace("type:", ""));
                alreadyAdded.Add(json.Replace("hint:", "").Replace("type:", ""));

                var format = item1 != "" ? item3.Format(item1) : item3.ToFrontBack();

                var qfmt = Regex.Split(format, "<hr id=answer(.*?)>")[0];
                var afmt = format;

                afmt = afmt.Replace(qfmt, "{{FrontSide}}\n");
                models = models.Replace("{QFMT}", JsonConvert.ToString(qfmt)).Replace("{AFMT}", JsonConvert.ToString(afmt)).Replace("\r\n", "");
            }

            var deckFileContent = GeneralHelper.ReadResource("AnkiSharp.AnkiData.decks.json");
            var decks = deckFileContent.Replace("{NAME}", name).Replace("{ID_DECK}", DeckId).Replace("{MOD}", mod).Replace("\r\n", "");

            var dconfFileContent = GeneralHelper.ReadResource("AnkiSharp.AnkiData.dconf.json");
            var dconf = dconfFileContent.Replace("\r\n", "");

            SqlQuery = @"INSERT INTO col VALUES(@id, @crt, @mod1, @mod2, 11, 0, 0, 0, @conf, @models, @decks, @dconf, '{}');";
            SQLiteParameter[] parameters =
            {
                new SQLiteParameter("id", Id),
                new SQLiteParameter("crt", crt),
                new SQLiteParameter("mod1", mod),
                new SQLiteParameter("mod2", mod),
                new SQLiteParameter("conf", conf),
                new SQLiteParameter("models", "{" + models + "}"),
                new SQLiteParameter("decks", decks),
                new SQLiteParameter("dconf", dconf),
            };
            SqlParameters = parameters;
        }

        private long GetDayStart()
        {
            var dateOffset = DateTimeOffset.Now;
            TimeSpan fourHoursSpan = new TimeSpan(4, 0, 0);
            dateOffset = dateOffset.Subtract(fourHoursSpan);
            dateOffset = new DateTimeOffset(dateOffset.Year, dateOffset.Month, dateOffset.Day,
                                            0, 0, 0, dateOffset.Offset);
            dateOffset = dateOffset.Add(fourHoursSpan);
            return dateOffset.ToUnixTimeSeconds();
        }
    }
}
