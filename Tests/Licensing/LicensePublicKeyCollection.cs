// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/LicensePublicKeyCollection.cs
// PROJECT: ProGlassAutomation
// MODULE: Tests — xUnit collection for license-key sharing
//
// Commit 6 of the licensing refactor. All five test
// classes that touch LicensePublicKey.SetForTest and /
// or LicenseService.TestDataFolderOverride now share
// one xUnit collection ("LicensePublicKey") with
// parallelization disabled. Without this, xUnit runs
// the four Licensing/* classes (and CrashReportServiceTests)
// concurrently, and the static
// LicensePublicKey.PublicKeyXml + LicenseService
// .TestDataFolderOverride are overwritten across
// tests in ways the tests cannot observe — one test
// signs with private key A, another test (running
// at the same time) overwrites PublicKeyXml with
// public key B, then the verify step reads B and
// returns Invalid. The skip-reason on the previously
// skipped tests is the symptom: "the in-memory
// PublicKeyXml is not being read by LicenseVerifier
// at the expected point."
//
// Putting all five classes in one xUnit collection
// with DisableParallelization = true forces them to
// run serially, so each test sees the public key it
// installed in its constructor. The cost is that
// the license + crash-report suite no longer runs in
// parallel, but that suite is small (≈ 41 tests) and
// the dominant cost is the .NET runtime startup, not
// the test wall time.
//
// xUnit rules reminder: a [CollectionDefinition] must
// live in the test assembly and must be paired with
// [Collection("Name")] on each participating class.
// xUnit picks up [CollectionDefinition] from the
// assembly by reflection, so the file path does not
// matter; convention is to put it next to the first
// test that uses it.
// ═══════════════════════════════════════════════════════════════════════

using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    /// <summary>
    /// The shared collection name is "LicensePublicKey".
    /// All five test classes below declare
    /// [Collection("LicensePublicKey")] on themselves.
    /// xUnit serialises every test in the collection
    /// within one process; tests in other collections
    /// (and tests in the same collection but in a
    /// different process) are unaffected.
    /// </summary>
    [CollectionDefinition("LicensePublicKey",
        DisableParallelization = true)]
    public sealed class LicensePublicKeyCollection
    {
        // Intentionally empty. The attribute is the
        // payload. A class body is required so the
        // compiler can attach the [CollectionDefinition]
        // to a concrete type that xUnit's collector
        // can find by reflection.
    }
}
