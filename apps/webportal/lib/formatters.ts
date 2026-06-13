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

export const supportStatus = {
  open: { label: "Ouverte", tone: "info" },
  in_progress: { label: "En cours", tone: "warning" },
  closed: { label: "Clôturée", tone: "neutral" },
} as const;

export function formatDate(value: string) {
  return new Intl.DateTimeFormat("fr-FR", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  }).format(new Date(value));
}

export function formatCurrency(value: number) {
  return new Intl.NumberFormat("fr-FR", {
    style: "currency",
    currency: "EUR",
  }).format(value);
}
