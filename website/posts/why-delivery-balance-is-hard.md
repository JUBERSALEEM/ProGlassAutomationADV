---
title: Why delivery balance is harder than it looks
summary: A job goes out over three movements and the fourth is a replacement for a broken pane. The arithmetic that answers 'what is still owed' is easy to get wrong.
date: 2026-09-05
category: Industry
readingMinutes: 5
---

A glass order is rarely delivered once. It goes out in whatever the vehicle and the site can take, and the balance follows. The question 'how much of this job has actually gone out' sounds trivial and is not.

## Three ways it goes wrong

- Double counting: the balance is recomputed from the original order each time, and a piece that was dispatched in movement one gets counted again in movement two.
- Over-delivery: nothing prevents the second delivery note from listing more pieces than remain, so the job ships 12 of 10 pieces and the invoice has to be unravelled.
- The replacement problem: a broken pane is re-cut and re-delivered. Is that a movement against the original balance, or a new line? If it is answered differently by different people, the balance drifts.

## One place owns the maths

The fix is not a better screen; it is deciding that exactly one piece of code owns the pending-piece calculation, and that nothing else is allowed to work it out independently. In ProGlass that lives in the delivery-note validation service, which refuses a movement that would exceed what was ordered.

## Why it is worth the trouble

The balance is what the customer will be told on the phone, what the driver will load against, and what the invoice is eventually raised for. When three people have three answers, the job costs more than the glass did.
