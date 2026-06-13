import type { CorrelationId } from "@kermaria/shared";

export const CORRELATION_HEADER = "X-Correlation-Id";

const VALID_CORRELATION_ID = /^[A-Za-z0-9._-]{1,128}$/;

export function resolveCorrelationId(value: string | null): CorrelationId {
  const candidate = value?.trim();

  if (candidate && VALID_CORRELATION_ID.test(candidate)) {
    return candidate as CorrelationId;
  }

  return crypto.randomUUID() as CorrelationId;
}
