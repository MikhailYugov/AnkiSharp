using System.Globalization;
using System.IO;
using System.Speech.Synthesis;
using NAudio.Wave;

namespace AnkiSharp.Helpers
{
    internal static class SynthetizerHelper
    {
        public static void CreateAudio(string path, string text, CultureInfo cultureInfo, int bitrate)
        {
            using (SpeechSynthesizer reader = new SpeechSynthesizer())
            {
                reader.Volume = 100;
                reader.Rate = 0; //medium

                MemoryStream ms = new MemoryStream();
                reader.SetOutputToWaveStream(ms);

                PromptBuilder builder = new PromptBuilder(cultureInfo);
                builder.AppendText(text);

                reader.Speak(builder);
                ConvertWavStreamToMp3File(ref ms, path, bitrate);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(path + ".mp3", path);
            }
        }

        public static void ConvertWavStreamToMp3File(ref MemoryStream ms, string savetofilename, int bitrate)
        {
            //rewind to beginning of stream
            ms.Seek(0, SeekOrigin.Begin);

            using (var retMs = new MemoryStream())
            {
                using (var rdr = new WaveFileReader(ms))
                {
                    MediaFoundationEncoder.EncodeToMp3(rdr, savetofilename + ".mp3", bitrate);
                }
            }
        }
    }
}
