"""Portable host-integration guard for importing the standalone theme subsystem."""
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
project = ET.parse(ROOT / 'ProGlassAutomation.csproj').getroot()
assert project.find('.//TargetFramework').text.startswith('net8.0-windows')
packages = {(item.attrib.get('Include'), item.attrib.get('Version')) for item in project.findall('.//PackageReference')}
assert ('LiveChartsCore.SkiaSharpView.WPF', '2.0.4') in packages
assert any(item.tag == 'Compile' and item.attrib.get('Remove', '').replace('\\', '/').startswith('Theme/step1.d.tests/') for item in project.findall('.//Compile'))

families = ('Bento', 'Claymorphism', 'Fluent', 'Neumorphism', 'LiquidGlass', 'Glassmorphism', 'Spatial', 'Swiss', 'Holographic')
for family in families:
    folder = ROOT / ('Theme/Theme' + family)
    assert (folder / ('Theme' + family + '.Light.xaml')).is_file(), family + ' Light package missing'
    assert (folder / ('Theme' + family + '.Dark.xaml')).is_file(), family + ' Dark package missing'

runtime = (ROOT / 'Services/Theme/ImportedThemeRuntime.cs').read_text()
for name in ('BentoGrid', 'Fluent', 'Claymorphism', 'Neumorphism', 'LiquidGlass', 'Glassmorphism', 'SpatialImmersive', 'SwissInternational', 'HolographicIridescent'):
    assert 'VisualThemeEngine.' + name in runtime, 'host adapter missing family ' + name
assert 'SharedGeometry.Compose' in runtime and 'ColorPlateState' in runtime
assert 'IconService.Apply' in runtime and 'CopyBrush' in runtime and 'CopyTyped' in runtime
assert 'ImportedThemeRuntime.Initialize' in (ROOT / 'App.StartupCore.cs').read_text()
assert 'ImportedThemeRuntime.ApplyFromHost' in (ROOT / 'Services/Theme/ThemePipeline.cs').read_text()
assert 'OpenImportedThemeStudioCommand' in (ROOT / 'Views/Settings/Sections/SettingsThemesSection.xaml').read_text()
assert 'ImportedIconMark' in (ROOT / 'Themes/Professional/ProfessionalShell.xaml').read_text()
assert 'public static ResourceDictionary Load' in (ROOT / 'Theme/step1.a.ThemeEngine/step1.a.2.ThemeService.cs').read_text()
assert 'PackageChanged' in (ROOT / 'Theme/step1.a.ThemeEngine/step1.a.2.ThemeService.cs').read_text()
assert 'persistCustomization: false' in (ROOT / 'Theme/step1.d.tests/step1.d.1.RuntimeChecks.cs').read_text()

print('PASS: current ERP startup/pipeline, Settings Studio route, typed resource bridge, plates, icons, all nine Light/Dark packages and test/app boundary are connected.')
print('LIMITATION: portable source audit is not Windows WPF compilation, runtime smoke or visual inspection.')
