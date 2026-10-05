using System.Runtime.CompilerServices;
using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]

// Grant the test assembly access to internal members of this
// production assembly. This is the documented way to expose
// test-only hooks (e.g. LicensePublicKey.SetForTest) without
// making them part of the public API.
//
// ProGlassAutomation.Tests is the test project's assembly
// name (set via <RootNamespace>ProGlassAutomation.Tests</RootNamespace>
// in Tests/ProGlassAutomation.Tests.csproj).
[assembly: InternalsVisibleTo("ProGlassAutomation.Tests")]
