# Intent: Inbox helper

## The problem

When a person is faced with a pile of messages, they repeatedly do two small but attention-heavy tasks: work out what each sender wants and write a suitable reply. A useful first tool should reduce that sorting and drafting effort without taking control away from the person replying.

## The user and outcome

The user is a person reviewing a CSV of candidate messages. They want messages grouped by primary intent and a concise, relevant reply draft for each message. They remain responsible for checking the message, editing the draft, and deciding whether to use it.

## Intended workflow

1. Select the Inbox helper and load a CSV or the included sample.
2. Analyze the messages and inspect the intent group and reply draft beside each message.
3. Review and edit every draft in the result preview.
4. Save the reviewed result as a CSV for the user's own follow-up.

The helper does not send messages.

## Boundaries and success criteria

- Accept `message` or `body`, with optional `id`, `sender`, and `subject` columns.
- Produce exactly one short draft per input message and preserve its ID.
- Treat message content as untrusted input, not as instructions to the AI.
- Mark drafts as `Needs review` so generated text is not mistaken for approved text.
- Reject malformed or ambiguous input and surface a useful error rather than silently dropping data.
- Keep review in the human's hands: no send action, automation, or claim that a refund, booking, or other action has already happened.
- Make the workflow runnable with documented setup, sample data, and tests.

## Why this scope

The inbox helper is one of the proposed small tools and exercises AI for grouping and drafting while keeping the decision to respond with a human. The existing app also includes attendance and hiring-stall CSV helpers; this brief defines the inbox workflow as the primary intent, not as a claim that the other workflows are absent.
