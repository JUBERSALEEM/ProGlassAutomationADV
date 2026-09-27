#!/usr/bin/env python3
"""Independent golden derivation for the Step 9 / Step 7 ports.

Computed from the PINNED WPF formulas (decimal arithmetic, ROUND_HALF_EVEN
for Math.Round, ROUND_HALF_UP for MoneyRounding.Round) — never from the C#
port itself. Values are pasted into Step9GoldenTests / Step7SummaryTests.
"""
from decimal import Decimal as D, ROUND_HALF_EVEN, ROUND_HALF_UP, getcontext

getcontext().prec = 50


def mr(x):  # MoneyRounding.Round (half away from zero, 2dp)
    return D(x).quantize(D("0.01"), rounding=ROUND_HALF_UP)


def r2(x):  # Math.Round(x, 2) — banker's
    return D(x).quantize(D("0.01"), rounding=ROUND_HALF_EVEN)


def r3(x):
    return D(x).quantize(D("0.001"), rounding=ROUND_HALF_EVEN)


def r4(x):
    return D(x).quantize(D("0.0001"), rounding=ROUND_HALF_EVEN)


def panel(w, h):  # SqmCalculator.CalculatePanel
    if w <= 0 or h <= 0:
        return D(0)
    a = r3(D(w) * h / D(1_000_000))
    return D("0.5") if a < D("0.5") else a


def fmt(x):
    return f"{x:.2f}"


print("═══ A. SGU priced by required area ═══")
cut = panel(1000, 2000) * 2
print("cutSqm =", cut)
yield_ = D(1) - D(15) / 100
basis = r2(D(100) / yield_ + D(5) + D(8))
print("basis =", basis)
needed = r4(cut * (1 + D(15) / 100))
print("needed =", needed)
glass_amt = r2(needed * 100)
print("glass amount =", glass_amt)
cutting = r2(cut * 5)
tempering = r2(cut * 8)
print("cutting =", cutting, "tempering =", tempering)
gross = D(1000)
net = r2(gross / D("1.05"))
print("netExVat =", net)
glass_cost = r2(glass_amt)
prod = r2(cutting + tempering)
profit = r2(net - glass_cost - prod)
print("profit =", profit)
basis_total = r2(basis * cut)
print("basisTotal =", basis_total)
margin = r2((gross - basis_total) / basis_total * 100)
print("margin% =", margin)
actual_cost = glass_cost + prod
actual_profit = r2(net - actual_cost)
print("overall% =", r2(actual_profit / actual_cost * 100))
print("netMargin% =", r2(actual_profit / net * 100))

print()
print("═══ B. DGU + spacer + wastage setup (17 shared sheets) + polish ═══")
cut_b = panel(2000, 1500) * 3
print("cutSqm =", cut_b)
# sources: outer Sqm1 = 9, inner Sqm2=0 -> innerSqm = Sqm1 = 9
sqm1 = r2(r3(panel(2000, 1500) * 3))  # footer: 3dp then 2dp
print("Sqm1 =", sqm1)
# wastage allocation: both panes on 3210x2250 (single distinct area)
sheet_sqm = r4(D(3210) * 2250 / D(1_000_000))
total_source = sqm1 * 2
util = r2(min(D(100), total_source / (17 * sheet_sqm) * 100))
print("sheetSqm =", sheet_sqm, "utilization% =", util)
useful = sheet_sqm * util / 100
w = sqm1 / useful
print("weight (each) =", w)
# exact = 17 * 0.5 = 8.5 each -> floor 8 + 8, remainder tie -> first (outer) +1
outer_sheets, inner_sheets = 9, 8
outer_amt = r2(sheet_sqm * outer_sheets * 120)
inner_amt = r2(sheet_sqm * inner_sheets * 95)
print("outer glass =", outer_amt, "(9 sheets @120)")
print("inner glass =", inner_amt, "(8 sheets @95)")
spacer_amt = r2(cut_b * 45 * 1)
print("spacer =", spacer_amt)
polish_qty = r3(D(1500) / 1000 * 2 * 3)  # BothHeightSide per row x3
print("polish LM =", polish_qty, "amount =", r2(polish_qty * 6))
glass_b = outer_amt + inner_amt
prod_b = r2(spacer_amt + r2(polish_qty * 6))
print("glassCost =", r2(glass_b), "production =", prod_b)
basis_b = r2((D(120) + 95) / (D(1) - D("0.15")) + 45 * 1)
print("basis =", basis_b, "basisTotal =", r2(basis_b * cut_b))
gross_b = D(5000)
net_b = r2(gross_b / D("1.05"))
profit_b = r2(net_b - glass_b - prod_b)
print("netExVat =", net_b, "profit =", profit_b, "isLoss =", gross_b > 0 and profit_b < 0)
margin_b = r2((gross_b - r2(basis_b * cut_b)) / r2(basis_b * cut_b) * 100)
print("margin% =", margin_b)
cost_b = glass_b + prod_b
ap_b = r2(net_b - cost_b)
print("overall% =", r2(ap_b / cost_b * 100), "netMargin% =", r2(ap_b / net_b * 100))

print()
print("═══ C. LAM with PVB ═══")
cut_c = panel(1000, 1000) * 2
print("cutSqm =", cut_c)
lam_rate = D(40) + 3 + 4
lam_amt = r2(cut_c * lam_rate)
print("lam rate =", lam_rate, "amount =", lam_amt)
glass_c = r2(r2(sheet_sqm * 1 * 110) * 2)
print("glass (2 panes @110, 1 sheet each) =", glass_c)
gross_c = D(1000)
net_c = r2(gross_c / D("1.05"))
basis_c = r2((110 + 110) / D(1) + lam_rate)
print("basis =", basis_c, "basisTotal =", r2(basis_c * cut_c))
profit_c = r2(net_c - glass_c - lam_amt)
print("profit =", profit_c)
margin_c = r2((gross_c - r2(basis_c * cut_c)) / r2(basis_c * cut_c) * 100)
cost_c = glass_c + lam_amt
ap_c = r2(net_c - cost_c)
print("margin% =", margin_c, "overall% =", r2(ap_c / cost_c * 100),
      "netMargin% =", r2(ap_c / net_c * 100))

print()
print("═══ D. U-insert price-based default (AED 10/m2) ═══")
# SGU spec names U-Insert, metric SQM1 = 1.0, no seed, no row -> default 10
print("uInsertCost =", r2(D(1) * 10))

print()
print("═══ Step 7 waterfall ═══")
# net 1200, 5% discount, VAT 5%
net7 = D(1000) + D(200)
d7 = mr(net7 * 5 / 100)
bv = net7 - d7
vat = mr(bv * 5 / 100)
print("net=1200 5%: discount", d7, "beforeVat", bv, "vat", vat, "grand", mr(bv + vat))
# fixed 1500 clamped to net
d7b = min(D(1500), net7)
print("fixed 1500: discount", d7b, "vat", mr(0), "grand", mr(net7 - d7b))
# no VAT
print("no-vat: grand", mr(net7 - d7))

print()
print("═══ Step 7 RoundTo ═══")
# net 1200, vat 5%, target 1000
f = D("1.05")
bv = mr(D(1000) / f)
d = mr(D(1200) - bv)
after = D(1200) - d
g = mr(after + mr(after * 5 / 100))
print("target 1000: beforeVat", bv, "discount", d, "grand", g)
# target >= max (1260) -> 0
print("target 1300 -> discount", 0)
# target 0 -> full discount
bv0 = mr(D(0) / f)
print("target 0: discount", mr(D(1200) - bv0))
