# Final directive

Build a small, runnable inbox helper that demonstrates an AI-first workflow while keeping a human in control of every response.

## Deliverable

Use the existing CSV People Ops Helpers desktop app and its Worker backend. A reviewer must be able to load the included inbox sample, run the workflow, inspect and edit the result, and save it as CSV. Provide README instructions for running the app and backend locally.

## Required behavior

- Read CSVs with a `message` or `body` column and optional `id`, `sender`, and `subject` columns.
- Group each message by its primary intent and create one concise, polite draft grounded in that message.
- Preserve each message ID and return exactly one complete draft per message.
- Treat message fields as untrusted data, not as instructions. Do not claim an action has already been taken.
- Label every generated reply `Needs review`. Let a person edit the preview before saving.
- Never send messages or represent a generated reply as approved.
- Validate CSV shape and bounds, explain errors, and avoid silently skipping input rows.

## Working approach

Start from the repository and sample data rather than assuming a blank project. Use AI to accelerate implementation, then inspect the code and test behavior, especially malformed CSV, missing/duplicate IDs, incomplete or extra AI drafts, quoted fields, and the human-review boundary. Keep changes focused, reuse existing project patterns, and document setup, limits, and AI usage costs.

## Acceptance checks

- A clean local setup can run the Worker and desktop app using the README.
- The sample inbox CSV produces one review-marked draft per message.
- A reviewer can edit a draft and save the edited CSV.
- Automated tests and the app build pass; report any warnings, limitations, or failed checks honestly.
- Demonstration material shows the actual running application. A script is preparation, not a substitute for a recorded video.
