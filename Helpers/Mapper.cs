
using AnkiSharp.Models;
using System.Collections.Generic;
using System.Data.SQLite;

namespace AnkiSharp.Helpers
{
    internal class Mapper
    {
        private static Mapper _instance;

        private Mapper()
        {
        }

        public static Mapper Instance => _instance ?? (_instance = new Mapper());

        public static List<AnkiSharpDynamic> MapSqLiteReader(SQLiteConnection conn, string toExecute)
        {
            List<AnkiSharpDynamic> result = new List<AnkiSharpDynamic>();
            SQLiteDataReader reader = SQLiteHelper.ExecuteSQLiteCommandRead(conn, toExecute);

            while (reader.Read())
            {
                AnkiSharpDynamic ankiSharpDynamic = new AnkiSharpDynamic();

                for (int i = 0; i < reader.FieldCount; ++i)
                {
                    ankiSharpDynamic[reader.GetName(i)] = reader.GetValue(i);
                }

                result.Add(ankiSharpDynamic);
            }

            reader.Close();
            return result;
        }
    }
}
