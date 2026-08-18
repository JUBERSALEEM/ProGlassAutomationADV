using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProGlassAutomation.Agent.Demo.Video
{
    /// <summary>
    /// Writes a playable MJPEG AVI (video only). Used as the encoder input
    /// and as a fallback when MP4 remux is unavailable.
    /// </summary>
    public static class MjpegAviWriter
    {
        public static void Write(string aviPath, IReadOnlyList<string> jpegPaths, int fps)
        {
            if (jpegPaths == null || jpegPaths.Count == 0)
                throw new InvalidOperationException("No frames to encode.");
            fps = Math.Clamp(fps, 1, 30);

            var frames = new List<byte[]>(jpegPaths.Count);
            int w = 0, h = 0;
            foreach (var p in jpegPaths)
            {
                var bytes = File.ReadAllBytes(p);
                frames.Add(bytes);
                ReadJpegSize(bytes, out int jw, out int jh);
                if (jw > w) w = jw;
                if (jh > h) h = jh;
            }
            if (w <= 0) w = 1280;
            if (h <= 0) h = 720;

            Directory.CreateDirectory(Path.GetDirectoryName(aviPath)!);
            using var fs = File.Create(aviPath);
            using var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: true);

            int n = frames.Count;
            int maxFrame = 0;
            long moviSize = 4; // 'movi'
            foreach (var f in frames)
            {
                int padded = (f.Length + 1) & ~1;
                if (padded > maxFrame) maxFrame = padded;
                moviSize += 8 + padded;
            }

            int hdrlSize = 4 + 8 + 56 + 8 + (4 + 8 + 56 + 8 + 40);
            // We'll write header after we know sizes — use a conservative layout:
            // RIFF size = 4 + LIST hdrl + LIST movi + idx1
            int idx1Size = 8 + n * 16;
            int hdrlList = 4 + 64 + 8 + (12 + 48 + 8 + 40); // avih + strl
            // Simpler fixed structure used by many MJPEG writers:
            WriteFour(bw, "RIFF");
            var riffSizePos = fs.Position;
            bw.Write(0);
            WriteFour(bw, "AVI ");

            WriteFour(bw, "LIST");
            bw.Write(192);
            WriteFour(bw, "hdrl");
            WriteFour(bw, "avih");
            bw.Write(56);
            bw.Write(1_000_000 / fps); // dwMicroSecPerFrame
            bw.Write(maxFrame * fps);
            bw.Write(0);
            bw.Write(0x10); // AVIF_HASINDEX
            bw.Write(n);
            bw.Write(0);
            bw.Write(1); // streams
            bw.Write(maxFrame);
            bw.Write(w);
            bw.Write(h);
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

            WriteFour(bw, "LIST");
            bw.Write(116);
            WriteFour(bw, "strl");
            WriteFour(bw, "strh");
            bw.Write(56);
            WriteFour(bw, "vids");
            WriteFour(bw, "MJPG");
            bw.Write(0); bw.Write(0); bw.Write(0);
            bw.Write(1);
            bw.Write(fps);
            bw.Write(0);
            bw.Write(n);
            bw.Write(maxFrame);
            bw.Write(-1);
            bw.Write(0);
            bw.Write((short)0); bw.Write((short)0);
            bw.Write((short)w); bw.Write((short)h);

            WriteFour(bw, "strf");
            bw.Write(40);
            bw.Write(40);
            bw.Write(w);
            bw.Write(h);
            bw.Write((short)1);
            bw.Write((short)24);
            WriteFour(bw, "MJPG");
            bw.Write(w * h * 3);
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

            WriteFour(bw, "LIST");
            var moviSizePos = fs.Position;
            bw.Write(0);
            WriteFour(bw, "movi");
            var offsets = new List<int>(n);
            long moviStart = fs.Position - 4;
            foreach (var f in frames)
            {
                offsets.Add((int)(fs.Position - moviStart - 4));
                WriteFour(bw, "00dc");
                bw.Write(f.Length);
                bw.Write(f);
                if ((f.Length & 1) == 1) bw.Write((byte)0);
            }
            var afterMovi = fs.Position;
            fs.Position = moviSizePos;
            bw.Write((int)(afterMovi - moviSizePos - 4));
            fs.Position = afterMovi;

            WriteFour(bw, "idx1");
            bw.Write(n * 16);
            foreach (var off in offsets)
            {
                WriteFour(bw, "00dc");
                bw.Write(0x10);
                bw.Write(off);
                bw.Write(maxFrame);
            }

            var end = fs.Position;
            fs.Position = riffSizePos;
            bw.Write((int)(end - 8));
            fs.Position = end;
        }

        private static void WriteFour(BinaryWriter bw, string s)
        {
            bw.Write((byte)s[0]); bw.Write((byte)s[1]);
            bw.Write((byte)s[2]); bw.Write((byte)s[3]);
        }

        private static void ReadJpegSize(byte[] jpeg, out int w, out int h)
        {
            w = 0; h = 0;
            int i = 2;
            while (i + 8 < jpeg.Length)
            {
                if (jpeg[i] != 0xFF) break;
                byte marker = jpeg[i + 1];
                int len = (jpeg[i + 2] << 8) | jpeg[i + 3];
                if (marker is 0xC0 or 0xC1 or 0xC2)
                {
                    h = (jpeg[i + 5] << 8) | jpeg[i + 6];
                    w = (jpeg[i + 7] << 8) | jpeg[i + 8];
                    return;
                }
                i += 2 + len;
            }
        }
    }
}
