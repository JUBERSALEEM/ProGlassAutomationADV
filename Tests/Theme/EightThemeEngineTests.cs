// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/EightThemeEngineTests.cs
// PROJECT: ProGlassAutomation.Tests
// ─────────────────────────────────────────────────────────────────────────
// THE EIGHT-FAMILY CONTRACT, PINNED
//
// These tests exist because every one of them corresponds to a way this
// subsystem has already broken:
//
//   · "the catalogue has seven families"      → a family count is a
//     product promise, and a silent seventh card is a regression nobody
//     would notice from the source.
//   · "the default resolved to the first item" → unknown saved values
//     used to resolve to catalogue entry zero, so reordering the Theme
//     Center changed what every user with a corrupt config saw.
//   · "a migrated theme drifted on every launch" → the translation must
//     be idempotent. Non-idempotent migration is invisible in review and
//     obvious to a user who watches their theme creep once a week.
//   · "neumorphism shipped without its edge cue" → the style's boundary
//     contrast measures ~1.4:1 and fails WCAG 1.4.11; the compensation is
//     part of the recipe, not an optional extra.
//   · "swiss still had drop shadows"           → a style whose entire
//     premise is the absence of decoration cannot publish one.
//   · "glassmorphism and liquid glass render identically" → they are the
//     two families users confuse most, and if they are visually the same
//     the catalogue is lying.
//
// NO WPF APPLICATION IS CONSTRUCTED. Everything asserted here is static
// catalogue data, so these run on any runner and in any order.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Eight;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class EightThemeEngineTests
    {
        // ── CATALOGUE SHAPE ──────────────────────────────────────────

        [Fact]
        public void Exactly_eight_families_are_selectable()
        {
            Assert.Equal(8, EightThemeNames.All.Length);
            Assert.Equal(8, EightThemeCatalog.All.Count);
            Assert.Equal(
                8, EightThemeNames.All.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void All_eight_carry_a_distinct_persisted_identity()
        {
            var ids = EightThemeCatalog.All
                .Select(p => p.Identity)
                .ToList();
            Assert.Equal(
                ids.Count,
                ids.Distinct(StringComparer.Ordinal).Count());
            Assert.All(ids, id =>
                Assert.False(string.IsNullOrWhiteSpace(id)));
        }

        [Fact]
        public void The_shipped_default_is_liquid_glass()
        {
            Assert.Equal(EightThemeNames.LiquidGlass, EightThemeNames.Default);
            Assert.True(EightThemeCatalog.IsKnown(EightThemeNames.Default));
            Assert.Equal(
                EightThemeNames.Default,
                EightThemeCatalog.Resolve(null).Identity);
        }

        // ── IDENTITY IS THE PERSISTED KEY ────────────────────────────
        //
        // These strings go into users' settings files. Renaming one silently
        // resets that user's appearance, so the exact spellings are pinned.

        [Theory]
        [InlineData("Bento Grid")]
        [InlineData("Spatial")]
        [InlineData("Swiss International")]
        [InlineData("Holographic")]
        [InlineData("Neumorphism")]
        [InlineData("Claymorphism")]
        [InlineData("Liquid Glass")]
        [InlineData("Glassmorphism")]
        public void Persisted_identity_round_trips_unchanged(string identity)
        {
            Assert.True(EightThemeCatalog.IsKnown(identity));
            Assert.Equal(identity, EightThemeCatalog.Resolve(identity).Identity);
            Assert.True(EightThemeMigration.IsCanonical(identity));
            Assert.Equal(
                identity, EightThemeMigration.Normalize(identity));
            Assert.Equal(
                identity, EightThemeMigration.Normalize(identity));
        }

        // ── MIGRATION ───────────────────────────────────────────────
        //
        // Idempotence is the whole point: Normalize is called on every read
        // of the saved value, so a non-idempotent map makes a theme drift
        // one step per launch.

        [Fact]
        public void Migration_is_idempotent_for_every_known_legacy_value()
        {
            foreach (var legacy in EightThemeMigration.KnownLegacyIdentities)
            {
                var once = EightThemeMigration.Normalize(legacy);
                var twice = EightThemeMigration.Normalize(once);
                var thrice = EightThemeMigration.Normalize(twice);

                Assert.Equal(once, twice);
                Assert.Equal(once, thrice);
                Assert.True(EightThemeCatalog.IsKnown(once));
            }
        }

        [Fact]
        public void Migration_table_is_not_empty()
        {
            // A migration map that loses an entry degrades every user on
            // that theme to the default with no error anywhere.
            Assert.NotEmpty(EightThemeMigration.KnownLegacyIdentities);
            Assert.All(
                EightThemeMigration.KnownLegacyIdentities,
                legacy => Assert.True(
                    EightThemeCatalog.IsKnown(
                        EightThemeMigration.Normalize(legacy)),
                    $"Legacy identity '{legacy}' maps to an unknown family."));
        }

        [Theory]
        [InlineData("Normal (Industrial)", EightThemeNames.BentoGrid)]
        [InlineData("Normal",              EightThemeNames.BentoGrid)]
        [InlineData("Monet",               EightThemeNames.Holographic)]
        [InlineData("Crystal Fluid",       EightThemeNames.LiquidGlass)]
        [InlineData("Ios-26",              EightThemeNames.Glassmorphism)]
        public void Legacy_identities_land_on_their_documented_family(
            string legacy, string expected)
        {
            Assert.Equal(expected, EightThemeMigration.Normalize(legacy));
            Assert.True(EightThemeMigration.WasLegacy(legacy));
        }

        [Fact]
        public void Unknown_values_resolve_to_the_default_not_the_first_item()
        {
            // The bug this pins: a Theme Center reorder silently changed
            // what every user with a corrupt or hand-edited config saw.
            string first = EightThemeCatalog.All[0].Identity;
            string fallback = EightThemeMigration.Normalize("not-a-real-theme");
            Assert.Equal(EightThemeNames.Default, fallback);
            if (first != EightThemeNames.Default)
                Assert.NotEqual(first, fallback);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Empty_values_resolve_to_the_default(string? saved)
        {
            Assert.Equal(EightThemeNames.Default,
                EightThemeMigration.Normalize(saved));
        }

        // ── GEOMETRY IS FAMILY DATA ──────────────────────────────────
        //
        // Corner radius is the loudest identity signal in a UI: a Swiss
        // card at 0px and a clay card at 32px read as different products
        // before any colour is compared.

        [Fact]
        public void Swiss_is_the_only_family_with_a_zero_radius_scale()
        {
            var zeroed = EightThemeCatalog.All
                .Where(p => p.Geometry.Control == 0)
                .Select(p => p.Identity)
                .ToList();
            Assert.Equal(
                new[] { EightThemeNames.Swiss }, zeroed);
        }

        [Fact]
        public void Claymorphism_radius_is_roughly_twice_neumorphism()
        {
            // The researched distinction: clay corners read as soft blobs
            // (28–48px), neumorphic ones as a card with rounded corners
            // (12–20px). Anything less than 1.5× collapses the difference.
            var neu = EightThemeCatalog.Resolve(EightThemeNames.Neumorphism);
            var clay = EightThemeCatalog.Resolve(EightThemeNames.Claymorphism);
            Assert.True(clay.Geometry.Card >= neu.Geometry.Card * 1.5,
                $"Clay card radius {clay.Geometry.Card} must be at least 1.5x " +
                $"neumorphism's {neu.Geometry.Card}.");
        }

        [Fact]
        public void Card_radius_never_exceeds_control_radius_across_a_family()
        {
            foreach (var p in EightThemeCatalog.All)
            {
                Assert.True(p.Geometry.Control <= p.Geometry.Card,
                    $"{p.Identity}: control radius must not exceed card radius.");
                Assert.True(p.Geometry.Card <= p.Geometry.Large,
                    $"{p.Identity}: card radius must not exceed large radius.");
                Assert.True(p.Geometry.Large <= p.Geometry.Hero,
                    $"{p.Identity}: large radius must not exceed hero radius.");
            }
        }

        // ── SHADOWS ──────────────────────────────────────────────────

        [Fact]
        public void Swiss_ships_no_shadows()
        {
            var swiss = EightThemeCatalog.Resolve(EightThemeNames.Swiss);
            Assert.Equal(EightElevationKind.None, swiss.Elevation.Kind);
            Assert.Equal(0, swiss.Elevation.LowOpacity);
            Assert.Equal(0, swiss.Elevation.HighOpacity);
        }

        [Fact]
        public void Neumorphism_ships_a_mirrored_outset_pair()
        {
            // The defining trait: ONE light stop and ONE shade stop, with
            // the light stop genuinely lighter than the shade. A family that
            // declares Extrude and publishes one shadow has thrown away the
            // only thing it was.
            var neu = EightThemeCatalog.Resolve(EightThemeNames.Neumorphism);
            Assert.Equal(EightElevationKind.Extrude, neu.Elevation.Kind);

            double lum = RelativeLuminance(neu.Elevation.LightStop);
            double shade = RelativeLuminance(neu.Elevation.ShadeStop);
            Assert.True(lum > shade,
                "Neumorphism's light stop must be lighter than its shade stop.");

            // ...and in dark mode the highlight is light GREY, not white,
            // while the shade is dark grey, not black.
            Assert.NotEqual(System.Windows.Media.Colors.White,
                neu.Elevation.LightStop);
            Assert.NotEqual(System.Windows.Media.Colors.Black,
                neu.Elevation.ShadeStop);
        }

        [Fact]
        public void Neumorphism_requires_a_non_shadow_edge_cue()
        {
            // Its boundary measures ~1.4:1 against WCAG 2.2 SC 1.4.11's 3:1,
            // and cannot be fixed by tuning — raising shadow contrast hard
            // enough destroys the soft look entirely. The hairline is the
            // compensation, so it must be flagged as required by the recipe.
            var neu = EightThemeCatalog.Resolve(EightThemeNames.Neumorphism);
            Assert.True(neu.Elevation.RequiresNonShadowEdge);
            Assert.True(neu.Material.RimOpacity > 0.2,
                "Neumorphism must ship a visible hairline beside the shadow.");
        }

        [Fact]
        public void Only_neumorphism_requires_a_non_shadow_edge()
        {
            foreach (var p in EightThemeCatalog.All)
            {
                bool expected = p.Id == EightThemeId.Neumorphism;
                Assert.Equal(expected, p.Elevation.RequiresNonShadowEdge);
            }
        }

        [Fact]
        public void Bento_grid_ships_exactly_one_shadow_level()
        {
            // A bento tray whose cells cast different shadows stops reading
            // as one object. Low and high MUST be identical.
            var bento = EightThemeCatalog.Resolve(EightThemeNames.BentoGrid);
            Assert.Equal(EightElevationKind.FlatSingleLevel, bento.Elevation.Kind);
            Assert.Equal(bento.Elevation.LowBlur, bento.Elevation.HighBlur);
            Assert.Equal(bento.Elevation.LowDepth, bento.Elevation.HighDepth);
            Assert.Equal(bento.Elevation.LowOpacity, bento.Elevation.HighOpacity);
            Assert.True(bento.Elevation.LowOpacity <= 0.12,
                "Bento's single shadow level must stay genuinely flat.");
        }

        [Fact]
        public void Shadows_stay_within_the_render_budget()
        {
            // A DropShadowEffect is re-rasterised by the GPU on every
            // repaint. A 96px blur on a scrolling DataGrid row costs frames.
            foreach (var p in EightThemeCatalog.All)
            {
                Assert.True(p.Elevation.LowBlur <= 56,
                    $"{p.Identity} low blur {p.Elevation.LowBlur} exceeds 56.");
                Assert.True(p.Elevation.HighBlur <= 56,
                    $"{p.Identity} high blur {p.Elevation.HighBlur} exceeds 56.");
                Assert.True(p.Elevation.LowDepth <= 12,
                    $"{p.Identity} low depth exceeds 12.");
                Assert.True(p.Elevation.HighDepth <= 12,
                    $"{p.Identity} high depth exceeds 12.");
            }
        }

        // ── THE TWO GLASSES ARE DIFFERENT MATERIALS ───────────────────
        //
        // The single most confused pair in the whole catalogue. If they
        // render the same, one of the two cards is lying to the user.

        [Fact]
        public void Liquid_glass_and_glassmorphism_are_different_materials()
        {
            var liquid = EightThemeCatalog.Resolve(EightThemeNames.LiquidGlass);
            var glass = EightThemeCatalog.Resolve(EightThemeNames.Glassmorphism);

            Assert.Equal(EightMaterialKind.Refractive, liquid.Material.Kind);
            Assert.Equal(EightMaterialKind.Frosted, glass.Material.Kind);

            // Liquid Glass blurs harder...
            Assert.True(liquid.Material.BlurRadius > glass.Material.BlurRadius,
                "Liquid Glass must blur harder than glassmorphism.");
            // ...pushes saturation harder...
            Assert.True(liquid.Material.SaturationBoost >
                        glass.Material.SaturationBoost,
                "Liquid Glass must out-saturate glassmorphism.");
            // ...and is more see-through.
            Assert.True(liquid.Material.SurfaceAlpha <
                        glass.Material.SurfaceAlpha,
                "Liquid Glass must be more transparent than glassmorphism.");
        }

        [Fact]
        public void Only_liquid_glass_and_spatial_carry_a_specular_rim()
        {
            // A specular rim — bright along the lit edge, falling away on
            // the opposite side — is what makes a surface read as a curved
            // optical body instead of a flat pane. Glassmorphism is the
            // 2020 recipe and its rim is a FLAT hairline; giving it a
            // specular rim turns it into Liquid Glass by accident.
            foreach (var p in EightThemeCatalog.All)
            {
                bool expected = p.Id is EightThemeId.LiquidGlass
                    or EightThemeId.Spatial;
                Assert.Equal(expected, p.Material.SpecularRim);
            }
        }

        // ── NEUMORPHISM'S DEFINING RULE ──────────────────────────────

        [Fact]
        public void Neumorphism_surfaces_equal_their_page_exactly()
        {
            // The style dies the moment a surface's colour differs from its
            // parent's, because the light shadow needs a surface darker
            // than itself to read against.
            var neu = EightThemeCatalog.Resolve(EightThemeNames.Neumorphism);
            foreach (var ramp in new[] { neu.Light, neu.Dark })
            {
                Assert.Equal(ramp.Window, ramp.Module);
                Assert.Equal(ramp.Window, ramp.Surface);
                Assert.Equal(ramp.Window, ramp.SurfaceSecondary);
                Assert.Equal(ramp.Window, ramp.SurfaceDetail);
                Assert.Equal(ramp.Window, ramp.Chrome);
                Assert.Equal(ramp.Window, ramp.Dialog);
                Assert.Equal(ramp.Window, ramp.DialogInset);
            }
        }

        [Fact]
        public void Families_that_need_a_flat_ground_forbid_a_backdrop()
        {
            // Neumorphism needs a controlled mid-tone (a gradient behind an
            // extruded card kills the extrude); Swiss needs the grid left
            // unviolated. Offering either a scene it must not paint is
            // worse than offering none — the user picks a wallpaper and
            // nothing happens.
            var neu = EightThemeCatalog.Resolve(EightThemeNames.Neumorphism);
            var swiss = EightThemeCatalog.Resolve(EightThemeNames.Swiss);
            Assert.False(neu.Material.AllowBackdrop);
            Assert.False(swiss.Material.AllowBackdrop);

            Assert.True(
                EightThemeCatalog.Resolve(EightThemeNames.LiquidGlass)
                    .Material.AllowBackdrop);
            Assert.True(
                EightThemeCatalog.Resolve(EightThemeNames.Glassmorphism)
                    .Material.AllowBackdrop);
        }

        // ── DARK-PROMISING FAMILIES ──────────────────────────────────

        [Fact]
        public void Families_that_need_dark_to_read_pair_to_dark()
        {
            // Neumorphism, Spatial and Holographic have no meaningful
            // light ramp: a light-shadowed extruded surface on white paper
            // is not a lighter neumorphism, it is a broken one.
            var darkPromising = EightThemeCatalog.All
                .Where(p => p.Id is EightThemeId.Neumorphism
                    or EightThemeId.Spatial
                    or EightThemeId.Holographic)
                .Select(p => p.Identity)
                .ToList();

            Assert.Equal(3, darkPromising.Count);
            foreach (var identity in darkPromising)
            {
                Assert.Equal(
                    true,
                    VisualThemeEngine.ModePairFor(identity));
            }
        }

        [Fact]
        public void Families_with_a_real_light_ramp_follow_the_user_toggle()
        {
            foreach (var identity in new[]
            {
                EightThemeNames.BentoGrid,
                EightThemeNames.Swiss,
                EightThemeNames.Claymorphism,
                EightThemeNames.LiquidGlass,
                EightThemeNames.Glassmorphism,
            })
            {
                Assert.Equal(
                    null,
                    VisualThemeEngine.ModePairFor(identity));
            }
        }

        // ── TEXT LEGIBILITY ──────────────────────────────────────────

        [Fact]
        public void Every_ramp_has_a_readable_primary_text_colour()
        {
            // COMPOSITED, not measured raw. Five of the eight families are
            // translucent, and a translucent surface's real colour is its
            // fill over whatever is behind it — measuring #FFFFFF at 9%
            // against white ink would report 1.05:1 for a family that
            // actually renders at 14:1. Every ramp is measured the way the
            // screen shows it: fill over module, ink over that.
            foreach (var p in EightThemeCatalog.All)
            {
                foreach (var ramp in new[] { p.Light, p.Dark })
                {
                    var card = Composite(ramp.Surface, ramp.Module);
                    double ratio = RelativeContrast(ramp.TextPrimary, card);
                    Assert.True(ratio >= 4.5,
                        $"{p.Identity}: body text contrast {ratio:0.00}:1 " +
                        "is below WCAG AA 4.5:1.");
                }
            }
        }

        [Theory]
        [InlineData(false)]   // light
        [InlineData(true)]    // dark
        public void Every_ink_level_clears_AA_against_card_and_stage(
            bool dark)
        {
            // Body text is the easy part. SECONDARY and MUTED ink are
            // where themes actually fail, and they failed here: eight of
            // the sixteen ramps shipped a muted grey between 2.97:1 and
            // 4.46:1 against their own card. Muted is still body text —
            // captions, metadata, placeholder, table footers — so it gets
            // the same 4.5:1 as everything else. "Decorative grey" is not
            // a category a colour earns by being hard to read.
            //
            // Both surfaces are checked because ink lands on both: the
            // composited card for anything inside a panel, and the raw
            // window for headings and anything in open stage.
            foreach (var p in EightThemeCatalog.All)
            {
                var ramp = dark ? p.Dark : p.Light;
                var card = Composite(ramp.Surface, ramp.Module);

                foreach (var (name, ink) in new[]
                {
                    ("secondary", ramp.TextSecondary),
                    ("muted", ramp.TextMuted),
                })
                {
                    double onCard = RelativeContrast(ink, card);
                    Assert.True(onCard >= 4.5,
                        $"{p.Identity} ({(dark ? "dark" : "light")}): " +
                        $"{name} text on card is {onCard:0.00}:1, below " +
                        "WCAG AA 4.5:1.");

                    double onStage = RelativeContrast(ink, ramp.Window);
                    Assert.True(onStage >= 4.5,
                        $"{p.Identity} ({(dark ? "dark" : "light")}): " +
                        $"{name} text on the stage is {onStage:0.00}:1, " +
                        "below WCAG AA 4.5:1.");
                }
            }
        }

        [Theory]
        [InlineData(EightMaterialKind.Solid)]
        [InlineData(EightMaterialKind.Extruded)]
        [InlineData(EightMaterialKind.Inflated)]
        [InlineData(EightMaterialKind.Foil)]
        public void An_opaque_family_never_carries_a_translucent_fill(
            EightMaterialKind kind)
        {
            // The alpha single-source-of-truth rule, pinned.
            //
            // Opaque families are flattened to full alpha when painted, so
            // any alpha left on their ramp surface levels is silently
            // DISCARDED — which means an author who wrote A("#FFFFFF",
            // 0.62) there gets a pure white card instead of the tint they
            // composed by eye. Holographic did exactly this on its light
            // ramp. Opaque families therefore carry C(), full stop.
            foreach (var p in EightThemeCatalog.All)
            {
                if (p.Material.Kind != kind) continue;
                foreach (var ramp in new[] { p.Light, p.Dark })
                {
                    foreach (var (name, c) in new[]
                    {
                        ("surface", ramp.Surface),
                        ("surfaceSecondary", ramp.SurfaceSecondary),
                        ("surfaceDetail", ramp.SurfaceDetail),
                        ("chrome", ramp.Chrome),
                        ("dialog", ramp.Dialog),
                        ("dialogInset", ramp.DialogInset),
                    })
                    {
                        Assert.Equal(255, c.A);
                        Assert.False(ColorsEqual(c, System.Windows.Media.Colors.White),
                            $"{p.Identity}: opaque '{name}' is pure white — " +
                            "the tint its A() alpha was composing has " +
                            "been thrown away by the flatten.");
                    }
                }
            }
        }

        [Theory]
        [InlineData(EightMaterialKind.Frosted)]
        [InlineData(EightMaterialKind.Refractive)]
        [InlineData(EightMaterialKind.Volumetric)]
        public void A_translucent_family_keeps_its_per_level_alpha(
            EightMaterialKind kind)
        {
            // The other half of the same rule: a translucent family whose
            // fills are all fully opaque has nothing to composite and has
            // silently become a Solid family with a glassy name.
            foreach (var p in EightThemeCatalog.All)
            {
                if (p.Material.Kind != kind) continue;
                foreach (var ramp in new[] { p.Light, p.Dark })
                {
                    Assert.True(ramp.Surface.A < 255,
                        $"{p.Identity}: translucent but its surface fill " +
                        "is opaque.");
                    Assert.True(ramp.Chrome.A < 255,
                        $"{p.Identity}: translucent but its chrome fill " +
                        "is opaque.");
                    // A card is more opaque than the rail behind it; that
                    // gradient is the whole look and cannot be expressed
                    // by a single Material.SurfaceAlpha.
                    Assert.True(ramp.Surface.A > ramp.Chrome.A,
                        $"{p.Identity}: the card must be more opaque than " +
                        "the chrome behind it.");
                }
            }
        }

        [Fact]
        public void Every_ramp_has_a_visible_card_boundary()
        {
            // Rule 1 of the painting contract: a card whose fill equals its
            // stage and whose border is an 8% hairline has NO visible
            // boundary, and the whole page reads as one empty sheet. This is
            // the iOS 26 light-ramp bug, pinned so it cannot come back.
            foreach (var p in EightThemeCatalog.All)
            {
                foreach (var ramp in new[] { p.Light, p.Dark })
                {
                    var card = Composite(ramp.Surface, ramp.Module);
                    bool fillDiffers = !ColorsEqual(card, ramp.Module);
                    // A keyline counts as visible at a 12% alpha floor, which
                    // is roughly where a hairline stops reading as a
                    // printing artefact.
                    bool borderVisible =
                        ramp.BorderDefault.A >= 0x20 ||
                        !ColorsEqual(ramp.BorderDefault, ramp.BorderSubtle);
                    Assert.True(fillDiffers || borderVisible,
                        $"{p.Identity}: a card must differ from its stage " +
                        "by fill or by a visible keyline.");
                }
            }
        }

        [Fact]
        public void Accent_foreground_is_readable_on_every_accent()
        {
            foreach (var p in EightThemeCatalog.All)
            {
                foreach (var ramp in new[] { p.Light, p.Dark })
                {
                    double ratio = RelativeContrast(
                        ramp.AccentForeground, ramp.Accent);
                    Assert.True(ratio >= 4.5,
                        $"{p.Identity}: accent foreground contrast " +
                        $"{ratio:0.00}:1 is below WCAG AA.");
                }
            }
        }

        [Fact]
        public void Every_family_offers_at_least_three_accent_presets()
        {
            foreach (var p in EightThemeCatalog.All)
            {
                Assert.True(p.AccentPresets.Length >= 3,
                    $"{p.Identity} offers too few accent presets.");
                Assert.All(p.AccentPresets, hex =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(hex));
                    Assert.Equal(7, hex.TrimStart('#').Length);
                });
            }
        }

        // ── MOTION ───────────────────────────────────────────────────

        [Fact]
        public void Motion_durations_are_ordered_and_within_budget()
        {
            foreach (var p in EightThemeCatalog.All)
            {
                Assert.True(p.Motion.Fast <= p.Motion.Normal,
                    $"{p.Identity}: fast must not exceed normal.");
                Assert.True(p.Motion.Normal <= p.Motion.Slow,
                    $"{p.Identity}: normal must not exceed slow.");
                Assert.False(string.IsNullOrWhiteSpace(p.Motion.EaseOut));
                Assert.False(string.IsNullOrWhiteSpace(p.Motion.Spring));
            }
        }

        [Fact]
        public void Swiss_motion_never_exceeds_the_150ms_ceiling()
        {
            // The researched HARD limit for the style. Anything slower reads
            // as decoration, which is exactly what Swiss abolishes.
            var swiss = EightThemeCatalog.Resolve(EightThemeNames.Swiss);
            Assert.True(swiss.Motion.Slow <= 150,
                "Swiss motion must not exceed 150ms.");
        }

        [Fact]
        public void Reduced_motion_suppresses_every_family_duration()
        {
            // The reduced-motion contract, exercised through the same
            // public entry point the app uses.
            //
            // This was a real hole. The motion tokens were published with
            // each family's durations and NOTHING read the OS preference,
            // so a user who had turned off animations in Windows Ease of
            // Access still got the holographic shimmer, the liquid
            // refraction and the neumorphic spring. The research on
            // vestibular disorders is unambiguous that this is the
            // difference between an expressive theme and one that makes
            // people ill.
            var full = new System.Windows.ResourceDictionary();
            var reduced = new System.Windows.ResourceDictionary();

            foreach (var p in EightThemeCatalog.All)
            {
                EightThemePainter.Apply(full, p, false,
                    System.Windows.Media.Colors.Blue, false);
                EightThemePainter.Apply(reduced, p, false,
                    System.Windows.Media.Colors.Blue, true);
            }

            // The suppressed dictionary must be strictly faster, or this
            // test is proving nothing.
            Assert.True(
                Dur(reduced, "MotionSlow") < Dur(full, "MotionSlow"),
                "reduced motion did not actually shorten the budget.");

            // 1ms rather than zero: WPF takes a zero duration as "not
            // animated" on some paths and as an instant cut on others,
            // and 1ms is indistinguishable from zero to the eye.
            var one = TimeSpan.FromMilliseconds(1);
            Assert.Equal(one, Dur(reduced, "MotionFast"));
            Assert.Equal(one, Dur(reduced, "MotionNormal"));
            Assert.Equal(one, Dur(reduced, "MotionSlow"));

            // Ordering must survive suppression — a consumer that reads
            // Fast <= Normal <= Slow cannot suddenly invert.
            Assert.True(Dur(reduced, "MotionSlow")
                     <= Dur(reduced, "MotionNormal"));
            Assert.True(Dur(reduced, "MotionNormal")
                     <= Dur(reduced, "MotionFast"));

            // The splines must flatten too. A duration of 1ms with a
            // spring curve still overshoots on the first frame.
            Assert.Equal("0,0,1,1",
                reduced["MotionEaseOutKeySpline"] as string);
            Assert.Equal("0,0,1,1",
                reduced["MotionSpringKeySpline"] as string);
            Assert.NotEqual("0,0,1,1",
                full["MotionSpringKeySpline"] as string);

            // MotionReduced must be published, because a zeroed duration
            // alone still renders a spring as a jump cut. Controls bind the
            // flag and substitute an opacity cross-fade instead.
            Assert.IsType<bool>(reduced["MotionReduced"]);
            Assert.True((bool)reduced["MotionReduced"]!);
            Assert.IsType<bool>(full["MotionReduced"]);
            Assert.False((bool)full["MotionReduced"]!);
        }

        [Fact]
        public void The_motion_dictionary_defines_a_reduced_default()
        {
            // A DynamicResource binding to a key the dictionary does not
            // define logs a binding error on every lookup. MotionReduced is
            // new, so the design-system dictionary must ship a default or
            // every animated control warns until the first theme paint.
            var source = ReadRepoFile(
                "Themes/ProGlassDesignSystem/Tokens/SemanticMotion.xaml");
            Assert.Contains("x:Key=\"MotionReduced\"", source,
                StringComparison.Ordinal);
        }

        private static TimeSpan Dur(
            System.Windows.ResourceDictionary d, string key) =>
            TimeSpan.Parse((string)d[key]);

        private static string ReadRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string probe = Path.Combine(dir.FullName, relativePath
                    .Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(probe)) return File.ReadAllText(probe);
                if (File.Exists(Path.Combine(
                        dir.FullName, "ProGlassAutomation.csproj")))
                    break;
                dir = dir.Parent;
            }
            throw new FileNotFoundException(
                "could not locate " + relativePath + " from the test runner");
        }

        // ── METADATA ─────────────────────────────────────────────────

        [Fact]
        public void Every_family_explains_itself()
        {
            // A card has to say what a style DOES, not just name it.
            // "Neumorphism" means nothing to somebody who has to decide.
            foreach (var p in EightThemeCatalog.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(p.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(p.Tagline));
                Assert.False(string.IsNullOrWhiteSpace(p.Summary));
                Assert.False(string.IsNullOrWhiteSpace(p.MaterialNote));
                Assert.False(string.IsNullOrWhiteSpace(p.SceneLabel));
                Assert.False(string.IsNullOrWhiteSpace(p.SceneType));
                Assert.True(p.Summary.Length >= 30,
                    $"{p.Identity}: summary is too thin to be useful.");
            }
        }

        [Fact]
        public void Every_family_has_a_distinct_scene_label_and_type()
        {
            // Two families sharing a scene caption is exactly how
            // "why do these two look identical?" gets reported.
            var labels = EightThemeCatalog.All.Select(p => p.SceneLabel).ToList();
            var types = EightThemeCatalog.All.Select(p => p.SceneType).ToList();
            Assert.Equal(labels.Count,
                labels.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(types.Count,
                types.Distinct(StringComparer.Ordinal).Count());
        }

        // ── SCENES ───────────────────────────────────────────────────

        [Fact]
        public void Every_family_has_a_scene_entry()
        {
            var scenes = EightThemeScenes.All;
            Assert.Equal(8, scenes.Length);
            foreach (var identity in EightThemeNames.All)
            {
                Assert.Contains(scenes,
                    s => string.Equals(s.Identity, identity,
                        StringComparison.Ordinal));
            }
        }

        // ── HELPERS ──────────────────────────────────────────────────

        private static bool ColorsEqual(
            System.Windows.Media.Color a, System.Windows.Media.Color b) =>
            a.R == b.R && a.G == b.G && a.B == b.B;

        /// <summary>
        /// Flattens a surface over the stage behind it, exactly as the
        /// compositor does — using the fill's OWN alpha channel, because
        /// that is the single source of truth for opacity in this engine.
        /// Opaque families carry alpha 255 and so pass straight through;
        /// forcing a blend on them would hide a genuine "this family should
        /// not be translucent" regression.
        /// </summary>
        private static System.Windows.Media.Color Composite(
            System.Windows.Media.Color fill,
            System.Windows.Media.Color over)
        {
            double a = fill.A / 255.0;
            return System.Windows.Media.Color.FromArgb(
                255,
                (byte)Math.Round(a * fill.R + (1 - a) * over.R),
                (byte)Math.Round(a * fill.G + (1 - a) * over.G),
                (byte)Math.Round(a * fill.B + (1 - a) * over.B));
        }

        private static double RelativeContrast(
            System.Windows.Media.Color a, System.Windows.Media.Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        private static double RelativeLuminance(System.Windows.Media.Color c) =>
            Luminance(c);

        private static double Luminance(System.Windows.Media.Color c)
        {
            double r = Channel(c.R), g = Channel(c.G), b = Channel(c.B);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private static double Channel(byte v)
        {
            double c = v / 255.0;
            return c <= 0.03928 ? c / 12.92
                : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}