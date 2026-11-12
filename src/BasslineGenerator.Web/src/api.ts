import type { GenerateRequest, GenerationPackage, Progression } from "./types";

const base = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, "") ?? "";

async function request(path: string, body?: unknown, binary = false): Promise<Response> {
  let response: Response;
  try {
    response = await fetch(`${base}${path}`, body === undefined ? {} : {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: binary ? "audio/midi" : "application/json" },
      body: JSON.stringify(body),
    });
  } catch {
    throw new Error("Cannot reach the API. Check your connection and API configuration.");
  }
  if (!response.ok) {
    if (response.status === 429) throw new Error("Too many requests. Please wait and try again.");
    const data: unknown = await response.json().catch(() => null);
    if (typeof data === "object" && data !== null && "error" in data && typeof data.error === "string")
      throw new Error(data.error);
    throw new Error(`Request failed (HTTP ${response.status}).`);
  }
  return response;
}

async function json<T>(path: string, body?: unknown): Promise<T> {
  return (await request(path, body)).json() as Promise<T>;
}

async function midi(path: string, body: GenerationPackage): Promise<Blob> {
  const response = await request(path, body, true);
  if (response.headers.get("content-type")?.split(";")[0] !== "audio/midi")
    throw new Error("The server did not return a MIDI file.");
  return response.blob();
}

export const api = {
  presets: () => json<string[]>("/api/presets"),
  progressions: () => json<Progression[]>("/api/progressions"),
  generate: (body: GenerateRequest) => json<GenerationPackage>("/api/generate/package", body),
  replay: (body: GenerationPackage) => json<GenerationPackage>("/api/package/regenerate", body),
  share: (body: GenerationPackage) => json<{ shareCode: string }>("/api/share", body),
  decode: (shareCode: string) => json<GenerationPackage>("/api/share/decode", { shareCode }),
  midi: (body: GenerationPackage) => midi("/api/package/midi", body),
  combinedMidi: (body: GenerationPackage) => midi("/api/package/combined-midi", body),
};

export function download(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
