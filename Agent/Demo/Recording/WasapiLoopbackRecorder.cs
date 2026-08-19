using NAudio.Wave;
using ProGlassAutomation.Agent.Demo.Tts;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Recording
{
    /// <summary>
    /// Records what the app plays (SAPI narration, UI sounds) via WASAPI
    /// loopback. Never captures the microphone. Failure is non-fatal —
    /// the director falls back to generated TTS WAVs.
    /// </summary>
    public sealed class WasapiLoopbackRecorder : IDisposable
    {
        private WasapiLoopbackCapture? _capture;
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
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(wavPath)!);
                _capture = new WasapiLoopbackCapture();
                _writer = new WaveFileWriter(wavPath, _capture.WaveFormat);
                _stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _capture.DataAvailable += OnData;
                _capture.RecordingStopped += OnStopped;
                _capture.StartRecording();
                Path = wavPath;
                IsRecording = true;
                return true;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Debug.WriteLine("[DemoAudio] loopback start failed: " + ex.Message);
                StopCore();
                return false;
            }
        }

        public async Task<TimeSpan> StopAsync()
        {
            var wait = _stopped;
            try { _capture?.StopRecording(); }
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
                    Debug.WriteLine("[DemoAudio] write failed: " + ex.Message);
                }
            }
        }

        private void OnStopped(object? sender, StoppedEventArgs e)
        {
            IsRecording = false;
            if (e.Exception != null)
            {
                Error = e.Exception.Message;
                Debug.WriteLine("[DemoAudio] loopback stopped: " + e.Exception.Message);
            }
            _stopped?.TrySetResult(true);
        }

        private void StopCore()
        {
            IsRecording = false;
            try
            {
                if (_capture != null)
                {
                    _capture.DataAvailable -= OnData;
                    _capture.RecordingStopped -= OnStopped;
                    _capture.Dispose();
                }
            }
            catch { /* already stopped */ }
            _capture = null;

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
