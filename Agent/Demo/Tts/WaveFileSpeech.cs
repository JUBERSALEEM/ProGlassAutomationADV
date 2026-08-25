using NAudio.Wave;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Tts
{
    /// <summary>Writes spoken narration to a real WAV using Windows SAPI.</summary>
    public static class WaveFileSpeech
    {
        public static Task<TimeSpan> SpeakToWavAsync(
            string text, string wavPath, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                string spoken = Clean(text);
                if (spoken.Length == 0)
                    return TimeSpan.Zero;

                Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);
                TimeSpan dur = SpeakOnce(spoken, wavPath);
                if (dur > TimeSpan.Zero) return dur;

                string ascii = ToAscii(spoken);
                if (!string.Equals(ascii, spoken, StringComparison.Ordinal)
                    && ascii.Length > 0)
                {
                    dur = SpeakOnce(ascii, wavPath);
                    if (dur > TimeSpan.Zero) return dur;
                }

                return WavDuration(wavPath);
            }, ct);
        }

        private static TimeSpan SpeakOnce(string spoken, string wavPath)
        {
            try
            {
                if (File.Exists(wavPath))
                {
                    try { File.Delete(wavPath); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WaveFileSpeech] SpeakOnce: overwrite below: " + ex.Message); }
                }

                using var synth = new SpeechSynthesizer();
                SelectEnglish(synth);
                synth.Rate = 0;
                synth.Volume = 90;
                synth.SetOutputToWaveFile(wavPath);
                try { synth.Speak(spoken); }
                finally { try { synth.SetOutputToNull(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } }

                return WavDuration(wavPath);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        private static void SelectEnglish(SpeechSynthesizer synth)
        {
            try
            {
                InstalledVoice? en = null;
                foreach (var v in synth.GetInstalledVoices())
                {
                    if (!v.Enabled) continue;
                    var name = v.VoiceInfo.Culture?.Name ?? "";
                    if (!name.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                        continue;
                    en = v;
                    if (name.StartsWith("en-US", StringComparison.OrdinalIgnoreCase))
                        break;
                }
                if (en != null) synth.SelectVoice(en.VoiceInfo.Name);
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WaveFileSpeech] SelectEnglish: default voice: " + ex.Message); }
        }

        /// <summary>
        /// Real duration of a WAV. SAPI often writes a fact/LIST chunk
        /// before data, so a 44-byte header read is wrong and used to
        /// report zero — which aborted the whole demo.
        /// </summary>
        public static TimeSpan WavDuration(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return TimeSpan.Zero;

            try
            {
                using var reader = new WaveFileReader(path);
                if (reader.TotalTime > TimeSpan.Zero)
                    return reader.TotalTime;
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WaveFileSpeech] WavDuration: fall through to chunk walk: " + ex.Message); }

            return DurationFromChunks(path);
        }

        public static TimeSpan DurationFromChunks(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length < 44) return TimeSpan.Zero;
                if (bytes[0] != 'R' || bytes[8] != 'W') return TimeSpan.Zero;

                int byteRate = 0;
                int dataSize = 0;
                int i = 12;
                while (i + 8 <= bytes.Length)
                {
                    string id = Encoding.ASCII.GetString(bytes, i, 4);
                    int size = BitConverter.ToInt32(bytes, i + 4);
                    if (size < 0 || i + 8 + size > bytes.Length) break;
                    if (id == "fmt " && size >= 16)
                        byteRate = BitConverter.ToInt32(bytes, i + 8 + 8);
                    else if (id == "data")
                        dataSize = size;
                    i += 8 + size;
                    if ((size & 1) == 1) i++;
                }

                if (byteRate <= 0 || dataSize <= 0) return TimeSpan.Zero;
                return TimeSpan.FromSeconds(dataSize / (double)byteRate);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        public static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var s = text
                .Replace("—", ", ")
                .Replace("–", ", ")
                .Replace("−", "-")
                .Replace("“", "\"")
                .Replace("”", "\"")
                .Replace("‘", "'")
                .Replace("’", "'")
                .Replace("**", "")
                .Replace("\n", " ")
                .Replace("\r", " ");
            while (s.Contains("  ", StringComparison.Ordinal))
                s = s.Replace("  ", " ");
            return s.Trim();
        }

        private static string ToAscii(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (ch <= 127) sb.Append(ch);
                else if (char.IsWhiteSpace(ch)) sb.Append(' ');
            }
            return sb.ToString().Trim();
        }
    }
}
