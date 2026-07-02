export const serviceStatus = {
  active: { label: "Actif", tone: "success" },
  pending: { label: "En attente", tone: "warning" },
  suspended: { label: "Suspendu", tone: "danger" },
} as const;

export const invoiceStatus = {
  paid: { label: "Payée", tone: "success" },
  pending: { label: "À régler", tone: "warning" },
  overdue: { label: "En retard", tone: "danger" },
} as const;

export const commercialDocumentStatus = {
  draft: { label: "Brouillon", tone: "neutral" },
  pending_review: { label: "À vérifier", tone: "warning" },
  shared_with_customer: { label: "Partagé au client", tone: "success" },
  cancelled: { label: "Annulé", tone: "neutral" },
  issued: { label: "Facture émise", tone: "success" },
  paid: { label: "Réglée", tone: "success" },
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
  monthly: { label: "Mensuelle", tone: "info" },
} as const;

export const subscriptionStatus = {
  pending_approval: { label: "En attente d'approbation", tone: "warning" },
  pending_activation: { label: "Approuvée, activation en cours", tone: "info" },
  active: { label: "Active", tone: "success" },
  suspended: { label: "Suspendue", tone: "warning" },
  cancelled: { label: "Annulée", tone: "neutral" },
  expired: { label: "Expirée", tone: "neutral" },
} as const;

export const supportStatus = {
  open: {
    label: "Ouverte",
    tone: "info",
    description: "Votre demande a été reçue.",
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
    label: "Résolue",
    tone: "success",
    description: "Votre demande a été résolue.",
  },
  closed: {
    label: "Clôturée",
    tone: "neutral",
    description: "Le suivi de cette demande est clôturé.",
  },
  cancelled: {
    label: "Annulée",
    tone: "neutral",
    description: "Cette demande a été annulée.",
  },
} as const;

export const serviceRequestStatus = {
  received: {
    label: "Reçue",
    tone: "info",
    description: "Votre demande a été reçue.",
  },
  under_review: {
    label: "En étude",
    tone: "warning",
    description: "Votre demande de service est en cours d'étude.",
  },
  accepted: {
    label: "Acceptée",
    tone: "success",
    description:
      "Votre demande a été acceptée. Elle sera traitée manuellement.",
  },
  rejected: {
    label: "Refusée",
    tone: "danger",
    description: "Votre demande ne peut pas être retenue dans ce périmètre.",
  },
  cancelled: {
    label: "Annulée",
    tone: "neutral",
    description: "Cette demande a été annulée.",
  },
  completed: {
    label: "Terminée",
    tone: "success",
    description: "Le traitement manuel de cette demande est terminé.",
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
