using System;
using ProGlassAutomation.Agent.Demo.Recording;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    /// <summary>
    /// Pins the Android-recorder time format. The old formatter overflowed
    /// its minutes field past 99 minutes ("100:05.0") and carried a useless
    /// decisecond digit; the new one is a plain MM:SS / H:MM:SS.
    /// </summary>
    public class RecordingTimeTests
    {
        [Theory]
        [InlineData(0, 0, 0, "00:00")]
        [InlineData(0, 0, 7, "00:07")]
        [InlineData(0, 7, 42, "07:42")]
        [InlineData(0, 59, 59, "59:59")]
        [InlineData(1, 0, 0, "1:00:00")]
        [InlineData(1, 7, 42, "1:07:42")]
        [InlineData(2, 30, 5, "2:30:05")]
        [InlineData(100, 0, 0, "100:00:00")]
        public void Format_never_overflows_and_rolls_to_hours(
            int h, int m, int s, string expected)
        {
            Assert.Equal(expected, RecordingTime.Format(new TimeSpan(h, m, s)));
        }

        [Fact]
        public void Format_clamps_negative_to_zero()
        {
            Assert.Equal("00:00", RecordingTime.Format(TimeSpan.FromSeconds(-5)));
        }

        [Fact]
        public void Format_pads_minutes_and_seconds_to_two_digits()
        {
            Assert.Equal("05:09", RecordingTime.Format(new TimeSpan(0, 5, 9)));
            Assert.Equal("1:05:09", RecordingTime.Format(new TimeSpan(1, 5, 9)));
        }
    }
}
