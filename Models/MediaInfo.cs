using System.Globalization;
using System.Speech.AudioFormat;

namespace AnkiSharp
{
    public class MediaInfo
    {
        public CultureInfo FrontCultureInfo = null;
        public string FrontTextField = null;
        public string FrontAudioField = null;
        public CultureInfo BackCultureInfo = null;
        public string BackTextField = null;
        public string BackAudioField = null;
        public string Extension = ".mp3";
        public int Bitrate = 96000;
    }
}
