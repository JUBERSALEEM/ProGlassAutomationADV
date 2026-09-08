# Phase 4 — Licensing Hardening

**Severity: Major · Estimate: 2-3 days · Prerequisite: green build**

The licensing cryptography is genuinely good: RSA-PSS only, with the PKCS#1 v1.5
fallback explicitly removed and the reasoning recorded; a production key *ring*
so re-issued licenses coexist with ones already in the field; placeholder keys
detected and explained rather than silently rejecting everything.

The gaps are all in the **enforcement layer above** the crypto.

## Findings

### 4.1 — Gating fails open on an unknown module key

`Services/Licensing/LicenseGate.cs`:

```csharp
if (!Map.TryGetValue(moduleKey, out var required)) return LicenseGateResult.Allow();
```

`Map` keys are plain strings duplicating the navigation rail's keys. Rename
`"Optimization"` in the rail and the gate for the paid cutting-optimisation
engine silently stops applying — no compile error, no failing test, feature
unlocked. Same for `"Cutting"`, `"Polish"`, `"Mitring"`, `"JobProfitability"`.

**Fix:** make the keys a shared enum or a set of `const string` referenced by
*both* the rail and the gate, so a rename breaks the build. Then invert the
default for keys that are *supposed* to be gated — an unrecognised key in a
gated module should deny, or at minimum log loudly.

Add a test that enumerates every navigation key the rail can emit and asserts
each one is present in `Map`. That test is the ratchet that keeps this fixed.

### 4.2 — Gating is advisory, not a boundary

The file's own header:

> The Gate does not block the user from OPENING a view. It blocks the
> NAVIGATION. If the module is already loaded in the ViewModel, the user can
> still scroll to it.

`LicenseGate` sits in the navigation switch. Anything that reaches a gated
feature by another route — a command, a deep link, a restored workspace, the AI
agent — bypasses it entirely.

**Fix:** move enforcement into the service layer, alongside `OperationGuard`.
The pattern already exists: `OperationGuard.Demand(permission, description)`
throws `PermissionDeniedException` from the service. Add the equivalent
`LicenseGate.Demand(feature, description)` at the top of every gated service
operation, so the boundary is where the work happens rather than where the
button is.

### 4.3 — `LicenseVerifier`'s header contradicts itself

Lines 12-16 state the verifier "is no longer the active runtime authority after
the secure offline activation redesign". Lines 20-22 state it "is the ONLY piece
of licensing code the app's runtime calls on every cold start".

Both cannot be true. Determine which by reading `LicenseService.Part1.cs` and
`OfflineActivationStore.cs`, then delete the wrong paragraph. A licensing module
whose entry point is documented two contradictory ways will be changed
incorrectly.

### 4.4 — An unbound license is valid on any machine

```csharp
if (!string.IsNullOrEmpty(payload.MachineFingerprint)) { /* check */ }
```

A payload signed without a fingerprint passes on every machine. That may be an
intended floating-license feature — but it is undocumented, and the generator
can produce one by omission rather than by decision.

**Fix:** add an explicit `BindingMode` to the payload (`MachineBound` /
`Floating`). A license with neither is `Invalid`, not silently floating. Make the
generator require the choice.

### 4.5 — Trial grants every feature

```csharp
if (LicenseService.Current.Status == LicenseStatus.Trial)
    return LicenseGateResult.Allow();
```

The comment explains why (the trial payload carries no feature list, so
`FeatureAllowed` would deny everything). The mechanism is right; the consequence
— a trial is functionally the top edition — is a commercial decision that should
be written down somewhere a salesperson will find it, not only in a code comment.

## Acceptance criteria

- [ ] Navigation keys and gate keys share one declaration; renaming one breaks
      the build.
- [ ] A test enumerates every navigable module and asserts gate coverage.
- [ ] At least the highest-value gated operations (`Cutting.*`, `Optimization`)
      are enforced in the service layer, not only in navigation.
- [ ] `LicenseVerifier`'s header states one truth about when it runs.
- [ ] A license without an explicit binding mode is rejected.

## Must not change

- The RSA-PSS-only verification path and the reason it excludes v1.5.
- The production key ring. Collapsing it to one key breaks licenses in the field.
- `ReadOnlyGate`. It is the boundary that keeps customer data readable when a
  license lapses, which is the correct commercial behaviour.
- The placeholder-key detection messages. They are the difference between a
  support ticket and a diagnosis.
