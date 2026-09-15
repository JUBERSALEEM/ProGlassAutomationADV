#!/usr/bin/env python3
"""Settings rewrite guard — a page may be restyled, never defused.

WHY THIS EXISTS
───────────────
The Settings redesign rewrites twelve section files that between them carry
hundreds of bindings to their view models. A restyle is a *structural* edit:
cards are re-wrapped, headers are rebuilt, spacers are replaced. The failure
mode that matters is silent and invisible to every other check —

    drop one `{Binding …}` while re-wrapping a card and the control keeps
    rendering, just with no data and no error.

The repo's other guards catch unknown binding *heads*; they cannot catch a
binding that was simply deleted. This tool can: it compares a page against
its committed version and refuses to let any binding, command parameter,
resource reference or x:Name disappear.

USAGE
    python3 scripts/settings-rewrite/verify_bindings.py            # all pages
    python3 scripts/settings-rewrite/verify_bindings.py FILE...    # selected
    python3 scripts/settings-rewrite/verify_bindings.py --against REV FILE...

Exit code 1 on any regression, so it can gate a commit.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SECTIONS = REPO / "Views" / "Settings" / "Sections"

# Things that carry meaning into the view model or the visual tree.
PATTERNS = {
    "binding": re.compile(r"\{Binding\s+([^},]+)"),
    "bindingpath": re.compile(r"\{Binding\s+Path=([^},]+)"),
    "commandparam": re.compile(r'CommandParameter="([^"]+)"'),
    "elementname": re.compile(r"ElementName=([A-Za-z_][\w]*)"),
    "xname": re.compile(r'x:Name="([^"]+)"'),
    "relsource": re.compile(r'RelativeSource=(\{[^}]*\}|\w+)'),
    "staticres": re.compile(r'\{StaticResource\s+([^}]+)\}'),
    "dynres": re.compile(r'\{DynamicResource\s+([^}]+)\}'),
    "converter": re.compile(r'Converter=\{StaticResource\s+([^}]+)\}'),
    "datatrigger": re.compile(r'<DataTrigger[^>]*Binding="\{Binding\s+([^},]+)'),
    "behaviour": re.compile(r'behaviors:(\w+)'),
}


# Intentional removals, recorded per page. A redesign may delete chrome on
# purpose — a duplicate page title, a surface token replaced by the grammar —
# but the decision must be written down here, with its reason, instead of
# being lost in a diff. Everything NOT listed is a regression.
JUSTIFIED: dict[str, dict[str, str]] = {
    "SettingsLicenseSection.xaml": {
        # staticres
        "SettingsGroupHeaderTextStyle": "replaced by SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — cards carry their own notes",
        # dynres
        "About.License": "text of the removed duplicate page title",
        "Settings.ChoosePlanRequestKeyPasteVendorSignedKey": "text of the removed duplicate subtitle",
        "AccentBrush": "status badge is now a SettingsChipGreenStyle chip, not an accent block",
        "ControlCornerRadius": "grammar radius token (Ios26RadiusChip) replaces it",
        "FontSize.Body": "grammar type role (Ios26FontSizeBody) replaces it",
        "FontSize.H1": "grammar type role (Ios26FontSizeH1) replaces it",
        "FontSize.H2": "grammar type role (Ios26FontSizeH2) replaces it",
        "OnAccentForegroundBrush": "no accent-filled text block on the page any more",
        "TextPrimaryBrush": "grammar ink token (Ios26TextTitleBrush) replaces it",
        "TextTertiaryBrush": "grammar ink token (Ios26TextMutedBrush) replaces it",
        "Weight.Bold": "grammar type roles carry the weights",
        "Weight.SemiBold": "grammar type roles carry the weights",
    },
    "SettingsUpdatesSection.xaml": {
        # staticres
        "SettingsGroupCaptionStyle": "group bands are card headers now — title + helper line, per the approved mockup",
        # staticres
        "SettingsGroupHeaderTextStyle": "group captions use SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.Caption": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        # dynres
        "CardBorderThickness": "the grammar tile owns its 1px hairline; the hand-built inline chrome is gone",
        "Settings.KeepProglassLatestReleaseSafely": "text of the removed duplicate page subtitle",
        "Settings.SecUpdates": "text of the removed duplicate page title (the shell strip shows the page name)",
    },
    # ── the standard page-chrome removal, page by page ──────────────
    # Every page carried its own H1 + subtitle and its own group-band
    # captions; the shell header strip is now the single page header and the
    # grammar's card titles replace the bands. These are the text keys that
    # went with that chrome — recorded so the removal is a decision, not a
    # silent deletion, and so any OTHER loss still fails the check.
    "SettingsAgentSection.xaml": {
        # staticres
        "SettingsGroupCaptionStyle": "group bands are card headers now — title + helper line, per the approved mockup",
        "SettingsGroupHeaderTextStyle": "group captions use SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.Caption": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        "Typography.Title": "replaced by SettingsRowTitleStyle / SettingsCardTitleStyle",
        "Settings.SecAgent": "text of the removed duplicate page title",
        "Settings.PrivacyPostureProviderCredentialsTaught": "text of the removed duplicate page subtitle",
    },
    "SettingsAiSection.xaml": {
        # staticres
        "SettingsGroupCaptionStyle": "group bands are card headers now — title + helper line, per the approved mockup",
        "SettingsGroupHeaderTextStyle": "group captions use SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.Caption": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        "Typography.Title": "replaced by SettingsRowTitleStyle / SettingsCardTitleStyle",
        "Settings.SecAi": "text of the removed duplicate page title",
        "Settings.ProvidersAPIKeysModelTuningDataAccess": "text of the removed duplicate page subtitle",
    },
    "SettingsDataSection.xaml": {
        # staticres
        "SettingsGroupCaptionStyle": "group bands are card headers now — title + helper line, per the approved mockup",
        "SettingsGroupHeaderTextStyle": "group captions use SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.Caption": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        "Typography.Title": "replaced by SettingsRowTitleStyle / SettingsCardTitleStyle",
        "Settings.SecData": "text of the removed duplicate page title",
        "Settings.ProtectProductionDataExploreSampleData": "text of the removed duplicate page subtitle",
    },
    "SettingsDemoSection.xaml": {
        # staticres
        "SettingsGroupCaptionStyle": "group bands are card headers now — title + helper line, per the approved mockup",
        "SettingsGroupHeaderTextStyle": "group captions use SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.Caption": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        "Typography.Title": "replaced by SettingsRowTitleStyle / SettingsCardTitleStyle",
        "Settings.SecDemo": "text of the removed duplicate page title",
        "Settings.ModuleRecordsLiveWalkProductionStagesReports": "text of the removed duplicate page subtitle",
    },
    "SettingsAppearanceSection.xaml": {
        "SettingsRowMetaStyle": "the group recap line is the card helper line (SettingsCardHeaderNoteStyle)",
        # staticres
        "SettingsGroupCaptionStyle": "group bands are card headers now — title + helper line, per the approved mockup",
        "SettingsGroupHeaderTextStyle": "group captions use SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.Caption": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        "Typography.Title": "replaced by SettingsRowTitleStyle / SettingsCardTitleStyle",
        "Settings.SecAppearance": "text of the removed duplicate page title",
        "Settings.ThemeColoursWallpapersMotionModule": "text of the removed duplicate page subtitle",
    },
    "SettingsFeaturesSection.xaml": {
        "SettingsGroupHeaderTextStyle": "group captions use SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        "Typography.Title": "replaced by SettingsRowTitleStyle / SettingsCardTitleStyle",
        "Settings.SecFeatures": "text of the removed duplicate page title",
        "Settings.TurnIndividualFieldsWithoutTouchingCode": "text of the removed duplicate page subtitle",
    },
    "SettingsSharingSection.xaml": {
        # staticres
        "SettingsGroupCaptionStyle": "group bands are card headers now — title + helper line, per the approved mockup",
        "SettingsGroupHeaderTextStyle": "group captions use SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.Caption": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        "Typography.Title": "replaced by SettingsRowTitleStyle / SettingsCardTitleStyle",
        "Settings.SecSharing": "text of the removed duplicate page title",
        "Sharing.SettingsSubtitle": "text of the removed duplicate page subtitle",
    },
    "SettingsVoiceSection.xaml": {
        "Settings.SecVoice": "text of the removed duplicate page title",
        "Settings.OfflineSpeechTextOpenaiWhisper95AccuracyNo": "text of the removed duplicate page subtitle",
    },
    "SettingsThemesSection.xaml": {
        # staticres
        "SettingsGroupCaptionStyle": "group bands are card headers now — title + helper line, per the approved mockup",
        "SettingsGroupHeaderTextStyle": "group captions use SettingsGroupCaptionStyle",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        "Typography.Title": "replaced by SettingsRowTitleStyle / SettingsCardTitleStyle",
        "Settings.ThemesSubtitle": "text of the removed duplicate page subtitle",
    },
    "SettingsAboutSection.xaml": {
        # staticres
        "InfoGlyphBadgeStyle": "replaced by SettingsIconTileStyle",
        "InfoGlyphTextStyle": "replaced by SettingsIconGlyphStyle",
        "SettingsGroupHeaderTextStyle": "group captions removed — the approved mockup has card titles, not band captions",
        "Typography.BodySmall": "replaced by SettingsRowMetaStyle / SettingsCardNoteStyle",
        "Typography.Caption": "replaced by SettingsRowMetaStyle",
        "Typography.H1": "duplicate page title removed — the shell header strip owns it",
        "Typography.Subtitle": "duplicate page subtitle removed — the shell header strip owns it",
        "Typography.Title": "replaced by SettingsCardTitleStyle / SettingsRowTitleStyle",
        # dynres
        "Settings.Application": "group caption text removed with the band captions",
        "Settings.HowProglassWorksHood": "duplicate page subtitle text removed with the local title block",
        "Settings.SecAbout": "text of the removed duplicate page title (the shell strip shows the page name)",
        "Weight.Bold": "grammar type roles carry the weights",
    },
}


def _check_no_duplicate_pages(source: str) -> None:
    """A page may appear ONCE in JUSTIFIED.

    Two blocks for the same page is not a merge conflict the reader can see —
    the later dict silently replaces the earlier one, so every justification
    the first block held stops being applied. That happened while the band
    captions were converted, and the symptom (a page failing for a removal it
    had documented) looks exactly like a missing entry.
    """
    names = re.findall(r'^    "(Settings\w+\.xaml)": \{$', source, re.M)
    dupes = sorted({n for n in names if names.count(n) > 1})
    if dupes:
        raise SystemExit("verify_bindings: duplicate JUSTIFIED entries for "
                         + ", ".join(dupes))


def collect(text: str) -> dict[str, set[str]]:
    out: dict[str, set[str]] = {}
    for name, pattern in PATTERNS.items():
        # Normalise internal whitespace: a restyle re-indents the tree,
        # and a multi-line {RelativeSource\n AncestorType=…} binding is the
        # same reference no matter how deep it now sits.
        found = {" ".join(m.group(1).split()) for m in pattern.finditer(text)}
        found.discard("")
        out[name] = found
    return out


def committed(path: Path, rev: str) -> str | None:
    rel = path.relative_to(REPO).as_posix()
    try:
        return subprocess.run(
            ["git", "show", f"{rev}:{rel}"],
            cwd=REPO, capture_output=True, text=True, check=True,
        ).stdout
    except subprocess.CalledProcessError:
        return None


def main() -> int:
    _check_no_duplicate_pages(Path(__file__).read_text(encoding="utf-8"))
    ap = argparse.ArgumentParser()
    ap.add_argument("--against", default="HEAD")
    ap.add_argument("files", nargs="*")
    args = ap.parse_args()

    files = ([Path(f) for f in args.files] if args.files
             else sorted(SECTIONS.glob("*.xaml")))

    problems = 0
    for path in files:
        if not path.is_absolute():
            path = REPO / path
        if not path.exists():
            print(f"skip  {path.name}: not found")
            continue

        before = committed(path, args.against)
        if before is None:
            print(f"new   {path.name}: not in {args.against}, nothing to compare")
            continue

        old, new = collect(before), collect(path.read_text(encoding="utf-8"))

        lost: list[str] = []
        for kind in PATTERNS:
            # Resource lookups legitimately change: a restyle replaces the
            # generic surface styles with the Settings grammar. Bindings,
            # names, params and converters must survive.
            if kind in ("staticres", "dynres"):
                missing = old[kind] - new[kind]
                # A grammar swap moves surface keys onto Ios26/Settings keys;
                # report only keys that vanished without a replacement of the
                # same shape (i.e. still referenced nowhere at all).
                surface_only = {k for k in missing
                                if k.startswith(("Surface", "SettingsCard",
                                                 "Ios26", "SettingsPage",
                                                 "SettingsInner", "SettingsFlat",
                                                 "SettingsBare", "SettingsInteractive"))}
                missing -= surface_only
                if missing:
                    lost.append(f"{kind}: {', '.join(sorted(missing))}")
                continue

            missing = old[kind] - new[kind]
            if missing:
                lost.append(f"{kind}: {', '.join(sorted(missing))}")

        justified = JUSTIFIED.get(path.name, {})
        unexplained: list[str] = []
        for line in lost:
            kind, _, keys = line.partition(": ")
            remaining = [k.strip() for k in keys.split(",")
                         if k.strip() not in justified]
            if remaining:
                unexplained.append(f"{kind}: {', '.join(remaining)}")

        if unexplained:
            problems += 1
            print(f"FAIL  {path.name}")
            for line in unexplained:
                print(f"        lost {line}")
        elif lost:
            print(f"ok    {path.name}  ({len(justified)} justified removal(s))")
        else:
            added = sum(len(new[k] - old[k]) for k in PATTERNS)
            print(f"ok    {path.name}"
                  + (f"  (+{added} reference(s) added)" if added else ""))

    print()
    if problems:
        print(f"{problems} page(s) lost references — refusing. "
              "Restore them or justify each one explicitly.")
        return 1
    print("No reference regressions.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
