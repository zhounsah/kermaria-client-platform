import type { DataSubjectRequestStatus, DataSubjectRequestType } from "@kermaria/shared";

export const dataRequestTypeLabels: Record<DataSubjectRequestType, string> = {
  access: "Consultation", rectification: "Correction", erasure: "Suppression",
  portability: "Récupération", objection: "Opposition", restriction: "Limitation",
};

export const dataRequestStatusLabels: Record<DataSubjectRequestStatus, string> = {
  received: "Reçue", in_progress: "En cours de traitement",
  waiting_for_customer: "Précision attendue", response_ready: "Réponse disponible",
  closed: "Terminée", refused: "Refusée, motif communiqué",
};
