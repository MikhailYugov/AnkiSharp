using AnkiSharp.Helpers;
using AnkiSharp.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NAudio.MediaFoundation;
using Newtonsoft.Json;
using Info = System.Tuple<string, string, AnkiSharp.Models.FieldList>;

namespace AnkiSharp
{
    public class Anki
    {
        private SQLiteConnection _conn;

        private MediaInfo _mediaInfo;

        private string _name;
        private Assembly _assembly;
        private string _path;
        private string _collectionFilePath;

        private List<AnkiItem> _ankiItems;
        private Queue<CardMetadata> _cardsMetadatas;
        private List<RevLogMetadata> _revLogMetadatas;

        public string Mid { get; set; }
        public string Mod { get; set; }
        public string Did { get; set; }

        /// <summary>
        /// Key : string which represent Mid
        /// Value : Tuple string, string, FieldList which represent repectively the format, the css and the field list
        /// </summary>
        OrderedDictionary _infoPerMid;

        /// <summary>
        /// Creates a Anki object
        /// </summary>
        /// <param name="name">Specify the name of apkg file and deck</param>
        /// <param name="info"></param>
        /// <param name="path">Where to save your apkg file</param>
        public Anki(string name, MediaInfo info = null, string path = null)
        {
            _cardsMetadatas = new Queue<CardMetadata>();
            _revLogMetadatas = new List<RevLogMetadata>();

            _assembly = Assembly.GetExecutingAssembly();

            _mediaInfo = info;

            _path = path ?? Path.Combine(Path.GetDirectoryName(_assembly.Location) ?? throw new InvalidOperationException(), "tmp");

            if (Directory.Exists(_path) == false)
                Directory.CreateDirectory(_path);

            Init(_path, name);
        }

        /// <summary>
        /// Create anki object from an Apkg file
        /// </summary>
        /// <param name="name">Specify the name of apkg file and deck</param>
        /// <param name="file">Apkg file</param>
        /// <param name="info"></param>
        public Anki(string name, ApkgFile file, MediaInfo info = null)
        {
            _cardsMetadatas = new Queue<CardMetadata>();
            _revLogMetadatas = new List<RevLogMetadata>();

            _assembly = Assembly.GetExecutingAssembly();
            _path = Path.Combine(Path.GetDirectoryName(_assembly.Location) ?? throw new InvalidOperationException(), "tmp");

            _mediaInfo = info;

            if (Directory.Exists(_path) == false)
                Directory.CreateDirectory(_path);

            Init(_path, name);

            _collectionFilePath = Path.Combine(_path, "collection.db");

            ReadApkgFile(file.Path());
        }

        public void SetFields(params string[] values)
        {
            FieldList fields = new FieldList();

            foreach (var value in values)
            {
                if (value.Contains("hint:") || value.Contains("type:"))
                    continue;

                fields.Add(new Field(value));
            }

            var currentDefault = (Info)_infoPerMid["DEFAULT"];
            if (currentDefault == null) return;
            var newDefault = new Info(currentDefault.Item1, currentDefault.Item2, fields);

            _infoPerMid["DEFAULT"] = newDefault;
        }

        public void SetCss(string css)
        {
            var currentDefault = _infoPerMid["DEFAULT"] as Info;
            var newDefault = new Info(currentDefault.Item1, css, currentDefault.Item3);

            _infoPerMid["DEFAULT"] = newDefault;
        }

        public void SetFormat(string format)
        {
            var currentDefault = _infoPerMid["DEFAULT"] as Info;
            var newDefault = new Info(format, currentDefault.Item2, currentDefault.Item3);

            _infoPerMid["DEFAULT"] = newDefault;
        }

        /// <summary>
        /// Create a apkg file with all the words
        /// </summary>
        public void CreateApkgFile(string path)
        {
            CreateDbFile();

            CreateMediaFile();

            ExecuteSqLiteCommands();

            CreateZipFile(path);
        }

        /// <summary>
        /// Creates an AnkiItem and add it to the Anki object
        /// </summary>
        public void AddItem(params string[] properties)
        {
            var mid = "";
            IDictionaryEnumerator myEnumerator = _infoPerMid.GetEnumerator();

            while (myEnumerator.MoveNext())
            {
                if (!IsRightFieldList(((Info)myEnumerator.Value).Item3, properties)) continue;
                if (myEnumerator.Key != null) mid = myEnumerator.Key.ToString();
                break;
            }

            if (mid == "" || (_infoPerMid.Contains(mid) && properties.Length != (_infoPerMid[mid] as Info).Item3.Count))
                throw new ArgumentException("Number of fields provided is not the same as the one expected");

            AnkiItem item = new AnkiItem((_infoPerMid[mid] as Info).Item3, properties)
            {
                Mid = mid
            };

            if (ContainsItem(item) == true)
                return;

            _ankiItems.Add(item);
        }

