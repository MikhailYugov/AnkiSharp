using System;
using System.Collections.Generic;
using System.Linq;

namespace AnkiSharp.Models
{
    public class FieldList : List<Field>
    {
        #region CTOR

        public FieldList()
        {
        }

        #endregion

        #region FUNCTIONS
        public new void Add(Field field)
        {
            field.SetOrd(Count);
            base.Add(field);
        }

        public string ToJson()
        {
            var json = from field in FindAll(x => x != null)
                       select field.ToJson();

            return String.Join(",\n", json.ToArray());
        }

        public string ToFrontBack()
        {
            return String.Join("\n<hr id=answer />\n", (object[])ToArray());
        }

        public override string ToString()
        {
            return String.Join("\n<br>\n", (object[])ToArray());
        }

        public string Format(string format)
        {
            var array = ToArray();
            
            for (int i = 0; i < array.Length; ++i)
            {
                format = format.Replace("{" + i + "}", array[i].ToString());
            }

            return format;
        }
        #endregion
    }
}
