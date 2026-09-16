import type { PreDiagnosticAnswers } from "@/lib/pre-diagnostic";

export type DiagnosticCallbackPayload = {
  answers: PreDiagnosticAnswers;
  /** Version publiée rendue au visiteur ; 0 = fallback code historique. */
  configurationVersion: number;
  name: string;
  phone: string;
  email: string | null;
  organisation: string | null;
  preferredTime: string | null;
  comment: string | null;
  consent: true;
  website: string;
};

export type DiagnosticCallbackFieldErrors = Partial<Record<"name" | "phone" | "email" | "organisation" | "preferredTime" | "comment" | "consent" | "answers", string>>;

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE_PATTERN = /^[0-9+().\s-]{6,40}$/;
const MAX_ANSWERS = 20;
const MAX_ANSWER_KEY = 40;
const MAX_ANSWER_VALUE = 80;

export function normalizeSingleLine(value: unknown, max: number) {
  if (typeof value !== "string") return "";
  const normalized = value.replace(/\s+/g, " ").trim();
  return normalized.length <= max ? normalized : "";
}

export function normalizeMultiline(value: unknown, max: number) {
  if (typeof value !== "string") return "";
  const normalized = value.replace(/\r\n?/g, "\n").trim();
  return normalized.length <= max ? normalized : "";
}

function tooLong(value: unknown, max: number, multiline = false) {
  if (typeof value !== "string") return false;
  const normalized = multiline
    ? value.replace(/\r\n?/g, "\n").trim()
    : value.replace(/\s+/g, " ").trim();
  return normalized.length > max;
}

export function validateDiagnosticCallbackPayload(value: unknown): { payload: DiagnosticCallbackPayload | null; errors: DiagnosticCallbackFieldErrors } {
  const errors: DiagnosticCallbackFieldErrors = {};
  if (!value || typeof value !== "object" || Array.isArray(value)) return { payload: null, errors: { answers: "La demande est invalide." } };
  const body = value as Record<string, unknown>;
  const name = normalizeSingleLine(body.name, 120);
  const phone = normalizeSingleLine(body.phone, 40);
  const emailValue = normalizeSingleLine(body.email, 254);
  const organisationValue = normalizeSingleLine(body.organisation, 160);
  const preferredTimeValue = normalizeSingleLine(body.preferredTime, 160);
  const commentValue = normalizeMultiline(body.comment, 1200);
  const website = normalizeSingleLine(body.website, 200);

  if (!name) errors.name = "Le nom est requis.";
  else if (tooLong(body.name, 120)) errors.name = "Le nom est trop long.";
  if (!phone) errors.phone = "Le téléphone est requis.";
  else if (!PHONE_PATTERN.test(phone)) errors.phone = "Le téléphone est invalide.";
  if (tooLong(body.phone, 40)) errors.phone = "Le téléphone est trop long.";
  if (emailValue && !EMAIL_PATTERN.test(emailValue)) errors.email = "L'adresse e-mail est invalide.";
  else if (tooLong(body.email, 254)) errors.email = "L'adresse e-mail est trop longue.";
  if (tooLong(body.organisation, 160)) errors.organisation = "L'organisation est trop longue.";
  if (tooLong(body.preferredTime, 160)) errors.preferredTime = "Cette indication est trop longue.";
  if (tooLong(body.comment, 1200, true)) errors.comment = "Le commentaire est trop long.";
  if (body.consent !== true) errors.consent = "Votre accord est nécessaire pour être recontacté.";

  const rawAnswers = body.answers;
  const configurationVersion = typeof body.configurationVersion === "number" && Number.isInteger(body.configurationVersion) && body.configurationVersion >= 0 && body.configurationVersion <= 1_000_000 ? body.configurationVersion : -1;
  if (configurationVersion < 0) errors.answers = "La version du diagnostic est invalide.";
  if (!rawAnswers || typeof rawAnswers !== "object" || Array.isArray(rawAnswers)) {
    errors.answers = "Les réponses du diagnostic sont invalides.";
  }
  const entries = rawAnswers && typeof rawAnswers === "object" && !Array.isArray(rawAnswers) ? Object.entries(rawAnswers) : [];
  if (entries.length === 0 || entries.length > MAX_ANSWERS || !entries.every(([key, answer]) => key.length <= MAX_ANSWER_KEY && typeof answer === "string" && answer.trim().length > 0 && answer.trim().length <= MAX_ANSWER_VALUE)) {
    errors.answers = "Les réponses du diagnostic sont invalides.";
  }
  const answers = Object.fromEntries(entries.map(([key, answer]) => [key, (answer as string).trim()]));
  const profile = answers.profile;
  const requiredAnswers = ["profile", "equipmentCount", "equipmentAge", "performance", "updates", "backup", "network", "wifiCoverage", "mfa", "sharedAccounts", "phishing"];
  if (profile === "professional" || profile === "association") requiredAnswers.push("guestWifi", "continuity", "businessDependence");
  // Une v2 est contrôlée exactement par API-INTERNAL avec son snapshot publié.
  // Le BFF ne fait ici qu'une validation de forme, pour ne pas conserver une
  // seconde liste de questions qui bloquerait les changements administratifs.
  if (!(["individual", "professional", "association"] as string[]).includes(profile ?? "")
    || (configurationVersion === 0 && (requiredAnswers.some((key) => !answers[key]) || Object.keys(answers).length !== requiredAnswers.length || Object.keys(answers).some((key) => !requiredAnswers.includes(key))))) errors.answers = "Le profil ou les réponses du diagnostic sont invalides.";

  if (Object.keys(errors).length) return { payload: null, errors };
  return {
    errors,
    payload: {
      answers: answers as PreDiagnosticAnswers,
      configurationVersion,
      name,
      phone,
      email: emailValue || null,
      organisation: organisationValue || null,
      preferredTime: preferredTimeValue || null,
      comment: commentValue || null,
      consent: true,
      website,
    },
  };
}
