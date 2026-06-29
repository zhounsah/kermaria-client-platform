import "server-only";

import type { NextRequest } from "next/server";

type Entry = { times: number[] };

const store = new Map<string, Entry>();

export type RateLimitDecision = {
  limited: boolean;
  retryAfterSeconds: number;
};

export function checkRateLimit(
  key: string,
  maxRequests: number,
  windowMs: number,
): RateLimitDecision {
  const now = Date.now();
  const entry = store.get(key) ?? { times: [] };
  entry.times = entry.times.filter((t) => now - t < windowMs);

  if (entry.times.length >= maxRequests) {
    store.set(key, entry);
    const oldest = entry.times[0] ?? now;
    const retryAfterMs = Math.max(0, windowMs - (now - oldest));
    return {
      limited: true,
      retryAfterSeconds: Math.ceil(retryAfterMs / 1000),
    };
  }

  entry.times.push(now);
  store.set(key, entry);
  return { limited: false, retryAfterSeconds: 0 };
}

export function getRequestIdentifier(request: NextRequest): string {
  const forwarded = request.headers.get("x-forwarded-for")?.trim();
  if (forwarded) {
    return forwarded.split(",")[0]?.trim() || "unknown";
  }

  const realIp = request.headers.get("x-real-ip")?.trim();
  return realIp || "unknown";
}
