export type WorkflowMode = "inbox" | "attendance" | "stalls";

type CsvRow = Record<string, string>;
const MAX_CSV_ROWS = 5_000;

export interface InboxDraft {
	id: string;
	group: string;
	reply: string;
}

function parseCsv(input: string): CsvRow[] {
	const text = input.replace(/^\uFEFF/, "");
	const matrix: string[][] = [];
	let row: string[] = [];
	let field = "";
	let quoted = false;

	for (let i = 0; i < text.length; i++) {
		const char = text[i];
		if (quoted) {
			if (char === '"' && text[i + 1] === '"') {
				field += '"';
				i++;
			} else if (char === '"') {
				quoted = false;
			} else {
				field += char;
			}
		} else if (char === '"' && field.length === 0) {
			quoted = true;
		} else if (char === ",") {
			row.push(field);
			field = "";
		} else if (char === "\n" || char === "\r") {
			if (char === "\r" && text[i + 1] === "\n") i++;
			row.push(field);
			field = "";
			if (row.some((cell) => cell.trim() !== "")) matrix.push(row);
			row = [];
		} else {
			field += char;
		}
	}

	if (quoted) throw new Error("CSV contains an unclosed quoted field.");
	row.push(field);
	if (row.some((cell) => cell.trim() !== "")) matrix.push(row);
	if (matrix.length < 2) throw new Error("CSV must include a header and at least one data row.");
	if (matrix.length - 1 > MAX_CSV_ROWS) throw new Error(`CSV supports at most ${MAX_CSV_ROWS} data rows.`);

	const headers = matrix[0].map((header) => normalize(header));
	if (headers.some((header) => !header)) throw new Error("CSV headers cannot be blank.");
	if (new Set(headers).size !== headers.length) throw new Error("CSV contains duplicate column headers.");

	return matrix.slice(1).map((cells, index) => {
		if (cells.length !== headers.length) {
			throw new Error(`CSV row ${index + 2} has ${cells.length} values; expected ${headers.length}.`);
		}
		return Object.fromEntries(headers.map((header, i) => [header, cells[i].trim()]));
	});
}

function normalize(value: string): string {
	return value.trim().toLowerCase().replace(/[\s-]+/g, "_");
}

function required(row: CsvRow, key: string, rowNumber: number): string {
	const value = row[key]?.trim();
	if (!value) throw new Error(`CSV row ${rowNumber} is missing "${key}".`);
	return value;
}

function numberValue(value: string, column: string, rowNumber: number): number {
	const number = Number(value);
	if (!Number.isFinite(number) || value.trim() === "") {
		throw new Error(`CSV row ${rowNumber} has an invalid "${column}" value.`);
	}
	return number;
}

function csvCell(value: unknown): string {
	const text = String(value ?? "");
	return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

function toCsv(headers: string[], rows: unknown[][]): string {
	return [headers, ...rows].map((row) => row.map(csvCell).join(",")).join("\r\n") + "\r\n";
}

export function analyzeAttendance(input: string): string {
	const rows = parseCsv(input);
	const output: unknown[][] = [];

	for (const [index, row] of rows.entries()) {
		const rowNumber = index + 2;
		const employee = required(row, "employee", rowNumber);
		const date = required(row, "date", rowNumber);
		parseDate(date, rowNumber, "date");
		const scheduled = numberValue(required(row, "scheduled_hours", rowNumber), "scheduled_hours", rowNumber);
		const worked = numberValue(required(row, "worked_hours", rowNumber), "worked_hours", rowNumber);
		const leave = row.leave_hours ? numberValue(row.leave_hours, "leave_hours", rowNumber) : 0;
		const flags: string[] = [];

		if (scheduled < 0 || worked < 0 || leave < 0 || leave > scheduled) {
			flags.push("Hours or leave are outside the valid scheduled range.");
		} else {
			const expected = scheduled - leave;
			if (expected - worked > 1) {
				flags.push(`Worked ${worked} hours, more than 1 hour below the ${expected} hours expected after leave.`);
			}
			if (worked > 12) flags.push(`Worked ${worked} hours, exceeding the 12-hour daily review threshold.`);
		}

		for (const explanation of flags) {
			output.push([employee, date, scheduled, worked, leave, explanation]);
		}
	}

	return toCsv(["employee", "date", "scheduled_hours", "worked_hours", "leave_hours", "explanation"], output);
}

function parseDate(value: string, rowNumber: number, column = "last_moved_date"): Date {
	if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
		throw new Error(`CSV row ${rowNumber} has an invalid "${column}"; use YYYY-MM-DD.`);
	}
	const [year, month, day] = value.split("-").map(Number);
	const date = new Date(Date.UTC(year, month - 1, day));
	if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) {
		throw new Error(`CSV row ${rowNumber} has an invalid "${column}"; use YYYY-MM-DD.`);
	}
	return date;
}

