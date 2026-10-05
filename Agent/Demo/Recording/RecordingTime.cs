// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Demo/Recording/RecordingTime.cs
// PROJECT: ProGlassAutomation
// MODULE: Screen recording — shared duration formatting
// VERSION: Screen-recording redesign — 2026-08-21
//
// PURPOSE:
//   One place to turn a TimeSpan into the compact "Android recorder" style
//   timer text used by the recording overlay and the "saved" dialog.
//
//   WHY (the reported time bug):
//   The old formatter was
//       string.Format("{0:00}:{1:00}.{2:0}", (int)ts.TotalMinutes, …)
//   which (a) overflows the minutes field once a recording passes 99
//   minutes — "100:05.0" breaks the MM:SS layout — and (b) shows a
//   decisecond digit that read like a stopwatch but carried no useful
//   precision for a screen recording. The Android recorder shows a plain
//   MM:SS (or H:MM:SS past one hour), so this helper does exactly that.
//
//   Format:
//     < 1 hour  → "MM:SS"     (e.g. "07:42")
//     >= 1 hour → "H:MM:SS"   (e.g. "1:07:42")
//   Negative / zero clamps to "00:00".
//
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Agent.Demo.Recording
{
    public static class RecordingTime
    {
        /// <summary>Android-style compact timer text. Never throws, never overflows.</summary>
        public static string Format(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;

            int totalSeconds = (int)ts.TotalSeconds;
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            return hours > 0
                ? $"{hours}:{minutes:00}:{seconds:00}"
                : $"{minutes:00}:{seconds:00}";
        }
    }
}
