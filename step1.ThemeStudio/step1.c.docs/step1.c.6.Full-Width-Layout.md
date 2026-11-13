# step1.c.6 — Full-width compact Theme Studio

Latest user request: adapt to full screen, make the page more compact and use empty width for cards. This supersedes earlier fixed maximum-width / paired-row layout descriptions, not previous acceptance limitations.

## Implemented

- MainWindow starts maximized with normal Windows restore/maximize behavior. Restored sizing/minimums still work; no kiosk or borderless mode.
- Studio fills the viewport instead of being capped at 1440 DIPs.
- Compact wrapping header combines title, real theme/appearance selectors and active status.
- One column below 980 content DIPs, two below 1500, three at/above 1500. Eight existing card controls flow into independent native StackPanel lanes inside a star-sized Grid. At breakpoint changes, measure actual desired card heights and assign the next card to the shortest lane. No absolute positioning, duplicated view models or replacement controls. Each lane grows naturally, eliminating blank shared-row cells below shorter cards. Bottoms need not be identical and this is not an optimal mathematical bin packer.
- Explicit root-source DataContext binding preserves the view model while cards are temporarily reparented; keyboard focus is restored where possible. Runtime verification is still required for editing/focus/selection preservation and keyboard traversal. Lane reading/tab order is top-to-bottom within each column.
- Bento Light/Dark padding: page/card 16 DIPs, plates 12, card gutters 10, item spacing 8, sections 12. Typography and native minimum control targets remain unchanged.
- Token browser has a 320-DIP maximum internal scroll viewport. All values and search remain available without a many-screen-tall card. The unused PageMaxWidth contract role is replaced with ReferenceListMaxHeight; typed token count remains 77.

## Files/responsibilities

- MainWindow.xaml: maximized startup.
- b.1.ThemeStudioView.xaml/.cs: compact header, three native lanes, measurement/breakpoint placement/focus/context preservation. Presentation logic only.
- b.11.TokensPanel.xaml: bounded scrollable token results.
- ThemeBento.Light/Dark.xaml: centralized compact spacing and list viewport token.
- ThemeContract.cs / contract README: renamed actual consumer role.
- d.1.RuntimeChecks.cs: restore before resizing, add 1920/2560 widths, verify third lane, eight cards, preserved input binding/selected row and non-overlapping bounds.
- d.6.verify_foundation.py: source guards for maximized/full-width/lane/context layout, alongside cleanup/startup guards.
- AGENTS, design system, Bento spec and Studio README: canonical responsive/compact rules reconciled.

## Verification

Portable source/isolation checks PASS: 77 typed tokens × 2, 76 contrast pairs, 20 XML/XAML documents, 29 style targets. All 11 temporary-copy regression mutations PASS. Foundation guard PASS: 47 legacy files remain absent, 22 Compile /19 Page items valid, full-width maximized layout guards present. git diff --check PASS.

No SDK installation. The user subsequently authorized immediate commit/push of completed work; this layout and the permanent Git/PR safeguards are being synchronized to the session branch. No PR creation or Windows verification is implied. The Windows workflow remains inactive at cols/ci/windows-wpf.yml, as explicitly requested earlier; it has not run. Local Windows/.NET/WPF rendering is unavailable. No Windows build, runtime layout/focus/wheel test, visual screenshot, DPI/accessibility or performance result is claimed. On Windows inspect both modes at restored and maximized sizes and 100/150/200% scale, compare card density and column flow, then correct/regress before acceptance.
