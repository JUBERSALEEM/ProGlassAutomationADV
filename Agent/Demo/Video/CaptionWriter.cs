using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProGlassAutomation.Agent.Demo.Video
{
    /// <summary>SRT / VTT timed from real WAV durations — never a fake 4s beat.</summary>
    public static class CaptionWriter
    {
        public static void Write(
            string folder,
            IReadOnlyList<(string Text, TimeSpan Duration)> beats)
        {
            Directory.CreateDirectory(folder);
            var srt = new StringBuilder();
            var vtt = new StringBuilder();
            vtt.AppendLine("WEBVTT");
            vtt.AppendLine();

            var cursor = TimeSpan.Zero;
            int n = 0;
            foreach (var beat in beats)
            {
                if (beat.Duration <= TimeSpan.Zero) continue;
                n++;
                var start = cursor;
                var end = cursor + beat.Duration;
                srt.AppendLine(n.ToString());
                srt.AppendLine($"{Fmt(start, ',')} --> {Fmt(end, ',')}");
                srt.AppendLine(beat.Text ?? "");
                srt.AppendLine();
                vtt.AppendLine($"{Fmt(start, '.')} --> {Fmt(end, '.')}");
                vtt.AppendLine(beat.Text ?? "");
                vtt.AppendLine();
                cursor = end;
            }

            File.WriteAllText(Path.Combine(folder, "captions.srt"), srt.ToString());
            File.WriteAllText(Path.Combine(folder, "captions.vtt"), vtt.ToString());
        }

        private static string Fmt(TimeSpan ts, char msSep)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}{msSep}{ts.Milliseconds:000}";
        }
    }
}
