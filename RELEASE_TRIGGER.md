# ProGlass Automation v2.0.15 release trigger

This file exists only on the versioned release branch to trigger the Windows release workflow. The release workflow verifies the project version, builds and tests the WPF application, publishes a self-contained single-file Windows x64 executable, validates its signature, and only then publishes the release assets. The private License Generator remains excluded from public release assets and OTA.
