using AnkiSharp.Helpers;
using System;
using System.Collections;
using System.Data.SQLite;
using Info = System.Tuple<string, string, AnkiSharp.Models.FieldList>;

namespace AnkiSharp.Models
{
    internal class Note
    {
        internal long Id { private set; get; }
        //internal string Query { private set; get; }
        internal string SqlQuery { private set; get; }
        internal SQLiteParameter[] SqlParameters { private set; get; }

        public Note(IDictionary infoPerMid, MediaInfo mediaInfo, AnkiItem ankiItem)
        {
            var fields = ((Info) infoPerMid[ankiItem.Mid]).Item3;
            Id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var guid = ((ShortGuid)Guid.NewGuid()).ToString().Substring(0, 10);
            var mid = ankiItem.Mid;
            var mod = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var flds = GeneralHelper.ConcatFields(fields, ankiItem, "\x1f", mediaInfo);
            var sfld = ankiItem[fields[0].Name].ToString();
            var csum = GeneralHelper.CheckSum(sfld);
            
            SqlQuery = @"INSERT INTO notes VALUES(@id, @guid, @mid, @mod, -1, '  ', @flds, @sfld, @csum, 0, '');";
            SQLiteParameter[] parameters =
            {
                new SQLiteParameter("id", Id),
                new SQLiteParameter("guid", guid),
                new SQLiteParameter("mid", mid),
                new SQLiteParameter("mod", mod),
                new SQLiteParameter("flds", flds),
                new SQLiteParameter("sfld", sfld),
                new SQLiteParameter("csum", csum),
            };
            SqlParameters = parameters;
        }
    }
}
