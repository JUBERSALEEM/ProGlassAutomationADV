using ProGlassAutomation.Agent.Demo.Video;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class LiveEncodeTests
    {
        [Fact]
        public void Mux_copy_method_exists_for_instant_save()
        {
            var method = typeof(FfmpegMp4Composer).GetMethod(
                nameof(FfmpegMp4Composer.MuxCopyVideoAudioAsync));
            Assert.NotNull(method);
        }

        [Fact]
        public void Live_pipe_does_not_start_without_ffmpeg()
        {
            FfmpegMp4Composer.InvalidateCache();
            var pipe = FfmpegLivePipe.TryStart(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pg-live-test.mp4"),
                30, 1280, 720, 18);
            if (FfmpegMp4Composer.Find() == null)
                Assert.Null(pipe);
            pipe?.Dispose();
        }
    }
}
