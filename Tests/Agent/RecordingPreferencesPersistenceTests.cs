// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/RecordingPreferencesPersistenceTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the persistence contract for RecordingPreferences introduced by
// the August 2026 Demo Hub audit. The user's complaint was:
//
//   "Demo hub resetting last saved video resolution and mic or speaker
//    access it should be fixed with update do not restore."
//
// The tests here are hermetic — they build a POCO AppConfig, round-trip
// it through System.Text.Json (the same serializer ConfigService uses),
// and assert the recording preferences survive.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using System.Text.Json;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class RecordingPreferencesPersistenceTests
    {
        [Fact]
        public void RecordingPreferences_defaults_are_sensible()
        {
            var prefs = new RecordingPreferences();

            // 2026-09-04: seed defaults raised to 4K/60 on the user's
            // explicit quality-first instruction (AppConfig.cs documents
            // the codec-ladder math). This test previously pinned the
            // pre-2026-09-04 values (1080/30) and was stale-red.
            Assert.Equal(2160, prefs.QualityHeight);
            Assert.Equal(60,   prefs.Fps);
            // PHASE 4 (2026-09-14): quality tier defaults to High (1).
            Assert.Equal(1,    prefs.QualityTier);
            Assert.True(prefs.CaptureLiveAudio);
            Assert.True(prefs.CaptureMicrophone);
            Assert.False(prefs.Supersample);
            Assert.True(prefs.AutoCloseHudAfterSave);
        }

        [Fact]
        public void RecordingPreferences_round_trip_JSON_preserves_every_field()
        {
            var original = new RecordingPreferences
            {
                QualityHeight        = 1440,
                QualityTier          = 2,
                Fps                  = 60,
                CaptureLiveAudio     = false,
                CaptureMicrophone    = false,
                Supersample          = true,
                AutoCloseHudAfterSave = false
            };

            string json = JsonSerializer.Serialize(original);
            var back = JsonSerializer.Deserialize<RecordingPreferences>(json);

            Assert.NotNull(back);
            Assert.Equal(1440, back!.QualityHeight);
            Assert.Equal(2,    back.QualityTier);
            Assert.Equal(60,   back.Fps);
            Assert.False(back.CaptureLiveAudio);
            Assert.False(back.CaptureMicrophone);
            Assert.True(back.Supersample);
            Assert.False(back.AutoCloseHudAfterSave);
        }

        [Fact]
        public void Legacy_JSON_without_QualityTier_hydrates_to_default()
        {
            // A config saved before Phase 4 has no QualityTier field;
            // the property initializer (1 = High) must win, so legacy
            // configs do not silently drop to the smallest tier.
            string legacyJson =
                "{ \"QualityHeight\": 2160, \"Fps\": 60, \"CaptureLiveAudio\": true }";
            var back = JsonSerializer.Deserialize<RecordingPreferences>(legacyJson);

            Assert.NotNull(back);
            Assert.Equal(1, back!.QualityTier);
        }

        [Fact]
        public void AppConfig_recording_defaults_when_JSON_missing_the_property()
        {
            // Legacy config.json from before RecordingPreferences was
            // added must hydrate to defaults, not null. ConfigService.
            // Load does the ??= new() pass; the model must round-trip
            // through JSON as an addressable object so the null check
            // is meaningful.
            string legacyJson = "{ \"Ai\": null, \"Appearance\": null }";
            var back = JsonSerializer.Deserialize<AppConfig>(legacyJson);

            Assert.NotNull(back);
            // The property is null in JSON — the load pipeline
            // upgrades it. Documented here as the expected shape.
            Assert.Null(back!.Recording);
        }

        [Fact]
        public void AppConfig_serialised_carries_the_recording_block()
        {
            var cfg = new AppConfig
            {
                Recording = new RecordingPreferences
                {
                    QualityHeight = 720,
                    Fps           = 40
                }
            };

            string json = JsonSerializer.Serialize(cfg);

            Assert.Contains("\"Recording\"", json);
            Assert.Contains("\"QualityHeight\":720", json);
            Assert.Contains("\"Fps\":40", json);
        }
    }
}
