using ProGlassAutomation.Agent.Demo.Video;
using System.IO;
using System.Text;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class Mp4ValidationTests
    {
        [Fact]
        public void Missing_file_is_not_an_mp4()
        {
            Assert.False(FfmpegMp4Composer.IsValidMp4(
                Path.Combine(Path.GetTempPath(), "no-such-proglass-demo.mp4")));
        }

        [Fact]
        public void Empty_file_is_not_an_mp4()
        {
            string path = Path.Combine(Path.GetTempPath(), "proglass-empty.mp4");
            File.WriteAllBytes(path, Array.Empty<byte>());
            try { Assert.False(FfmpegMp4Composer.IsValidMp4(path)); }
            finally { TryDelete(path); }
        }

        [Fact]
        public void Avi_riff_header_is_not_an_mp4()
        {
            string path = Path.Combine(Path.GetTempPath(), "proglass-riff.mp4");
            var buf = new byte[32];
            Encoding.ASCII.GetBytes("RIFF").CopyTo(buf, 0);
            Encoding.ASCII.GetBytes("AVI ").CopyTo(buf, 8);
            File.WriteAllBytes(path, buf);
            try { Assert.False(FfmpegMp4Composer.IsValidMp4(path)); }
            finally { TryDelete(path); }
        }

        [Fact]
        public void Ftyp_at_offset_four_is_accepted()
        {
            string path = Path.Combine(Path.GetTempPath(), "proglass-ftyp.mp4");
            var buf = new byte[32];
            buf[0] = 0; buf[1] = 0; buf[2] = 0; buf[3] = 20;
            buf[4] = (byte)'f';
            buf[5] = (byte)'t';
            buf[6] = (byte)'y';
            buf[7] = (byte)'p';
            buf[8] = (byte)'i';
            buf[9] = (byte)'s';
            buf[10] = (byte)'o';
            buf[11] = (byte)'m';
            File.WriteAllBytes(path, buf);
            try { Assert.True(FfmpegMp4Composer.IsValidMp4(path)); }
            finally { TryDelete(path); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
