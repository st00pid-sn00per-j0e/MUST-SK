# CSV People Ops Helpers

A small WPF desktop app backed by a Cloudflare Worker. Choose one helper, load a CSV (or its sample), inspect the result grid, edit cells if needed, and save the output.

## Project and submission notes

- [Intent](./intent.md) describes the inbox helper's user, scope, and human-review boundary.
- [Final directive](./Final%20directive.md) captures the implementation and acceptance requirements.
- [Five-day work log](./5-day-log.md) is explicitly a retrospective reconstruction, not a contemporaneous diary.
- [What I unlearned](./what-i-unlearned.md) is a reflection grounded in the implementation and recorded validation.
- [Demo video script](./demo-video-script.md) is a recording guide; it is not a recorded video.

## Helpers and CSV formats

- **Inbox helper** — uses Workers AI to group messages by intent and draft one concise reply per row. Required column: `message` or `body`. Optional columns: `id`, `sender`, `subject`. The preview marks every draft `Needs review`; review and edit each reply before using it. The app does not send messages.
- **Attendance checker** — flags a workday when worked hours are over 1 hour below scheduled hours after leave, when worked hours exceed 12, or when hours/leave are outside the valid range. Required columns: `employee`, `date` (`YYYY-MM-DD`), `scheduled_hours`, `worked_hours`, `leave_hours` (blank leave counts as zero). Explanations are one sentence per flag. The included `attendance-sample.csv` demonstrates normal, leave-adjusted, short, and unusually long days.
- **Stall alerter** — lists open candidates whose `last_moved_date` is at least the selected number of days old, prioritizing the longest-stalled first. Required columns: `candidate`, `stage`, `last_moved_date` (`YYYY-MM-DD`). Closed, hired, rejected, and withdrawn stages are excluded.

CSV uploads are limited to 1 MB and 5,000 rows; the inbox helper supports up to 100 messages per run. Output is a CSV that can be edited in the preview grid before saving.

## Run locally

1. Start the Worker in one terminal:

   ```powershell
   cd agent-backend
   npm run dev
   ```

   The desktop app expects the Worker at `http://127.0.0.1:8787`. Inbox runs use the configured Cloudflare Workers AI binding and may incur AI usage charges; attendance and stall checks run in the Worker without an AI call.

2. In another terminal, launch the desktop app:

   ```powershell
   dotnet run --project src\AgentClient.App\AgentClient.App.csproj
   ```

3. Select a helper, choose a CSV or sample data, run the analysis, inspect the preview, and save the result.

## Validate

```powershell
cd agent-backend
npm test -- --run
npx tsc --noEmit
cd ..
dotnet test tests\AgentClient.Tests\AgentClient.Tests.csproj
dotnet build src\AgentClient.App\AgentClient.App.csproj
```

The Worker suite includes CSV workflow tests and local end-to-end attendance and stall runs. The .NET tests cover API requests, error handling, output download, and CSV upload progress. A live Inbox run calls the configured Workers AI binding and may incur usage charges; automated tests validate the inbox input/output contract without making that paid AI call.
