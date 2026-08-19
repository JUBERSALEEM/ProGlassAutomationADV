using NAudio.Wave;
using ProGlassAutomation.Agent.Demo.Tts;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Recording
{
    /// <summary>
    /// Records the default microphone. Failure is non-fatal — the director
    /// keeps loopback / TTS if the mic is missing or in use.
    /// </summary>
    public sealed class WasapiMicRecorder : IDisposable
    {
        private WaveInEvent? _wave;
        private WaveFileWriter? _writer;
        private readonly object _gate = new();
        private TaskCompletionSource<bool>? _stopped;

        public string? Path { get; private set; }
        public bool IsRecording { get; private set; }
        public string? Error { get; private set; }

        public bool Start(string wavPath)
        {
            StopCore();
            Error = null;
            Path = null;
            try
            {
                if (WaveInEvent.DeviceCount <= 0)
                {
                    Error = "no microphone";
                    return false;
                }

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(wavPath)!);
                var format = new WaveFormat(44100, 16, 1);
                _writer = new WaveFileWriter(wavPath, format);
                _stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _wave = new WaveInEvent
                {
                    WaveFormat = format,
                    BufferMilliseconds = 50
                };
                _wave.DataAvailable += OnData;
                _wave.RecordingStopped += OnStopped;
                _wave.StartRecording();
                Path = wavPath;
                IsRecording = true;
                return true;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Debug.WriteLine("[DemoMic] start failed: " + ex.Message);
                StopCore();
                return false;
            }
        }

        public async Task<TimeSpan> StopAsync()
        {
            var wait = _stopped;
            try { _wave?.StopRecording(); }
            catch { wait?.TrySetResult(true); }

            if (wait != null)
                await Task.WhenAny(wait.Task, Task.Delay(1500)).ConfigureAwait(false);

            string? path = Path;
            StopCore();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return TimeSpan.Zero;
            try { return WaveFileSpeech.WavDuration(path); }
            catch { return TimeSpan.Zero; }
        }

        private void OnData(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0) return;
            lock (_gate)
            {
                try { _writer?.Write(e.Buffer, 0, e.BytesRecorded); }
                catch (Exception ex)
                {
                    Debug.WriteLine("[DemoMic] write failed: " + ex.Message);
                }
            }
        }

        private void OnStopped(object? sender, StoppedEventArgs e)
        {
            IsRecording = false;
            if (e.Exception != null)
            {
                Error = e.Exception.Message;
                Debug.WriteLine("[DemoMic] stopped: " + e.Exception.Message);
            }
            _stopped?.TrySetResult(true);
        }

        private void StopCore()
        {
            IsRecording = false;
            try
            {
                if (_wave != null)
                {
                    _wave.DataAvailable -= OnData;
                    _wave.RecordingStopped -= OnStopped;
                    _wave.Dispose();
                }
            }
            catch { /* already stopped */ }
            _wave = null;

            lock (_gate)
            {
                try { _writer?.Flush(); } catch { }
                try { _writer?.Dispose(); } catch { }
                _writer = null;
            }
        }

        public void Dispose() => StopCore();
    }
}