        /// <summary>
        /// Add AnkiItem to the Anki object
        /// </summary>
        public void AddItem(AnkiItem item)
        {
            if (item.Mid == "")
                item.Mid = "DEFAULT";

            if (_infoPerMid.Contains(item.Mid) && item.Count != (_infoPerMid[item.Mid] as Info).Item3.Count)
                throw new ArgumentException("Number of fields provided is not the same as the one expected");
            if (ContainsItem(item) == true)
                return;

            _ankiItems.Add(item);
        }

        /// <summary>
        /// Tell if the anki object contains an AnkiItem (strict comparison)
        /// </summary>
        public bool ContainsItem(AnkiItem item)
        {
            int matching = 1 + _ankiItems.Count(ankiItem => item == ankiItem);

            return matching == item.Count;
        }

        /// <summary>
        /// Tell if the anki object contains an AnkiItem (user defined comparison)
        /// </summary>
        public bool ContainsItem(Func<AnkiItem, bool> comparison)
        {
            return _ankiItems.Any(ankiItem => comparison(ankiItem));
        }

        public AnkiItem CreateAnkiItem(params string[] properties)
        {
            FieldList list = null;
            IDictionaryEnumerator myEnumerator = _infoPerMid.GetEnumerator();

            while (myEnumerator.MoveNext())
            {
                if (!IsRightFieldList((myEnumerator.Value as Info).Item3, properties)) continue;
                list = (myEnumerator.Value as Info).Item3;
                break;
            }

            return new AnkiItem(list, properties);
        }

        private void CreateDbFile(string name = "collection.db")
        {
            _collectionFilePath = Path.Combine(_path, name);

            if (File.Exists(_collectionFilePath) == true)
                File.Delete(_collectionFilePath);

            File.Create(_collectionFilePath).Close();
        }

        private void Init(string path, string name)
        {
            _infoPerMid = new OrderedDictionary();
            _name = name;
            _ankiItems = new List<AnkiItem>();
            _assembly = Assembly.GetExecutingAssembly();

            _path = path;

            var css = GeneralHelper.ReadResource("AnkiSharp.AnkiData.CardStyle.css");
            var fields = new FieldList
            {
                new Field("Front"),
                new Field("Back")
            };

            _infoPerMid.Add("DEFAULT", new Info("", css, fields));
        }

        private static bool IsRightFieldList(FieldList list, string[] properties)
        {
            if (list.Count != properties.Length)
                return false;

            return true;
        }

        private void CreateZipFile(string path)
        {
            string anki2FilePath = Path.Combine(_path, "collection.anki2");
            string mediaFilePath = Path.Combine(_path, "media");

            if (File.Exists(anki2FilePath) == true)
                File.Delete(anki2FilePath);

            File.Move(_collectionFilePath, anki2FilePath);
            string zipPath = Path.Combine(path, _name + ".apkg");

            if (File.Exists(zipPath) == true)
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(_path, zipPath);

            File.Delete(anki2FilePath);
            File.Delete(mediaFilePath);

            int i = 0;
            string currentFile = Path.Combine(_path, i.ToString());

            while (File.Exists(currentFile))
            {
                File.Delete(currentFile);
                ++i;
                currentFile = Path.Combine(_path, i.ToString());
            }

        }

        private string CreateCol()
        {
            Collection collection = new Collection(_infoPerMid, _ankiItems, _name, Mid, Mod, Did);

            SQLiteHelper.ExecuteSQLiteCommand(_conn, collection.SqlQuery, collection.SqlParameters);

            return collection.DeckId;
        }

        private void CreateNotesAndCards(string deckId, Anki anki = null)
        {
            Anki currentAnki = anki ?? (this);
            foreach (var ankiItem in currentAnki._ankiItems)
            {
                Note note = new Note(currentAnki._infoPerMid, currentAnki._mediaInfo, ankiItem);

                SQLiteHelper.ExecuteSQLiteCommand(currentAnki._conn, note.SqlQuery, note.SqlParameters);

                Card card = new Card(_cardsMetadatas, note, deckId);

                SQLiteHelper.ExecuteSQLiteCommand(currentAnki._conn, card.SqlQuery, card.SqlParameters);
            }
        }

