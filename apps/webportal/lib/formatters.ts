export const serviceStatus = {
  active: { label: "Actif", tone: "success" },
  pending: { label: "En attente", tone: "warning" },
  suspended: { label: "Suspendu", tone: "danger" },
} as const;

export const invoiceStatus = {
  paid: { label: "Payee", tone: "success" },
  pending: { label: "A regler", tone: "warning" },
  overdue: { label: "En retard", tone: "danger" },
} as const;

export const commercialDocumentStatus = {
  draft: { label: "Brouillon", tone: "neutral" },
  pending_review: { label: "A verifier", tone: "warning" },
  shared_with_customer: { label: "Partage au client", tone: "success" },
  cancelled: { label: "Annule", tone: "neutral" },
  issued: { label: "Facture emise", tone: "success" },
  paid: { label: "Reglee", tone: "success" },
} as const;

export const commercialDocumentType = {
  quote_draft: "Devis / proposition",
  billing_draft: "Brouillon de suivi",
  informational_invoice: "Document de facturation informatif",
} as const;

export const commercialOfferStatus = {
  active: { label: "Active", tone: "success" },
  inactive: { label: "Inactive", tone: "neutral" },
} as const;

export const commercialOfferBillingCadence = {
  one_time: { label: "Ponctuelle", tone: "neutral" },
  monthly: { label: "Recurrente", tone: "info" },
} as const;

export const commercialOfferPaymentMode = {
  monthly: { label: "Mensualise", tone: "info" },
  upfront: { label: "Comptant", tone: "warning" },
} as const;

export const subscriptionStatus = {
  pending_approval: { label: "En attente d'approbation", tone: "warning" },
  pending_activation: { label: "Approuvee, activation en cours", tone: "info" },
  pending_cancellation: {
    label: "Resiliation programmee",
    tone: "warning",
  },
  active: { label: "Active", tone: "success" },
  suspended: { label: "Suspendue", tone: "warning" },
  cancelled: { label: "Annulee", tone: "neutral" },
  expired: { label: "Expiree", tone: "neutral" },
} as const;

export const subscriptionProvisioningStatus = {
  not_configured: { label: "Non configure", tone: "warning" },
  not_required: { label: "Non requis", tone: "neutral" },
  ready: { label: "Pret", tone: "info" },
  succeeded: { label: "Synchronise", tone: "success" },
  failed: { label: "Echec", tone: "danger" },
} as const;

export const supportStatus = {
  open: {
    label: "Ouverte",
    tone: "info",
    description: "Votre demande a ete recue.",
  },
  in_progress: {
    label: "En cours",
    tone: "warning",
    description: "Votre demande est en cours de traitement.",
  },
  waiting_for_customer: {
    label: "En attente client",
    tone: "warning",
    description: "Votre demande est en attente d'un retour de votre part.",
  },
  resolved: {
    label: "Resolue",
    tone: "success",
    description: "Votre demande a ete resolue.",
  },
  closed: {
    label: "Cloturee",
    tone: "neutral",
    description: "Le suivi de cette demande est cloture.",
  },
  cancelled: {
    label: "Annulee",
    tone: "neutral",
    description: "Cette demande a ete annulee.",
  },
} as const;

export const serviceRequestStatus = {
  received: {
    label: "Recue",
    tone: "info",
    description: "Votre demande a ete recue.",
  },
  under_review: {
    label: "En etude",
    tone: "warning",
    description: "Votre demande de service est en cours d'etude.",
  },
  accepted: {
    label: "Acceptee",
    tone: "success",
    description:
      "Votre demande a ete acceptee. Elle sera traitee manuellement.",
  },
  rejected: {
    label: "Refusee",
    tone: "danger",
    description: "Votre demande ne peut pas etre retenue dans ce perimetre.",
  },
  cancelled: {
    label: "Annulee",
    tone: "neutral",
    description: "Cette demande a ete annulee.",
  },
  completed: {
    label: "Terminee",
    tone: "success",
    description: "Le traitement manuel de cette demande est termine.",
  },
} as const;

export const DISPLAY_TIME_ZONE = "Europe/Paris";

export function formatDate(value: string) {
  return new Intl.DateTimeFormat("fr-FR", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    timeZone: DISPLAY_TIME_ZONE,
  }).format(new Date(value));
}

export function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("fr-FR", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: DISPLAY_TIME_ZONE,
  }).format(new Date(value));
}

export function formatCurrency(value: number) {
  return new Intl.NumberFormat("fr-FR", {
    style: "currency",
    currency: "EUR",
  }).format(value);
}

export function formatCurrencyFromCents(value: number) {
  return formatCurrency(value / 100);
}

export function formatCommitmentMonths(value: number | null | undefined) {
  if (!value || value < 1) {
    return "—";
  }

  return value === 1 ? "1 mois" : `${value} mois`;
}

export function formatBillingIntervalMonths(value: number | null | undefined) {
  if (!value || value < 1) {
    return "—";
  }

  return value === 1 ? "Tous les mois" : `Tous les ${value} mois`;
}

export function formatPaymentModeLabel(
  value: "monthly" | "upfront" | null | undefined,
) {
  if (!value) {
    return "—";
  }

  return commercialOfferPaymentMode[value].label;
}
