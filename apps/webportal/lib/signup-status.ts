// V0.26 : libellés et tons FR pour les statuts de demande d'inscription.
type SignupStatusTone = "success" | "warning" | "danger" | "neutral" | "info";

export function localizeSignupStatus(status: string): string {
  switch (status) {
    case "email_pending":
      return "En attente e-mail";
    case "email_verified":
      return "Vérifiée";
    case "approved":
      return "Approuvée";
    case "rejected":
      return "Refusée";
    case "expired":
      return "Expirée";
    default:
      return status;
  }
}

export function signupStatusTone(status: string): SignupStatusTone {
  switch (status) {
    case "email_verified":
      return "info";
    case "approved":
      return "success";
    case "rejected":
      return "danger";
    case "expired":
      return "neutral";
    case "email_pending":
    default:
      return "warning";
  }
}
