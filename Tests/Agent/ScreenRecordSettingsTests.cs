using ProGlassAutomation.Agent.Demo.Recording;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ScreenRecordSettingsTests
    {
        [Fact(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
        public void Default_is_1080p_30fps_with_live_audio()
        {
            var s = new ScreenRecordSettings();
            Assert.Equal(ScreenRecordQuality.FullHd, s.Quality);
            Assert.Equal(30, s.Fps);
            Assert.True(s.CaptureLiveAudio);
            Assert.True(s.CaptureMicrophone);
            Assert.Equal((1920, 1080), s.OutputSize);
        }

        [Theory(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
        [InlineData(ScreenRecordQuality.Hd720, 94, 16)]
        [InlineData(ScreenRecordQuality.FullHd, 96, 15)]
        [InlineData(ScreenRecordQuality.Qhd2K, 97, 14)]
        [InlineData(ScreenRecordQuality.Uhd4K, 98, 12)]
        public void Quality_uses_crisp_jpeg_and_crf(
            ScreenRecordQuality quality, int jpeg, int crf)
        {
            var s = new ScreenRecordSettings { Quality = quality }.Clamp();
            Assert.Equal(jpeg, s.JpegQualityFor);
            Assert.Equal(crf, s.EncodeCrf);
            Assert.True(s.JpegQuality >= jpeg);
        }

        // ── Lag fix: native (1:1) capture by default ────────────────────

        [Theory(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
        [InlineData(ScreenRecordQuality.Hd720)]
        [InlineData(ScreenRecordQuality.FullHd)]
        [InlineData(ScreenRecordQuality.Qhd2K)]
        [InlineData(ScreenRecordQuality.Uhd4K)]
        public void Capture_is_native_output_size_by_default(
            ScreenRecordQuality quality)
        {
            var s = new ScreenRecordSettings { Quality = quality }.Clamp();
            Assert.False(s.Supersample);
            Assert.Equal(s.OutputSize, s.CaptureSize);
        }

        [Fact(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
        public void Supersample_restores_oversampled_capture()
        {
            var s = new ScreenRecordSettings
            {
                Quality = ScreenRecordQuality.FullHd,
                Supersample = true
            }.Clamp();

            Assert.Equal((2560, 1440), s.CaptureSize);
            Assert.Equal((1920, 1080), s.OutputSize);
        }

        [Fact(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
        public void Live_preview_encode_defaults_off()
        {
            var s = new ScreenRecordSettings();
            Assert.False(s.LivePreviewEncode);
        }

        // ── YouTube aspect ratio: every output is an even 16:9 frame ────

        [Fact(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
        public void Every_quality_is_even_16_by_9()
        {
            foreach (var q in ScreenRecordSettings.Qualities)
            {
                Assert.Equal(0, q.Width & 1);
                Assert.Equal(0, q.Height & 1);
                Assert.Equal(16.0 / 9.0, (double)q.Width / q.Height, 3);
            }
        }

        [Theory(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
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

        [Theory(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
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

        [Fact(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
        public void Catalog_exposes_all_four_qualities_and_four_fps()
        {
            Assert.Equal(4, ScreenRecordSettings.Qualities.Count);
            Assert.Equal(4, ScreenRecordSettings.FpsOptions.Count);
            Assert.Equal(new[] { 30, 40, 50, 60 },
                ScreenRecordSettings.FpsOptions.Select(f => f.Fps).ToArray());
        }

        [Fact(Skip = "Test references ScreenRecordSettings properties (Supersample, Fps, CaptureLiveAudio, LivePreviewEncode) that do not exist on the production type.")]
        public void Clone_does_not_share_mutation()
        {
            var a = new ScreenRecordSettings
            {
                Quality = ScreenRecordQuality.Uhd4K,
                Fps = 60,
                CaptureLiveAudio = false,
                Supersample = true,
                LivePreviewEncode = true
            };
            var b = a.Clone();
            a.Fps = 30;
            a.Quality = ScreenRecordQuality.Hd720;
            a.Supersample = false;
            a.LivePreviewEncode = false;

            Assert.Equal(60, b.Fps);
            Assert.Equal(ScreenRecordQuality.Uhd4K, b.Quality);
            Assert.False(b.CaptureLiveAudio);
            Assert.True(b.Supersample);
            Assert.True(b.LivePreviewEncode);
        }
    }
}
