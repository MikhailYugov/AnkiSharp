using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace AnkiSharp.Models
{
    internal class Card
    {
        internal long Id { get; }
        internal string SqlQuery { get; }
        internal SQLiteParameter[] SqlParameters { get; }

        public Card(Queue<CardMetadata> cardsMetadatas, Note note, string deckId)
        {
            Id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var mod = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();

            if (cardsMetadatas.Count != 0)
            {
                CardMetadata metadata = cardsMetadatas.Dequeue();

                SqlQuery = @"INSERT INTO cards VALUES(@id, @noteId, @deckId, 0, @mod, -1, @type, @queue, @due, @ivl, @factor, @reps, @lapses, @left, @odue, @odid, 0, '');";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("id", metadata.Id),
                    new SQLiteParameter("noteId", note.Id),
                    new SQLiteParameter("deckId", deckId),
                    new SQLiteParameter("mod", metadata.Mod),
                    new SQLiteParameter("type", metadata.Type),
                    new SQLiteParameter("queue", metadata.Queue),
                    new SQLiteParameter("due", metadata.Due),
                    new SQLiteParameter("ivl", metadata.Ivl),
                    new SQLiteParameter("factor", metadata.Factor),
                    new SQLiteParameter("reps", metadata.Reps),
                    new SQLiteParameter("lapses", metadata.Lapses),
                    new SQLiteParameter("left", metadata.Left),
                    new SQLiteParameter("odue", metadata.Odue),
                    new SQLiteParameter("odid", metadata.Odid),
                };
                SqlParameters = parameters;
            }
            else
            {
                SqlQuery = @"INSERT INTO cards VALUES(@id, @noteId, @deckId, 0, @mod, -1, 0, 0, @noteId2, 0, 0, 0, 0, 0, 0, 0, 0, '');";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("id", Id),
                    new SQLiteParameter("noteId", note.Id),
                    new SQLiteParameter("deckId", deckId),
                    new SQLiteParameter("mod", mod),
                    new SQLiteParameter("noteId2", note.Id),
                };
                SqlParameters = parameters;
            }
        }
    }
}
