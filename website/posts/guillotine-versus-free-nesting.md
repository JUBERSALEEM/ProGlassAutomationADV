---
title: Guillotine or free nesting: what decides the layout
summary: Two optimizers can produce very different-looking plans for the same cutting list, and both can be correct. The difference is what the table can physically do.
date: 2026-09-12
category: Industry
readingMinutes: 6
---

A nesting problem in the abstract is: fit these rectangles into that rectangle with as little waste as possible. On a glass table it is a narrower problem: fit these rectangles in such a way that a sequence of straight, edge-to-edge cuts separates them, in an order an operator can actually perform.

## The pinwheel problem

Free nesting algorithms like to produce pinwheel arrangements — rectangles that spiral around each other. They are efficient on paper and impossible on a guillotine. You cannot score a line from one edge of the sheet to the other without also cutting through a piece that is already in place.

## What guillotine costs you

Constraining to guillotine cuts loses some yield. That is the honest trade: a packed-pinwheel layout may report a higher utilization than a guillotine layout, while being uncuttable. A number you cannot execute is worse than no number at all.

## Two stages, three stages, or free

The trade continues inside the guillotine family. Two-stage patterns restrict the cut direction: first all the cuts one way, then all the cuts the other. They are fast, easy to set up and easy to jig, and they leave a bit more waste. Free guillotine allows unlimited depth and packs tighter, at the cost of more table time. Three-stage sits in between. Which one is right depends on your table, your volume and the job.

## The thing to ask any optimizer for

Ask it to show the cut sequence. A layout drawing shows where the pieces are; a cut sequence shows how they come free. If a plan can report its pieces but not its cuts, it has modelled the arrangement and not the operation.
