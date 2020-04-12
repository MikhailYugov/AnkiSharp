using AnkiSharp.Helpers;
using AnkiSharp.Models;
using NAudio.MediaFoundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Info = System.Tuple<string, string, AnkiSharp.Models.FieldList>;

namespace AnkiSharp
{
    public class Anki
    {
        private SQLiteConnection _conn;
        private string _name;
        private Assembly _assembly;
        private string _path;
        private string _collectionFilePath;
        private List<AnkiItem> _ankiItems;
        private Queue<CardMetadata> _cardsMetadatas;
        private List<RevLogMetadata> _revLogMetadatas;
        private OrderedDictionary _infoPerMid;
        public string ModelId { get; set; }
        public string UniqueField { get; set; }
        public MediaInfo MediaInfo { get; set; }
        public IpaInfo IpaInfo { get; set; }

        public Anki(string name, string path = null)
        {
            _cardsMetadatas = new Queue<CardMetadata>();
            _revLogMetadatas = new List<RevLogMetadata>();

            _assembly = Assembly.GetExecutingAssembly();

            _path = path ?? Path.Combine(Path.GetDirectoryName(_assembly.Location) ?? throw new InvalidOperationException(), "tmp");

            if (Directory.Exists(_path) == false)
                Directory.CreateDirectory(_path);

            Init(_path, name);
        }

        public Anki(string name, ApkgFile file)
        {
            _cardsMetadatas = new Queue<CardMetadata>();
            _revLogMetadatas = new List<RevLogMetadata>();

            _assembly = Assembly.GetExecutingAssembly();
            _path = Path.Combine(Path.GetDirectoryName(_assembly.Location) ?? throw new InvalidOperationException(), "tmp");

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

        public void CreateApkgFile(string path)
        {
            CreateDbFile();

            CreateMediaFile();

            CreateIpa();

            ExecuteSqLiteCommands();

            CreateZipFile(path);
        }

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

        public bool ContainsItem(AnkiItem item)
        {
            int matching = 1 + _ankiItems.Count(ankiItem => item == ankiItem);

            return matching == item.Count;
        }

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
            Collection collection = new Collection(_infoPerMid, _ankiItems, _name, ModelId);

            SQLiteHelper.ExecuteSQLiteCommand(_conn, collection.SqlQuery, collection.SqlParameters);

            return collection.DeckId;
        }

        private void CreateNotesAndCards(string deckId, Anki anki = null)
        {
            Anki currentAnki = anki ?? (this);
            foreach (var ankiItem in currentAnki._ankiItems)
            {
                Note note = new Note(currentAnki._infoPerMid, ankiItem, UniqueField);

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
                if (MediaInfo != null)
                {
                    foreach (var item in _ankiItems.Where(item => MediaInfo.Extension == ".mp3"))
                    {
                        var fileNameFront = CreateAudio(j, item, "F");
                        if (fileNameFront != String.Empty)
                        {
                            obj[j.ToString()] = fileNameFront;
                            j++;
                        }
                        var fileNameBack = CreateAudio(j, item, "B");
                        if (fileNameBack != String.Empty)
                        {
                            obj[j.ToString()] = fileNameBack;
                            j++;
                        }
                    }
                }

                var serialized = JsonConvert.SerializeObject(obj);
                byte[] info = new UTF8Encoding(true).GetBytes(serialized);
                fs.Write(info, 0, info.Length);
                fs.Close();
            }
        }

        private string CreateAudio(int j, AnkiItem item, string type)
        {
            const string audioTemplate = "[sound:{0}]";
            const string filePrefix = "DeckHelper-";

            string textField = MediaInfo.FrontTextField;
            string audioField = MediaInfo.FrontAudioField;
            CultureInfo cultureInfo = MediaInfo.FrontCultureInfo;

            if (type == "B")
            {
                textField = MediaInfo.BackTextField;
                audioField = MediaInfo.BackAudioField;
                cultureInfo = MediaInfo.BackCultureInfo;
            }

            if (textField == null || audioField == null || cultureInfo == null) return String.Empty;

            SynthetizerHelper.CreateAudio(Path.Combine(_path, j.ToString()),
                GeneralHelper.ProcessString(item[textField].ToString()), cultureInfo, MediaInfo.Bitrate);

            var fileName = filePrefix + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + MediaInfo.Extension;

            int indexOfAudioField = Array.IndexOf(item.Keys.ToArray(), audioField);
            if (indexOfAudioField != -1)
            {
                item.Set(audioField, String.Format(audioTemplate, fileName));
            }

            return fileName;
        }

        private void CreateIpa()
        {
            if (IpaInfo == null) return;

            foreach (var item in _ankiItems)
            {

                var ipaFront = IpaHelper.CreateIpa(
                    GeneralHelper.ProcessString(item[IpaInfo.FrontTextField].ToString()),
                    IpaInfo.FrontCultureInfo
                    );
                item.Set(IpaInfo.FrontIpaField, ipaFront);
                
                var ipaBack = IpaHelper.CreateIpa(
                    GeneralHelper.ProcessString(item[IpaInfo.BackTextField].ToString()),
                    IpaInfo.BackCultureInfo
                    );
                item.Set(IpaInfo.BackIpaField, ipaBack);
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
