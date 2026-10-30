# Five-day work log

> **Retrospective reconstruction, not a contemporaneous diary.** This log was written after the implementation was already present in the worktree. The available evidence does not establish that work happened on five separate calendar days, so the entries below organize the work into five stages; they do not claim five days of elapsed work or invented day-by-day prompts.

## Day 1 — Frame the problem and choose the workflow

- **Focus:** Define the Inbox helper around a real human task: sort a pile of candidate messages and prepare reply drafts.
- **Decision:** Keep the person in the loop. Drafting is useful; sending is out of scope.
- **Evidence in the deliverable:** `intent.md`, the inbox sample, and the UI's review-only messaging.
- **Would verify next:** Ask a reviewer whether the intent labels and concise replies make the queue easier to process.

## Day 2 — Establish the CSV contract

- **Focus:** Make the input/output shape explicit before relying on model output.
- **Decision:** Support `message` or `body`, preserve IDs, allow optional sender and subject, and reject malformed rows.
- **Evidence in the deliverable:** CSV parsing and validation in `agent-backend/src/workflows.ts`, plus workflow tests.
- **What could break:** Quoting, inconsistent row widths, duplicate IDs, and missing message text; tests cover several of these cases.

## Day 3 — Add AI drafting with checks around it

- **Focus:** Use Workers AI for intent grouping and reply drafting, while treating message content as untrusted input.
- **Decision:** Validate the response shape and one-draft-per-input invariant; do not trust a successful model call as proof of a usable result.
- **Evidence in the deliverable:** The Inbox branch in `agent-backend/src/index.ts` and tests of draft validation/rendering.
- **Honest limitation:** The validation run recorded here tested the Worker suite and TypeScript check; it did not record a live end-to-end inbox run against the remote AI binding.

## Day 4 — Put review in the desktop workflow

- **Focus:** Make the output inspectable and editable, and document setup for the app/backend pair.
- **Decision:** Keep a CSV preview and save flow; do not add a send action.
- **Evidence in the deliverable:** WPF preview/save behavior in `src/AgentClient.App` and run instructions in `README.md`.
- **What could break:** Worker connectivity, AI availability, or a CSV the output grid cannot parse; the app reports run and preview errors.

## Day 5 — Raise the load and check what is actually verified

- **Focus:** Exercise the broader helper suite and check boundary cases rather than only showing a happy-path inbox sample.
- **Decision:** Include attendance and hiring-stall workflows with sample CSVs and tests; keep the inbox capped at 100 messages and uploads at 1 MB.
- **Evidence in the deliverable:** Worker tests, sample files, TypeScript type-check, WPF build, and .NET test results.
- **Observed result:** Worker tests passed (9 tests), TypeScript type-check passed, desktop build passed with no warnings, and the .NET test passed. Vitest also printed a shutdown-timeout warning after reporting successful tests. The recorded `npx tsc --noEmit` attempt from the repository root failed because it selected the wrong package context; running the backend's local TypeScript compiler succeeded.
- **Still to do:** Record the actual five-minute demo and verify the inbox with a live AI-backed run. Do not present this reconstructed sequence as proof those checks happened.
