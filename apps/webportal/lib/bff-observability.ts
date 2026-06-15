import "server-only";

type BffFailureEvent = {
  category: string;
  code: string;
  correlation_id: string;
  operation: string;
  status: number;
  surface: string;
};

export function logBffFailure(event: BffFailureEvent) {
  process.stderr.write(
    `${JSON.stringify({
      ...event,
      level: "error",
      timestamp_utc: new Date().toISOString(),
    })}\n`,
  );
}
