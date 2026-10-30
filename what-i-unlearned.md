# What I unlearned

> Reflection draft based on the implementation and the available work record. It describes choices visible in the code and validation, not a complete account of five separately observed workdays.

I came in with the familiar instinct to treat a tool request as a feature checklist: take the input, do the work, return an answer. The inbox helper made that framing feel too automatic. A reply is not just text. It can promise a refund, imply that a meeting has moved, or make a customer believe someone has taken action. So I narrowed the automation boundary: the tool may group and draft, but it does not send, and every row says `Needs review`. That is not a disclaimer added after the feature; it is part of the output contract and the UI.

I also had to unlearn the idea that an AI result is good because the request succeeded. The model can return malformed JSON, omit a message, duplicate an ID, or create a reply that is too long. The backend checks the returned shape and requires exactly one valid draft per input. The deterministic CSV helpers and tests are a reminder that AI can be only one step in a workflow; parsing, constraints, and review should remain ordinary code where they are predictable.

Another habit to question is starting by building the requested thing from scratch. When I examined the worktree, the three helper workflows and substantial uncommitted changes were already present. The useful move was to inspect and validate that work, not overwrite it or pretend it was created during this turn. That changed how I treated the README, tests, and source: as existing work to preserve and assess.

Finally, I unlearned the temptation to turn a green test summary into a polished success story. The Worker tests, type-check, desktop build, and .NET test passed, but the test runner also reported a shutdown-timeout warning. A root-level TypeScript command failed before the correct backend-local command passed. And the tests did not demonstrate a live inbox run against Workers AI. Those distinctions matter: a test suite is evidence for its tested behavior, not proof of every integrated path.

The next step is not to claim that the helper is production-ready. It is to record a real end-to-end demo, including the AI-backed inbox run, and ask a human reviewer whether the drafts are actually useful. AI-first work should change how quickly I can explore and implement—not lower the standard for checking what I ship or for saying what I have not checked.
