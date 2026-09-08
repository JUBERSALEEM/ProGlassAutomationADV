# Phase 2 — One Rounding Policy for All Money

**Severity: Critical · Estimate: 2-3 days · Prerequisite: green build**

## The problem

The money pipeline uses two different rounding rules for the same operation.

- **122** call sites use `Math.Round(x, 2)`, whose default is
  `MidpointRounding.ToEven` — banker's rounding.
- **8** call sites pass `MidpointRounding.AwayFromZero` explicitly, all in
  `Services/ProformaInvoice/InvoiceRoundTo.cs` and
  `Services/Reporting/VatCalculator.cs`.

So the pricing engine rounds a half-fil toward the even digit and the invoice
layer rounds it away from zero. On any invoice whose unrounded values land
exactly on a half, `subtotal + vat` will not equal `grand` — and it will fail
reproducibly, on the same invoices, every time.

## A comment that actively misleads

`Services/Reporting/VatCalculator.cs:38-41`:

```csharp
/// <summary>
/// Rounding strategy (banker's rounding for financial accuracy)
/// </summary>
private const MidpointRounding ROUNDING = MidpointRounding.AwayFromZero;
```

`AwayFromZero` is **not** banker's rounding. Banker's rounding is `ToEven`. The
value may well be the right choice — but the comment tells the next maintainer
the opposite of what the code does, in the one class that is supposed to be the
single source of VAT arithmetic.

## Steps

### 2.1 — Decide the policy, and write it down

This is a business decision, not a technical one, and it must be made before any
code changes. The usual answer for VAT jurisdictions is:

> Round half away from zero, at 2 decimal places, at the **line** level. Extend
> by multiplying the rounded unit rate by quantity. Never round an intermediate
> and then round the product of the rounded value.

Record it in a new `docs/MONEY_ROUNDING_POLICY.md` stating:
- the rounding mode,
- the decimal places,
- **at which step** rounding happens (line vs subtotal vs total),
- which currency minor unit applies when the policy is per-currency (see Phase 5),
- and one worked example per build type in the pricing engine.

Without that document, this phase will be reverted by the next person who
"fixes" a rounding call in isolation.

### 2.2 — Give the policy one home

Create `Services/Reporting/MoneyRounding.cs`:

```csharp
public static class MoneyRounding
{
    public const int DecimalPlaces = 2;
    public const MidpointRounding Mode = MidpointRounding.AwayFromZero;

    public static decimal Round(decimal value) =>
        Math.Round(value, DecimalPlaces, Mode);
}
```

Then route `VatCalculator.Round` through it and keep
`VatCalculator.Round` as the public name so no call site has to change.

### 2.3 — Replace the 122 bare `Math.Round(x, 2)` calls

Enumerate them first:

```bash
grep -rn "Math\.Round(" --include=*.cs Services/ProformaInvoice \
  Services/TaxInvoice Services/Reporting Models/ | grep -v MidpointRounding
```

**Do not do this with a global find-and-replace.** Three of those calls round
quantities or square metres, not money, and rounding a count away-from-zero
changes results. Read each one and classify:

- **money** → `MoneyRounding.Round(...)`
- **quantity / area / percentage** → leave, or give it a named
  `QuantityRounding` if it has its own rule
- **display only** → leave, and say so in a comment

### 2.4 — Fix the misleading comment

Whatever 2.1 decides, the comment at `VatCalculator.cs:38-40` must describe the
value actually assigned.

### 2.5 — Make the pipeline reconcile

Add a property-based test asserting, for a wide spread of generated line sets:

```
sum(round(line)) == round(sum(lines))  OR  the difference is <= 0.01 * lineCount
subtotal + vat   == grand
```

The second assertion is the one that currently fails. Write it before changing
anything and record which inputs fail — that list is the real bug report, and it
tells you whether the pricing layer or the invoice layer was the wrong one.

### 2.6 — Document the pricing engine's deliberate intermediate rounding

`SpecificationTargetCalculator` rounds the base **rate** to 2dp before applying
the uplift, with a good reason recorded in the code: it reconciles with the
arithmetic the shop writes on the card by hand.

```csharp
decimal basePerSqm = Math.Round(preUplift, 2);
result.SpecificationTargetPerSqm = Math.Round(basePerSqm * upliftFactor, 2);
```

That is a legitimate choice, but it is a **third** rounding before
`rate × area` is rounded again. Keep the behaviour; move the justification into
`MONEY_ROUNDING_POLICY.md` so it survives contact with 2.3.

## Acceptance criteria

- [ ] `docs/MONEY_ROUNDING_POLICY.md` exists and states the mode, the precision,
      and the step at which rounding occurs.
- [ ] Exactly one declaration of the rounding mode for money; every money
      rounding routes through it.
- [ ] The comment at `VatCalculator.cs:38-40` matches the code.
- [ ] `subtotal + vat == grand` holds for every generated invoice in the
      property-based test, with no tolerance.
- [ ] Every remaining bare `Math.Round(x, 2)` in the money namespaces is either
      annotated as deliberately non-monetary or converted.

## Must not change

- The pricing **formula** in `SpecificationTargetCalculator`. Only the rounding
  calls around it. `TargetMarginTolerance.AtTargetPercent` is pinned by
  `ProfitMarginKpiTests.cs` and measures against the *base* margin — changing it
  moves every margin figure in the product.
- `SavedStatement`'s money columns are mapped `[Column(TypeName = "TEXT")]`.
  Rounding changes do not migrate column types; that is a separate, larger piece
  of work and must not be bundled here.

## Verification

```bash
grep -rn "Math\.Round(.*MidpointRounding" --include=*.cs . | grep -v obj
# every money rounding should now be in MoneyRounding.cs only

grep -rn "Math\.Round(" --include=*.cs Services/Reporting | grep -v MidpointRounding
# should be empty, or each hit annotated
```
