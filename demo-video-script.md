# Five-minute demo video: script and shot list

> **Recording guide only; no video has been recorded.** Capture the actual desktop app and actual run results. Do not substitute screenshots or a mocked success state for a live result.

## Before recording

- Start the Worker using the README and confirm it is reachable.
- Open the desktop app and use the included `inbox-sample.csv`.
- Confirm the Workers AI binding is available; inbox use may incur AI charges.
- Clear unrelated windows and notifications. Keep the error/status area visible.
- If live AI fails, show the error honestly and do not edit the screen or imply success.

## Timed script

### 0:00–0:35 — Set the context

**Show:** App title, helper selector, and result preview.

**Say:** “This is a small inbox helper. It groups messages by what the sender wants and drafts a short reply. It does not send messages. Every reply needs a human review.”

### 0:35–1:10 — Load the sample

**Show:** Select **Inbox helper**, then **Use sample data**. Briefly show the selected CSV path and the sample's four messages if available.

**Say:** “The input is a CSV with a message column and optional sender, subject, and ID. This sample includes a refund request, meeting change, product question, and order-status question.”

### 1:10–2:20 — Run the inbox workflow

**Show:** Click **Analyze CSV**. Show progress and run activity while it runs, then the result grid after completion. Let the actual result remain visible long enough to read.

**Say:** “The backend validates the CSV, asks Workers AI for one intent group and draft per message, then checks the response before presenting it. Generated text can be wrong, so the output is a suggestion, not an approved response.”

### 2:20–3:10 — Human review and edit

**Show:** Point out IDs, intent groups, draft replies, and `Needs review`. Edit one draft in the grid, then leave the edited value visible.

**Say:** “The reviewer can change the wording here. The app has no send button; saving the CSV is the handoff.”

### 3:10–4:05 — Raise the load with another helper

**Show:** Select **Attendance checker**, load its sample, run it, and show flagged rows with explanations. If time permits, point out that ordinary attendance analysis does not require the AI call.

**Say:** “I also exercised a more structured case: attendance rows are checked against scheduled time and leave, and a flag gets a one-sentence explanation. The included sample has both normal and unusual patterns.”

### 4:05–4:40 — Show the boundary of the stall helper

**Show:** Select **Stall alerter**, set the stale threshold, load its sample, and run it. Show sorted candidates and follow-up actions.

**Say:** “The hiring-board check prioritizes open candidates by time stalled and suggests a next follow-up. Closed stages are excluded.”

### 4:40–5:00 — Close with evidence and limitations

**Show:** README validation commands or a brief terminal view of test/build results.

**Say:** “The Worker tests, TypeScript check, desktop build, and .NET test passed. The test runner printed a shutdown warning, and this demo is the separate live check for the AI-backed inbox. No messages are sent by this tool.”
