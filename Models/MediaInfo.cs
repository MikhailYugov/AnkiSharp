using System.Globalization;
using System.Speech.AudioFormat;

namespace AnkiSharp
{
    public class MediaInfo
    {
        public CultureInfo CultureInfo;
        public string Field;
        public string Extension = ".wav";
        public SpeechAudioFormatInfo AudioFormat = new SpeechAudioFormatInfo(8000, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
    }
}
