using System;
using System.Collections.Generic;
using System.IO;

namespace ProGlassAutomation.Agent.Demo.Tts
{
    /// <summary>
    /// Concatenates PCM WAV files that share the same format header.
    /// Used so ffmpeg receives one narration track timed to real speech.
    /// </summary>
    public static class WaveConcat
    {
        public static TimeSpan Concat(IReadOnlyList<string> wavPaths, string outputPath)
        {
            if (wavPaths == null || wavPaths.Count == 0)
                throw new InvalidOperationException("No WAV files to concatenate.");

            byte[]? fmt = null;
            int fmtLen = 0;
            var payloads = new List<byte[]>(wavPaths.Count);
            long dataBytes = 0;

            foreach (var path in wavPaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    throw new InvalidOperationException("Missing WAV: " + path);

                var bytes = File.ReadAllBytes(path);
                if (!TrySplit(bytes, out var thisFmt, out var data))
                    throw new InvalidOperationException("Not a PCM WAV: " + path);

                if (fmt == null)
                {
                    fmt = thisFmt;
                    fmtLen = thisFmt.Length;
                }
                else if (fmtLen != thisFmt.Length || !BytesEqual(fmt, thisFmt))
                {
                    throw new InvalidOperationException(
                        "WAV formats do not match — refusing to glue unlike audio.");
                }

                payloads.Add(data);
                dataBytes += data.Length;
            }

            if (fmt == null || dataBytes > int.MaxValue - 44)
                throw new InvalidOperationException("WAV payload is empty or too large.");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            int riffSize = 4 + (8 + fmt.Length) + (8 + (int)dataBytes);
            using var fs = File.Create(outputPath);
            using var bw = new BinaryWriter(fs);
            bw.Write((byte)'R'); bw.Write((byte)'I'); bw.Write((byte)'F'); bw.Write((byte)'F');
            bw.Write(riffSize);
            bw.Write((byte)'W'); bw.Write((byte)'A'); bw.Write((byte)'V'); bw.Write((byte)'E');
            bw.Write((byte)'f'); bw.Write((byte)'m'); bw.Write((byte)'t'); bw.Write((byte)' ');
            bw.Write(fmt.Length);
            bw.Write(fmt);
            bw.Write((byte)'d'); bw.Write((byte)'a'); bw.Write((byte)'t'); bw.Write((byte)'a');
            bw.Write((int)dataBytes);
            foreach (var p in payloads) bw.Write(p);

            return WaveFileSpeech.WavDuration(outputPath);
        }

        private static bool TrySplit(byte[] wav, out byte[] fmt, out byte[] data)
        {
            fmt = Array.Empty<byte>();
            data = Array.Empty<byte>();
            if (wav.Length < 44) return false;
            if (wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F') return false;
            if (wav[8] != 'W' || wav[9] != 'A' || wav[10] != 'V' || wav[11] != 'E') return false;

            int i = 12;
            byte[]? foundFmt = null;
            byte[]? foundData = null;
            while (i + 8 <= wav.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(wav, i, 4);
                int size = BitConverter.ToInt32(wav, i + 4);
                if (size < 0 || i + 8 + size > wav.Length) return false;
                if (id == "fmt ")
                {
                    foundFmt = new byte[size];
                    Buffer.BlockCopy(wav, i + 8, foundFmt, 0, size);
                }
                else if (id == "data")
                {
                    foundData = new byte[size];
                    Buffer.BlockCopy(wav, i + 8, foundData, 0, size);
                }
                i += 8 + size;
                if ((size & 1) == 1) i++;
            }

            if (foundFmt == null || foundData == null) return false;
            fmt = foundFmt;
            data = foundData;
            return true;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