export function analyzeStalls(input: string, thresholdDays: number, today = new Date()): string {
	if (!Number.isInteger(thresholdDays) || thresholdDays < 1 || thresholdDays > 365) {
		throw new Error("Stall threshold must be a whole number between 1 and 365 days.");
	}

	const todayUtc = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate());
	const terminalStages = new Set(["hired", "rejected", "withdrawn", "closed", "offer accepted"]);
	const rows = parseCsv(input);
	const stalled = rows.flatMap((row, index) => {
		const rowNumber = index + 2;
		const candidate = required(row, "candidate", rowNumber);
		const stage = required(row, "stage", rowNumber);
		const movedDate = required(row, "last_moved_date", rowNumber);
		if (terminalStages.has(stage.toLowerCase())) return [];

		const moved = parseDate(movedDate, rowNumber);
		const days = Math.floor((todayUtc - moved.getTime()) / 86_400_000);
		if (days < 0) throw new Error(`CSV row ${rowNumber} has a future "last_moved_date".`);
		if (days < thresholdDays) return [];

		const normalizedStage = stage.toLowerCase();
		const action = normalizedStage.includes("interview")
			? "Ask the interviewer for feedback and confirm the next step."
			: normalizedStage.includes("screen")
				? "Contact the candidate to schedule or confirm the screening."
				: normalizedStage.includes("offer")
					? "Check the offer status with the candidate and hiring manager."
					: "Ask the hiring manager for a status update and agree on the next step.";
		return [{ candidate, stage, movedDate, days, priority: days >= thresholdDays * 2 ? "High" : "Normal", action, index }];
	});

	stalled.sort((a, b) => b.days - a.days || a.index - b.index);
	return toCsv(
		["candidate", "stage", "last_moved_date", "days_stalled", "priority", "follow_up_action"],
		stalled.map(({ candidate, stage, movedDate, days, priority, action }) => [
			candidate,
			stage,
			movedDate,
			days,
			priority,
			action,
		]),
	);
}

export function prepareInboxRows(input: string): Array<{ id: string; sender: string; subject: string; message: string }> {
	const rows = parseCsv(input);
	if (rows.length > 100) throw new Error("Inbox helper supports at most 100 messages at a time.");
	const prepared = rows.map((row, index) => {
		const rowNumber = index + 2;
		const message = row.message || row.body;
		if (!message?.trim()) throw new Error(`CSV row ${rowNumber} is missing "message" or "body".`);
		return {
			id: row.id || `message-${index + 1}`,
			sender: row.sender || "",
			subject: row.subject || "",
			message: message.trim(),
		};
	});
	if (new Set(prepared.map((row) => row.id)).size !== prepared.length) {
		throw new Error('Inbox CSV must contain unique values in the "id" column.');
	}
	return prepared;
}

export function renderInboxDrafts(
	rows: Array<{ id: string; sender: string; subject: string }>,
	drafts: InboxDraft[],
): string {
	if (drafts.length !== rows.length) throw new Error("AI did not return exactly one draft for each message.");
	const byId = new Map<string, InboxDraft>();
	for (const draft of drafts) {
		if (!draft.id || !draft.group?.trim() || !draft.reply?.trim()) {
			throw new Error("AI returned an incomplete inbox draft.");
		}
		if (byId.has(draft.id) || !rows.some((row) => row.id === draft.id)) {
			throw new Error("AI returned a duplicate or unknown message ID.");
		}
		if (draft.reply.length > 500) throw new Error(`AI draft for ${draft.id} is longer than 500 characters.`);
		byId.set(draft.id, draft);
	}
	if (rows.some((row) => !byId.has(row.id))) throw new Error("AI did not return a draft for every message.");

	return toCsv(
		["id", "sender", "subject", "intent_group", "draft_reply", "review_status"],
		rows.map((row) => [
			row.id,
			row.sender,
			row.subject,
			byId.get(row.id)!.group.trim(),
			byId.get(row.id)!.reply.trim(),
			"Needs review",
		]),
	);
}
