// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIVoice/AudioCaptureService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Voice (Chat 24 v1.3 — Whisper integration)
//
// PURPOSE:
//   Captures audio from the default microphone using NAudio.
//   Outputs 16kHz mono 16-bit PCM WAV bytes — the format required
//   by Whisper.net for speech recognition.
//
// WHY NAudio instead of System.Speech:
//   System.Speech.Recognition doesn't give us raw audio bytes —
//   it processes them internally. Whisper.net needs the raw WAV data
//   to run its own neural network inference. NAudio gives us direct
//   microphone access with full control over format and buffering.
//
// REQUIREMENTS:
//   NuGet: NAudio v2.2.1+
//   <PackageReference Include="NAudio" Version="2.2.1" />
//
// REGRESSION RISK: ZERO — new file, no existing code depends on it.
// ═══════════════════════════════════════════════════════════════════════

using NAudio.Wave;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIVoice
{
    /// <summary>
    /// Captures audio from default microphone as 16kHz mono WAV bytes.
    /// Used by WhisperSpeechService for neural speech recognition.
    /// </summary>
    public sealed class AudioCaptureService : IDisposable
    {
        private WaveInEvent _waveIn;
        private MemoryStream _buffer;
        private WaveFileWriter _writer;
        private bool _isRecording;
        private bool _isDisposed;

        /// <summary>True while microphone is actively recording.</summary>
        public bool IsRecording => _isRecording;

        /// <summary>True if a microphone is available.</summary>
        public bool IsMicrophoneAvailable { get; }

        /// <summary>
        /// Fires periodically during recording with the current audio
        /// level (0.0–1.0) for visual feedback (volume meter).
        /// </summary>
        public event EventHandler<float> AudioLevelChanged;

        public AudioCaptureService()
        {
            try
            {
                // Check if any recording device is available
                IsMicrophoneAvailable = WaveInEvent.DeviceCount > 0;
            }
            catch
            {
                IsMicrophoneAvailable = false;
            }
        }

        /// <summary>
        /// Start recording from the default microphone.
        /// Audio is buffered in memory as 16kHz mono 16-bit PCM WAV.
        /// Call <see cref="StopAndGetAudioAsync"/> to stop and get the bytes.
        /// </summary>
        public void StartRecording()
        {
            if (!IsMicrophoneAvailable || _isRecording || _isDisposed) return;

            try
            {
                // Whisper requires 16kHz, mono, 16-bit PCM
                var waveFormat = new WaveFormat(16000, 16, 1);

                _buffer = new MemoryStream();
                _writer = new WaveFileWriter(_buffer, waveFormat);

                _waveIn = new WaveInEvent
                {
                    WaveFormat = waveFormat,
                    BufferMilliseconds = 100 // 100ms chunks
                };

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _waveIn.StartRecording();
                _isRecording = true;
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[AudioCapture] StartRecording failed: {ex.Message}");
                CleanupRecording();
            }
        }

        /// <summary>
        /// Stop recording and return the captured audio as WAV bytes.
        /// Returns null if no audio was captured or recording failed.
        /// </summary>
        public async Task<byte[]> StopAndGetAudioAsync()
        {
            if (!_isRecording) return null;

            try
            {
                _waveIn?.StopRecording();

                // Small delay to ensure all buffers are flushed
                await Task.Delay(200);

                _writer?.Flush();

                var audioBytes = _buffer?.ToArray();

                CleanupRecording();

                // Minimum viable audio: at least 0.5 seconds of 16kHz mono
                // (16000 samples/sec × 2 bytes × 0.5 sec = 16000 bytes + WAV header)
                if (audioBytes == null || audioBytes.Length < 16000)
                    return null;

                return audioBytes;
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[AudioCapture] StopAndGetAudio failed: {ex.Message}");
                CleanupRecording();
                return null;
            }
        }

        /// <summary>Cancel recording without returning audio.</summary>
        public void CancelRecording()
        {
            if (!_isRecording) return;
            _waveIn?.StopRecording();
            CleanupRecording();
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                _writer?.Write(e.Buffer, 0, e.BytesRecorded);

                // Calculate audio level for visual feedback
                float maxLevel = 0;
                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    if (i + 1 < e.BytesRecorded)
                    {
                        short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                        float level = Math.Abs(sample) / 32768f;
                        if (level > maxLevel) maxLevel = level;
                    }
                }
                AudioLevelChanged?.Invoke(this, maxLevel);
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            _isRecording = false;
        }

        private void CleanupRecording()
        {
            _isRecording = false;

            try { _waveIn?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try { _writer?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            // Don't dispose _buffer — it's returned as byte array

            _waveIn = null;
            _writer = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            CancelRecording();
            _buffer?.Dispose();
            _buffer = null;
        }
    }
}