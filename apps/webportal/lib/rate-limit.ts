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
  // WEBPORTAL n'est pas expose directement : SRV-11 ecrase X-Real-IP avec
  // l'adresse issue de sa frontiere de confiance (PROXY v2 en production).
  // X-Forwarded-For reste descriptif, jamais une identite : sa premiere
  // valeur peut avoir ete fournie par le navigateur avant le proxy.
  return normalizeTrustedClientIp(request.headers.get("x-real-ip")) ?? "unknown";
}

export function normalizeTrustedClientIp(value: string | null): string | null {
  const candidate = value?.trim();
  if (!candidate || candidate.length > 45 || candidate.includes(",")) {
    return null;
  }

  // Le proxy transmet une seule IPv4 ou IPv6. Ne pas laisser une valeur
  // arbitraire creer un nombre illimite de compartiments de rate limiting.
  return /^[0-9a-f:.]+$/i.test(candidate) ? candidate.toLowerCase() : null;
}
