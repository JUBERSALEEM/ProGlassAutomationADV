using ProGlassAutomation.Agent.Demo.Tts;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class WaveFileSpeechTests
    {
        [Fact]
        public void Duration_reads_sapi_style_wav_with_fact_chunk()
        {
            string path = Path.Combine(Path.GetTempPath(), "pg-fact-" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                WritePcmWithFact(path, sampleRate: 22050, dataBytes: 22050 * 2);
                var dur = WaveFileSpeech.WavDuration(path);
                Assert.True(dur.TotalSeconds > 0.9 && dur.TotalSeconds < 1.1,
                    "duration was " + dur.TotalSeconds);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void Naive_44_byte_read_would_be_zero_on_fact_chunk()
        {
            string path = Path.Combine(Path.GetTempPath(), "pg-fact0-" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                WritePcmWithFact(path, sampleRate: 22050, dataBytes: 22050 * 2);
                var bytes = File.ReadAllBytes(path);
                int naiveDataSize = BitConverter.ToInt32(bytes, 40);
                Assert.True(naiveDataSize == 4 || naiveDataSize == 0 || naiveDataSize != 22050 * 2,
                    "fixture must not look like a 44-byte PCM header");
                Assert.True(WaveFileSpeech.DurationFromChunks(path).TotalSeconds > 0.5);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void Clean_strips_em_dash_so_sapi_gets_plain_english()
        {
            string cleaned = WaveFileSpeech.Clean("Al Zahia Tower — Podium Glazing");
            Assert.DoesNotContain("—", cleaned);
            Assert.Contains("Podium", cleaned);
        }

        [Fact]
        public void Missing_file_is_zero_not_an_exception()
        {
            Assert.Equal(TimeSpan.Zero, WaveFileSpeech.WavDuration(
                Path.Combine(Path.GetTempPath(), "no-such-pg-wav.wav")));
        }

        private static void WritePcmWithFact(string path, int sampleRate, int dataBytes)
        {
            int fmtSize = 16;
            int factSize = 4;
            int riffSize = 4 + (8 + fmtSize) + (8 + factSize) + (8 + dataBytes);
            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: false);
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(riffSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(fmtSize);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("fact"));
            bw.Write(factSize);
            bw.Write(dataBytes / 2);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataBytes);
            bw.Write(new byte[dataBytes]);
        }
    }
}
