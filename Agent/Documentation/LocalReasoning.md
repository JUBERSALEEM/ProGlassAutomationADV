# ProGlass Agent — Local reasoning

## What "local" means here

`LocalReasoningProvider` performs **no inference**. There is no model file
and no network call. It retrieves from the curated knowledge base and
presents what it found.

## Why it refuses to guess

For a system that quotes glass prices and VAT, a fluent wrong answer is
worse than "I don't know". When the knowledge base has nothing relevant,
the provider says so and points at what would help:

> I do not have anything about that in my local knowledge base, and I will
> not guess. You can add a note in Settings → AI & Agent, or ask me about
> stock, a document number, a customer, or recent activity — those I read
> from the live data.

## Live data vs knowledge

| Question type | Answered by |
|---|---|
| "how much 6mm clear do we have" | `get_inventory` tool — **live** Sheet Store |
| "find PI-2026-014" | `search_proforma` tool — **live** database |
| "what is the VAT rate" | Knowledge base |
| "what is a DGU" | Knowledge base |

Tool intents are detected **before** navigation intents precisely so that a
data question returns data.

## Local model advice

`Rules/LocalModelAdvisor` inspects `Environment.ProcessorCount` and
`GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` and **recommends**:

| RAM | Tier | Candidates |
|---|---|---|
| ≥ 16 GB | 7B Q4 | Phi-3.5-mini (MIT), Qwen2.5-7B-Instruct (Apache-2.0) |
| ≥ 8 GB | 3B Q4 | Qwen2.5-3B (Apache-2.0) |
| < 8 GB | none | Keep using the knowledge base |

**It never downloads anything.** No URL is opened and no background job is
scheduled — pulling multi-gigabyte weights is a deliberate user decision,
not something a settings page starts on its own. Only permissively
licensed models are named.
