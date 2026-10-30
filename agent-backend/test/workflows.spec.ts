import { describe, expect, it } from "vitest";
import {
	analyzeAttendance,
	analyzeStalls,
	prepareInboxRows,
	renderInboxDrafts,
} from "../src/workflows";

describe("CSV workflows", () => {
	it("flags attendance exceptions with one explanation per flag", () => {
		const result = analyzeAttendance(
			"employee,date,scheduled_hours,worked_hours,leave_hours\r\n" +
				"Ada,2026-10-25,8,5,0\r\n" +
				"Lin,2026-10-25,8,8,0\r\n" +
				"Mo,2026-10-25,8,13,0\r\n",
		);
		expect(result).toContain('"Worked 5 hours, more than 1 hour below');
		expect(result).toContain('"Worked 13 hours, exceeding');
		expect(result).not.toContain("Lin");
	});

	it("accounts for leave and rejects malformed attendance values", () => {
		const result = analyzeAttendance("employee,date,scheduled_hours,worked_hours,leave_hours\nAda,2026-10-25,8,4,4\n");
		expect(result.split("\r\n")).toHaveLength(2);
		expect(() => analyzeAttendance("employee,date,scheduled_hours,worked_hours,leave_hours\nAda,2026-10-25,eight,4,0")).toThrow(
			'invalid "scheduled_hours"',
		);
	});

	it("sorts stalled candidates by days and excludes closed stages", () => {
		const csv =
			"candidate,stage,last_moved_date\n" +
			"Ada,Interview,2026-10-10\n" +
			"Lin,Screen,2026-10-17\n" +
			"Mo,Hired,2026-09-01\n";
		const result = analyzeStalls(csv, 7, new Date("2026-10-27T12:00:00Z"));
		const lines = result.trim().split("\r\n");
		expect(lines[1]).toContain("Ada,Interview,2026-10-10,17,High");
		expect(lines[2]).toContain("Lin,Screen,2026-10-17,10,Normal");
		expect(result).not.toContain("Mo");
	});

	it("parses quoted inbox messages and requires one valid draft per message", () => {
		const rows = prepareInboxRows('id,sender,subject,message\n1,Alex,Help,"Need a refund, please."\n');
		expect(rows[0].message).toBe("Need a refund, please.");
		const result = renderInboxDrafts(rows, [{ id: "1", group: "Refund", reply: "I can help with that." }]);
		expect(result).toContain("Refund");
		expect(result).toContain("Needs review");
		expect(() => renderInboxDrafts(rows, [])).toThrow("exactly one draft");
	});

	it("rejects malformed CSV quoting and duplicate headers", () => {
		expect(() => prepareInboxRows('message\n"unfinished')).toThrow("unclosed quoted field");
		expect(() => prepareInboxRows('message\n"closed"extra')).toThrow("characters after a closing quote");
		expect(() => prepareInboxRows('message\nun"expected')).toThrow("unexpected quote");
		expect(() => prepareInboxRows("message,message\none,two")).toThrow("duplicate column headers");
	});

	it("accepts the body alias, assigns IDs, and handles quoted newlines", () => {
		const rows = prepareInboxRows('body\n"Could you help,\nplease?"\n');

		expect(rows).toEqual([
			{ id: "message-1", sender: "", subject: "", message: "Could you help,\nplease?" },
		]);
	});

	it("rejects duplicate inbox IDs and mismatched row widths", () => {
		expect(() => prepareInboxRows("id,message\nsame,first\nsame,second")).toThrow("unique values");
		expect(() => prepareInboxRows("message,sender\nonly-one-cell")).toThrow("expected 2");
	});

	it("rejects invalid attendance dates and invalid stall thresholds", () => {
		expect(() =>
			analyzeAttendance("employee,date,scheduled_hours,worked_hours,leave_hours\nAda,2026-02-30,8,8,0"),
		).toThrow('invalid "date"');
		expect(() => analyzeStalls("candidate,stage,last_moved_date\nAda,Screen,2026-10-10", 0)).toThrow(
			"between 1 and 365",
		);
	});
});
