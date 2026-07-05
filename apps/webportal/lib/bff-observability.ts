import "server-only";

type BffFailureEvent = {
  category: string;
  code: string;
  correlation_id: string;
  operation: string;
  status: number;
  surface: string;
  // Contexte de diagnostic optionnel (ex. error-codes hCaptcha). Ne doit jamais
  // contenir de secret ni de donnée personnelle : ces logs partent en clair.
  detail?: string;
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
