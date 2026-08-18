using System;
using System.IO;
using System.Speech.Synthesis;
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
                Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);
                using var synth = new SpeechSynthesizer();
                try
                {
                    var en = default(System.Speech.Synthesis.InstalledVoice);
                    foreach (var v in synth.GetInstalledVoices())
                    {
                        if (!v.Enabled) continue;
                        var name = v.VoiceInfo.Culture?.Name ?? "";
                        if (name.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                        {
                            en = v;
                            if (name.StartsWith("en-US", StringComparison.OrdinalIgnoreCase))
                                break;
                        }
                    }
                    if (en != null) synth.SelectVoice(en.VoiceInfo.Name);
                }
                catch { /* default voice */ }

                synth.Rate = 0;
                synth.Volume = 90;
                synth.SetOutputToWaveFile(wavPath);
                synth.Speak(Clean(text));
                synth.SetOutputToNull();

                return WavDuration(wavPath);
            }, ct);
        }

        public static TimeSpan WavDuration(string path)
        {
            if (!File.Exists(path)) return TimeSpan.Zero;
            using var fs = File.OpenRead(path);
            if (fs.Length < 44) return TimeSpan.Zero;
            var hdr = new byte[44];
            if (fs.Read(hdr, 0, 44) < 44) return TimeSpan.Zero;
            int byteRate = BitConverter.ToInt32(hdr, 28);
            int dataSize = BitConverter.ToInt32(hdr, 40);
            if (byteRate <= 0) return TimeSpan.Zero;
            return TimeSpan.FromSeconds(dataSize / (double)byteRate);
        }

        private static string Clean(string text) =>
            (text ?? "").Replace("**", "").Replace("\n", " ").Trim();
    }
}
