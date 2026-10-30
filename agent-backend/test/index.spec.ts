import { env, createExecutionContext, SELF } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import worker from "../src/index";

const IncomingRequest = Request<unknown, IncomingRequestCfProperties>;

describe("Worker routes", () => {
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
