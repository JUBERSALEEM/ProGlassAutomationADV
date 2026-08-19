using ProGlassAutomation.Agent.Demo.Recording;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ScreenRecordSettingsTests
    {
        [Fact]
        public void Default_is_1080p_30fps_with_live_audio()
        {
            var s = new ScreenRecordSettings();
            Assert.Equal(ScreenRecordQuality.FullHd, s.Quality);
            Assert.Equal(30, s.Fps);
            Assert.True(s.CaptureLiveAudio);
            Assert.Equal((1920, 1080), s.OutputSize);
        }

        [Theory]
        [InlineData(ScreenRecordQuality.Hd720, 1280, 720)]
        [InlineData(ScreenRecordQuality.FullHd, 1920, 1080)]
        [InlineData(ScreenRecordQuality.Qhd2K, 2560, 1440)]
        [InlineData(ScreenRecordQuality.Uhd4K, 3840, 2160)]
        public void Quality_maps_to_even_output_size(
            ScreenRecordQuality quality, int w, int h)
        {
            var s = new ScreenRecordSettings { Quality = quality }.Clamp();
            Assert.Equal((w, h), s.OutputSize);
            Assert.Equal(0, s.OutputSize.Width & 1);
            Assert.Equal(0, s.OutputSize.Height & 1);
        }

        [Theory]
        [InlineData(8, 30)]
        [InlineData(30, 30)]
        [InlineData(40, 40)]
        [InlineData(50, 50)]
        [InlineData(60, 60)]
        [InlineData(120, 60)]
        [InlineData(44, 40)]
        public void Fps_clamps_to_supported_rates(int input, int expected)
        {
            var s = new ScreenRecordSettings { Fps = input }.Clamp();
            Assert.Equal(expected, s.Fps);
            Assert.Contains(s.Fps, ScreenRecordSettings.AllowedFps);
        }

        [Fact]
        public void Catalog_exposes_all_four_qualities_and_four_fps()
        {
            Assert.Equal(4, ScreenRecordSettings.Qualities.Count);
            Assert.Equal(4, ScreenRecordSettings.FpsOptions.Count);
            Assert.Equal(new[] { 30, 40, 50, 60 },
                ScreenRecordSettings.FpsOptions.Select(f => f.Fps).ToArray());
        }

        [Fact]
        public void Clone_does_not_share_mutation()
        {
            var a = new ScreenRecordSettings
            {
                Quality = ScreenRecordQuality.Uhd4K,
                Fps = 60,
                CaptureLiveAudio = false
            };
            var b = a.Clone();
            a.Fps = 30;
            a.Quality = ScreenRecordQuality.Hd720;
            Assert.Equal(60, b.Fps);
            Assert.Equal(ScreenRecordQuality.Uhd4K, b.Quality);
            Assert.False(b.CaptureLiveAudio);
        }
    }
}
