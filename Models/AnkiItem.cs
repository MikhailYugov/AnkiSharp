using System;
using AnkiSharp.Models;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace AnkiSharp
{
    public class AnkiItem : DynamicObject
    {
        #region FIELDS

        Dictionary<string, object> _dictionary = new Dictionary<string, object>();
        #endregion

        #region PROPERTIES
        public object this[string elem] => _dictionary[elem];

        public int Count => _dictionary.Count;
        public Dictionary<string, object>.KeyCollection Keys => _dictionary.Keys;

        public string Mid { get; set; } = "";
        #endregion

        #region CTOR
        public AnkiItem(FieldList fields, params string[] properties)
        {
            for (int i = 0; i < properties.Length; ++i)
            {
                _dictionary[fields[i].Name] = properties[i].Replace("'", "’");
            }
        }
        #endregion

        #region FUNCTIONS
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            string name = binder.Name.ToLower();
            
            return _dictionary.TryGetValue(name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _dictionary[binder.Name.ToLower()] = value;
            
            return true;
        }
        public void Set(string key, string value)
        {
            if (_dictionary.ContainsKey(key))
            {
                _dictionary[key] = value;
            }
        }

        public static bool operator ==(AnkiItem first, AnkiItem second)
        {
            foreach (var pair in first._dictionary)
            {
                if (second._dictionary.ContainsKey(pair.Key) && second._dictionary[pair.Key].ToString() != pair.Value.ToString())
                    return false;
            }

            return true;
        }

        public static bool operator !=(AnkiItem first, AnkiItem second)
        {
            return !(first == second);
        }
        #endregion
    }
}