        private void AddRevlogMetadata()
        {
            if (_revLogMetadatas.Count == 0) return;

            foreach (var revlogMetadata in _revLogMetadatas)
            {
                const string sqlQuery = @"INSERT INTO revlog VALUES(@id, @cid, @usn, @ease, @ivl, @lastIvl, @factor, @time, @type)";
                SQLiteParameter[] sqlParameters =
                {
                    new SQLiteParameter("id", revlogMetadata.Id),
                    new SQLiteParameter("cid", revlogMetadata.Cid),
                    new SQLiteParameter("usn", revlogMetadata.Usn),
                    new SQLiteParameter("ease", revlogMetadata.Ease),
                    new SQLiteParameter("ivl", revlogMetadata.Ivl),
                    new SQLiteParameter("lastIvl", revlogMetadata.LastIvl),
                    new SQLiteParameter("factor", revlogMetadata.Factor),
                    new SQLiteParameter("time", revlogMetadata.Time),
                    new SQLiteParameter("type", revlogMetadata.Type),
                };
                SQLiteHelper.ExecuteSQLiteCommand(_conn, sqlQuery, sqlParameters);
            }
        }

        private void ExecuteSqLiteCommands(Anki anki = null)
        {
            try
            {
                _conn = new SQLiteConnection(@"Data Source=" + _collectionFilePath + ";Version=3;");
                _conn.Open();

                var column = GeneralHelper.ReadResource("AnkiSharp.SqLiteCommands.ColumnTable.txt");
                var notes = GeneralHelper.ReadResource("AnkiSharp.SqLiteCommands.NotesTable.txt");
                var cards = GeneralHelper.ReadResource("AnkiSharp.SqLiteCommands.CardsTable.txt");
                var revLogs = GeneralHelper.ReadResource("AnkiSharp.SqLiteCommands.RevLogTable.txt");
                var graves = GeneralHelper.ReadResource("AnkiSharp.SqLiteCommands.GravesTable.txt");
                var indexes = GeneralHelper.ReadResource("AnkiSharp.SqLiteCommands.Indexes.txt");

                SQLiteParameter[] parameters = { };
                SQLiteHelper.ExecuteSQLiteCommand(_conn, column, parameters);
                SQLiteHelper.ExecuteSQLiteCommand(_conn, notes, parameters);
                SQLiteHelper.ExecuteSQLiteCommand(_conn, cards, parameters);
                SQLiteHelper.ExecuteSQLiteCommand(_conn, revLogs, parameters);
                SQLiteHelper.ExecuteSQLiteCommand(_conn, graves, parameters);
                SQLiteHelper.ExecuteSQLiteCommand(_conn, indexes, parameters);

                var deckId = CreateCol();
                CreateNotesAndCards(deckId, anki);

                AddRevlogMetadata();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            finally
            {
                _conn.Close();
                _conn.Dispose();
                SQLiteConnection.ClearAllPools();
            }
        }

        private void CreateMediaFile()
        {
            string mediaFilePath = Path.Combine(_path, "media");
            MediaFoundationApi.Startup();
            if (File.Exists(mediaFilePath))
                File.Delete(mediaFilePath);

            using (FileStream fs = File.Create(mediaFilePath))
            {
                int j = 0;
                var obj = new JObject();
                if (_mediaInfo != null)
                {
                    foreach (var item in _ankiItems.Where(item => _mediaInfo.Extension == ".mp3"))
                    {
                        if (_mediaInfo.FrontTextField != null && _mediaInfo.FrontAudioField != null && _mediaInfo.FrontCultureInfo != null)
                        {
                            var frontJ = j;
                            j++;
                            SynthetizerHelper.CreateAudio(
                                Path.Combine(_path, frontJ.ToString()),
                                item[_mediaInfo.FrontTextField].ToString(),
                                _mediaInfo.FrontCultureInfo,
                                _mediaInfo.Bitrate);
                            
                            //var fileNameFront = frontJ + _mediaInfo.Extension;
                            var fileNameFront = "DeckHelper" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _mediaInfo.Extension;
                            obj[frontJ.ToString()] = fileNameFront;
                            item.Set(_mediaInfo.FrontAudioField, fileNameFront);
                        }
                        if (_mediaInfo.BackTextField != null && _mediaInfo.FrontAudioField != null && _mediaInfo.BackCultureInfo != null)
                        {
                            var backJ = j;
                            j++;
                            SynthetizerHelper.CreateAudio(
                                Path.Combine(_path, backJ.ToString()),
                                item[_mediaInfo.BackTextField].ToString(),
                                _mediaInfo.BackCultureInfo,
                                _mediaInfo.Bitrate);

                            //var fileNameBack = backJ + _mediaInfo.Extension;
                            var fileNameBack = "DeckHelper" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _mediaInfo.Extension;
                            obj[backJ.ToString()] = fileNameBack;
                            item.Set(_mediaInfo.BackAudioField, fileNameBack);
                        }
                    }
                }

                var serialized = JsonConvert.SerializeObject(obj);
                byte[] info = new UTF8Encoding(true).GetBytes(serialized);
                fs.Write(info, 0, info.Length);
                fs.Close();
            }
        }

        private void ReadApkgFile(string path)
        {
            if (File.Exists(Path.Combine(_path, "collection.db")))
                File.Delete(Path.Combine(_path, "collection.db"));

            if (File.Exists(Path.Combine(_path, "media")))
                File.Delete(Path.Combine(_path, "media"));

            ZipFile.ExtractToDirectory(path, _path);

            string anki2File = Path.Combine(_path, "collection.anki2");

            File.Move(anki2File, _collectionFilePath);

            _conn = new SQLiteConnection(@"Data Source=" + _collectionFilePath + ";Version=3;");

            try
            {
                _conn.Open();

                var cardMetadatas = Mapper.MapSqLiteReader(_conn, "SELECT id, mod, type, queue, due, ivl, factor, reps, lapses, left, odue, odid FROM cards");

                foreach (var cardMetadata in cardMetadatas)
                {
                    _cardsMetadatas.Enqueue(cardMetadata.ToObject<CardMetadata>());
                }

                SQLiteDataReader reader = SQLiteHelper.ExecuteSQLiteCommandRead(_conn, "SELECT notes.flds, notes.mid FROM notes");
                List<double> mids = new List<double>();
                List<string[]> result = new List<string[]>();

                while (reader.Read())
                {
                    var splitted = reader.GetString(0).Split('\x1f');

                    var currentMid = reader.GetInt64(1);
                    if (mids.Contains(currentMid) == false)
                        mids.Add(currentMid);

                    result.Add(splitted);
                }

                reader.Close();
                reader = SQLiteHelper.ExecuteSQLiteCommandRead(_conn, "SELECT models FROM col");
                JObject models = null;

                while (reader.Read())
                {
                    models = JObject.Parse(reader.GetString(0));
                }

                AddFields(models, mids);

                reader.Close();

                var revLogMetadatas = Mapper.MapSqLiteReader(_conn, "SELECT * FROM revlog");

                foreach (var revLogMetadata in revLogMetadatas)
                {
                    _revLogMetadatas.Add(revLogMetadata.ToObject<RevLogMetadata>());
                }

                foreach (var res in result)
                {
                    AddItem(res);
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            finally
            {
                _conn.Close();
                _conn.Dispose();
                SQLiteConnection.ClearAllPools();
            }
        }

        private void AddFields(JObject models, IEnumerable<double> mids)
        {
            var regex = new Regex("{{hint:(.*?)}}|{{type:(.*?)}}|{{(.*?)}}");

            foreach (var mid in mids)
            {
                var qfmt = models["" + mid]["tmpls"].First["qfmt"].ToString().Replace("\"", "");
                var afmt = models["" + mid]["tmpls"].First["afmt"].ToString();
                var css = models["" + mid]["css"].ToString();

                afmt = afmt.Replace("{{FrontSide}}", qfmt);

                var matches = regex.Matches(afmt);
                FieldList fields = new FieldList();

                foreach (Match match in matches)
                {
                    if (match.Value.Contains("type:") || match.Value.Contains("hint:"))
                        continue;

                    var value = match.Value;
                    var field = new Field(value.Replace("{{", "").Replace("}}", ""));

                    fields.Add(field);
                }

                _infoPerMid.Add("" + mid, new Info(afmt.Replace("\n", "\\n"), css.Replace("\n", "\\n"), fields));
            }
        }
    }
}
