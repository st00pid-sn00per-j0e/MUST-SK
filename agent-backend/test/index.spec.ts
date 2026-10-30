import { env, createExecutionContext, SELF } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import worker from "../src/index";

const IncomingRequest = Request<unknown, IncomingRequestCfProperties>;

describe("Worker routes", () => {
	it("runs an uploaded attendance CSV and serves its result", async () => {
		const create = await SELF.fetch("https://example.com/runs", {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ mode: "attendance" }),
		});
		expect(create.status).toBe(200);
		const { runId } = (await create.json()) as { runId: string };

		const upload = await SELF.fetch(`https://example.com/upload/${runId}`, {
			method: "PUT",
			body: "employee,date,scheduled_hours,worked_hours,leave_hours\r\nAda,2026-10-25,8,5,0\r\n",
		});
		expect(upload.status).toBe(200);

		const start = await SELF.fetch(`https://example.com/runs/${runId}/start`, { method: "POST" });
		expect(start.status).toBe(200);

		let snapshot: { status: string; errorMessage: string | null } | undefined;
		for (let attempt = 0; attempt < 50; attempt++) {
			const response = await SELF.fetch(`https://example.com/runs/${runId}`);
			snapshot = (await response.json()) as { status: string; errorMessage: string | null };
			if (snapshot.status === "completed" || snapshot.status === "failed") break;
			await new Promise((resolve) => setTimeout(resolve, 20));
		}
		expect(snapshot?.status, snapshot?.errorMessage ?? undefined).toBe("completed");

		const output = await SELF.fetch(`https://example.com/runs/${runId}/outputs/cleaned.csv`);
		expect(output.status).toBe(200);
		expect(await output.text()).toContain(
			'"Worked 5 hours, more than 1 hour below the 8 hours expected after leave.',
		);
	});

	it("runs a stall check using its configured threshold", async () => {
		const create = await SELF.fetch("https://example.com/runs", {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ mode: "stalls", thresholdDays: 7 }),
		});
		expect(create.status).toBe(200);
		const { runId } = (await create.json()) as { runId: string };

		const upload = await SELF.fetch(`https://example.com/upload/${runId}`, {
			method: "PUT",
			body: "candidate,stage,last_moved_date\r\nAda,Interview,2020-01-01\r\nLin,Screen,2020-01-01\r\nMo,Hired,2020-01-01\r\n",
		});
		expect(upload.status).toBe(200);
		const start = await SELF.fetch(`https://example.com/runs/${runId}/start`, { method: "POST" });
		expect(start.status).toBe(200);

		let status = "queued";
		for (let attempt = 0; attempt < 50; attempt++) {
			const response = await SELF.fetch(`https://example.com/runs/${runId}`);
			status = ((await response.json()) as { status: string }).status;
			if (status === "completed" || status === "failed") break;
			await new Promise((resolve) => setTimeout(resolve, 20));
		}
		expect(status).toBe("completed");

		const output = await SELF.fetch(`https://example.com/runs/${runId}/outputs/cleaned.csv`);
		expect(output.status).toBe(200);
		const csv = await output.text();
		expect(csv.indexOf("Ada,Interview")).toBeLessThan(csv.indexOf("Lin,Screen"));
		expect(csv).not.toContain("Mo,Hired");
		expect(csv).toContain("follow_up_action");
	});

	it("rejects unknown workflows before creating a run", async () => {
		const request = new IncomingRequest("http://example.com/runs", {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ mode: "unknown" }),
		});
		const response = await worker.fetch(request, env, createExecutionContext());
		expect(response.status).toBe(400);
		expect(await response.json()).toEqual({ error: "Choose inbox, attendance, or stalls." });
	});

	it("rejects oversized CSV uploads", async () => {
		const request = new IncomingRequest("http://example.com/upload/test-run", {
			method: "PUT",
			headers: { "Content-Length": "1000001" },
			body: "too large",
		});
		const response = await worker.fetch(request, env, createExecutionContext());
		expect(response.status).toBe(413);
	});

	it("returns a JSON 404 for an unknown route", async () => {
		const request = new IncomingRequest("http://example.com/unknown");
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		expect(response.status).toBe(404);
		expect(await response.json()).toEqual({ error: "Not Found" });
	});

	it("serves unknown integration routes as JSON 404s", async () => {
		const response = await SELF.fetch("https://example.com/unknown");
		expect(response.status).toBe(404);
		expect(await response.json()).toEqual({ error: "Not Found" });
	});
});
