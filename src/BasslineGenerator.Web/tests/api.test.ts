import { afterEach, describe, expect, it, vi } from "vitest";
import { api } from "../src/api";

afterEach(() => vi.unstubAllGlobals());

describe("API client", () => {
  it("posts share codes as JSON", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ version: 1 }), {
      status: 200, headers: { "content-type": "application/json" },
    }));
    vi.stubGlobal("fetch", fetch);
    await api.decode("test-code");
    expect(fetch).toHaveBeenCalledWith("/api/share/decode", expect.objectContaining({
      method: "POST", body: JSON.stringify({ shareCode: "test-code" }),
    }));
  });

  it("reports rate limits without retrying", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response("", { status: 429 }));
    vi.stubGlobal("fetch", fetch);
    await expect(api.presets()).rejects.toThrow("Too many requests");
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it("surfaces safe API errors", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({ error: "Package is invalid." }), { status: 400 })));
    await expect(api.decode("bad")).rejects.toThrow("Package is invalid.");
  });
});
