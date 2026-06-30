import type { ApiError } from "@kermaria/shared";
import {
  CSRF_HEADER_NAME,
  readCsrfTokenFromDocumentCookie,
} from "@/lib/csrf";

type BffSuccess<T> = {
  ok: true;
  status: number;
  data: T;
};

type BffFailure = {
  ok: false;
  status: number;
  error: {
    code: string;
    message: string;
    correlationId?: string;
  };
};

export type BffResult<T> = BffSuccess<T> | BffFailure;

const DEFAULT_TIMEOUT_MS = 15000;

export async function requestBffJson<T>(
  path: `/api/${string}`,
  init: RequestInit,
  timeoutMs = DEFAULT_TIMEOUT_MS,
): Promise<BffResult<T>> {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), timeoutMs);

  try {
    const headers = new Headers(init.headers);
    const csrfToken = shouldAttachCsrfToken(path, init.method)
      ? await ensureCsrfToken()
      : null;
    if (csrfToken) {
      headers.set(CSRF_HEADER_NAME, csrfToken);
    }

    const response = await fetch(path, {
      ...init,
      cache: "no-store",
      headers,
      signal: controller.signal,
    });
    const payload = await parseJsonSafely(response);

    if (!response.ok) {
      const apiError = isApiError(payload) ? payload : null;
      return {
        ok: false,
        status: response.status,
        error: {
          code: apiError?.code ?? "BFF_REQUEST_FAILED",
          message: userMessageFor(
            response.status,
            apiError?.code,
            apiError?.message,
          ),
          correlationId: apiError?.correlation_id,
        },
      };
    }

    if (payload === null) {
      return {
        ok: false,
        status: 502,
        error: {
          code: "INVALID_BFF_RESPONSE",
          message:
            "La réponse reçue est inutilisable. Réessayez dans quelques instants.",
        },
      };
    }

    return {
      ok: true,
      status: response.status,
      data: payload as T,
    };
  } catch (error) {
    const timedOut = error instanceof DOMException && error.name === "AbortError";

    return {
      ok: false,
      status: 0,
      error: {
        code: timedOut ? "BFF_TIMEOUT" : "BFF_UNAVAILABLE",
        message: timedOut
          ? "La demande prend trop de temps. Réessayez dans quelques instants."
          : "Le service est temporairement indisponible. Réessayez dans quelques instants.",
      },
    };
  } finally {
    window.clearTimeout(timeout);
  }
}

async function ensureCsrfToken() {
  const existingToken = readCsrfTokenFromDocumentCookie();
  if (existingToken) {
    return existingToken;
  }

  await fetch("/api/auth/me", {
    cache: "no-store",
    credentials: "same-origin",
    method: "GET",
  });

  return readCsrfTokenFromDocumentCookie();
}

function shouldAttachCsrfToken(
  path: `/api/${string}`,
  method: string | undefined,
) {
  if (!method || !path.startsWith("/api/admin/")) {
    return false;
  }

  // handleAdminGet (cote serveur) exige le jeton CSRF sur tout admin,
  // y compris les GET. On attache donc le jeton pour toute methode.
  return ["GET", "POST", "PATCH", "PUT", "DELETE"].includes(
    method.toUpperCase(),
  );
}

async function parseJsonSafely(response: Response): Promise<unknown | null> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.toLowerCase().includes("application/json")) {
    return null;
  }

  try {
    return await response.json();
  } catch {
    return null;
  }
}

function isApiError(value: unknown): value is ApiError {
  if (!value || typeof value !== "object") {
    return false;
  }

  const candidate = value as Partial<ApiError>;
  return (
    typeof candidate.code === "string"
    && typeof candidate.message === "string"
    && typeof candidate.correlation_id === "string"
  );
}

function userMessageFor(
  status: number,
  code?: string,
  serverMessage?: string,
) {
  if (code === "INVALID_CREDENTIALS" || code === "LOGIN_FAILED") {
    return "Identifiants invalides.";
  }

  // Rate limit dedie AD_PASSWORD_CHANGE_LOCKED a un message specifique
  // pour ne pas le confondre avec un account lockout login.
  if (code === "AD_PASSWORD_CHANGE_LOCKED") {
    return "Trop de tentatives de changement de mot de passe. Reessayez dans quelques minutes.";
  }

  if (code === "ACCOUNT_LOCKED" || status === 429) {
    return "Identifiants invalides ou connexion temporairement indisponible.";
  }

  if (
    code === "SESSION_REQUIRED"
    || code === "SESSION_EXPIRED"
    || code === "SESSION_INVALID"
    || code === "SESSION_REVOKED"
    || status === 401
  ) {
    return "Votre session n’est plus valide. Reconnectez-vous.";
  }

  if (code === "ACCESS_DENIED" || status === 403) {
    if (code === "CSRF_FORBIDDEN") {
      return "La session doit confirmer cette action avant de continuer.";
    }

    return "Vous n’êtes pas autorisé à effectuer cette action.";
  }

  if (code === "INVALID_REQUEST" || status === 400) {
    // Le BFF/API renvoie des messages explicites pour les payloads
    // refuses (renommage, deplacement, mot de passe...). On les
    // propage tels quels au lieu du fallback generique.
    return serverMessage
      ? serverMessage
      : "Vérifiez les champs du formulaire puis réessayez.";
  }

  if (status === 409) {
    if (code === "AD_OBJECT_ALREADY_EXISTS") {
      return "Cet identifiant Active Directory est déjà utilisé (nom de compte ou DN en conflit).";
    }

    return serverMessage
      ? serverMessage
      : "Conflit : la ressource demandée existe déjà ou est dans un état incompatible.";
  }

  if (status === 404) {
    if (code === "PORTAL_DATA_NOT_FOUND") {
      // Cas typique : reference client cible inexistante en DB pour
      // un move cross-client, ou customer/document referencé hors base.
      return "La référence demandée est introuvable côté serveur (par ex. client cible inexistant).";
    }

    if (code === "AD_OBJECT_NOT_FOUND") {
      return "L'objet Active Directory demandé est introuvable dans l'OU configurée.";
    }

    if (code === "AD_NO_LINK_FOR_USER") {
      return "Aucun compte Active Directory n'est associé à ce profil portail.";
    }

    return serverMessage
      ? serverMessage
      : "La ressource demandée est introuvable.";
  }

  if (
    code === "SQL_UNAVAILABLE"
    || code === "INTERNAL_API_UNAVAILABLE"
    || status >= 500
  ) {
    return "Le service est temporairement indisponible. Réessayez dans quelques instants.";
  }

  return serverMessage
    ? serverMessage
    : "La demande n’a pas pu être traitée. Réessayez dans quelques instants.";
}
