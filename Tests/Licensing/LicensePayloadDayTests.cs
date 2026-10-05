// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/LicensePayloadDayTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Tests — payload day/expiry helpers
//
// Regression for "the license page showed a 7-day trial when it was a
// 30-day trial". The app must read the duration and balance from the
// payload's own authoritative value, never from a hardcoded number.
// These tests pin LicensePayload.EffectiveExpiresAt / TotalDays.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Services.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    public sealed class LicensePayloadDayTests
    {
        [Fact]
        public void EffectiveExpiresAt_uses_TrialExpiresAt_for_a_trial()
        {
            var issued = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);
            var trial = new LicensePayload
            {
                IssuedAt = issued,
                ExpiresAt = issued.AddDays(999),   // safety-net value
                TrialSource = "auto-7d",
                TrialExpiresAt = issued.AddDays(30)
            };

            // The authoritative expiry is the strict trial window, NOT the
            // far-future ExpiresAt.
            Assert.Equal(trial.TrialExpiresAt, trial.EffectiveExpiresAt);
        }

        [Fact]
        public void EffectiveExpiresAt_uses_ExpiresAt_for_a_real_license()
        {
            var issued = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);
            var real = new LicensePayload
            {
                IssuedAt = issued,
                ExpiresAt = issued.AddDays(365),
                TrialSource = "",                 // not a trial
                TrialExpiresAt = default
            };

            Assert.Equal(real.ExpiresAt, real.EffectiveExpiresAt);
        }

        [Fact]
        public void TotalDays_returns_the_trial_window_from_the_payload()
        {
            var issued = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);
            var trial = new LicensePayload
            {
                IssuedAt = issued,
                ExpiresAt = issued.AddDays(999),
                TrialSource = "auto-7d",
                TrialExpiresAt = issued.AddDays(30)
            };

            Assert.Equal(30, trial.TotalDays);
        }

        [Fact]
        public void TotalDays_is_zero_for_an_uninterpretable_payload()
        {
            var bad = new LicensePayload
            {
                IssuedAt = default,
                ExpiresAt = default,
                TrialExpiresAt = default
            };

            Assert.Equal(0, bad.TotalDays);
        }
    }
}
