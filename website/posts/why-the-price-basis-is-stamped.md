---
title: Why every quoted figure carries its price basis
summary: An ERP that recalculates yesterday's quotation at today's glass prices is not an ERP. What ProGlass stores instead, and why it matters more than any feature.
date: 2026-09-20
category: Engineering
readingMinutes: 5
---

There is a failure mode in quotation software that is worse than a crash, because it does not look like a failure. The software is asked for last month's job. It reopens the quotation, re-reads the current sheet prices, and recomputes. The margin on screen is now a number that nobody ever quoted.

Nothing warns you. The screen looks normal. The report is internally consistent. And the number is wrong in the only way that matters: it is not the number the business acted on.

## Material costs move

Glass is bought on sheets and priced by area, and those buy prices change. When they do, every stored quotation that recalculates itself silently becomes a different quotation. If your profitability reporting reads those figures, your historical margin data is rewritten every time a supplier changes a rate.

## What ProGlass does instead

The profit and loss snapshot is written once, at Step 9 of the proforma, and it records the cost basis it was computed with. Job profitability, statements and reports read that stored snapshot rather than recomputing it. There is exactly one pricing engine in the application, and it runs at the moment of quoting.

When the sheet-store prices move, the affected rows are flagged rather than rescored. You can see that the basis has moved, decide what to do about it, and the historical figure stays what it was.

## Why this is the whole product

Feature lists are easy to write and hard to compare. This property is not a feature — it is what separates a system that holds business records from a calculator with a database attached. A price you cannot trust after the fact is not a price; it is a current estimate.
