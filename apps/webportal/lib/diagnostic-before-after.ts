import type {
  BillingV2PublicCatalog,
  BillingV2PublicSelection,
  DiagnosticAnswers,
  DiagnosticRecommendation,
} from "@kermaria/shared";

import {
  SERVICE_CODES,
  findService,
  resolveTierLabel,
} from "@/lib/billing-v2-formules";

export type BeforeAfterItem = {
  before: string;
  after: string;
};

export type BeforeAfterSummary = {
  title: string;
  items: BeforeAfterItem[];
};

export function buildDiagnosticBeforeAfterSummary({
  answers,
  recommendation,
  catalog,
}: {
  answers: DiagnosticAnswers;
  recommendation: DiagnosticRecommendation;
  catalog: BillingV2PublicCatalog;
}): BeforeAfterSummary {
  const items: BeforeAfterItem[] = [];
  const requiresQuote = recommendation.status === "requires_quote";
  const selection = recommendation.selection;

  addItem(items, {
    before: backupBefore(answers),
    after: requiresQuote
      ? "La fréquence sera validée avant de proposer la bonne solution"
      : backupAfter(selection),
  });

  addItem(items, {
    before: restoreBefore(answers),
    after: requiresQuote
      ? "Les modalités de restauration seront précisées avec vous"
      : selection?.backupPersonal || selection?.backupShared
        ? "Possibilité de restaurer vos fichiers selon les conditions du service"
        : "Le besoin de restauration sera clarifié avant activation",
  });

  if (answers.needsWindowsDesktop === true || selection?.remoteDesktop) {
    addItem(items, {
      before: "Bureau Windows distant non encore en place",
      after: requiresQuote
        ? "L'usage du bureau Windows sera cadré avant proposition"
        : "Bureau Windows accessible à distance",
    });
  } else if (
    answers.needsRemoteFiles === true
    || answers.needsVpn === true
    || selection?.vpnTierCode
  ) {
    addItem(items, {
      before: remoteAccessBefore(answers),
      after: requiresQuote
        ? "Le mode d'accès à distance sera confirmé avec vous"
        : remoteAccessAfter(selection),
    });
  }

  addItem(items, {
    before: continuityBefore(answers),
    after: requiresQuote
      ? "Un cadrage permettra de définir la bonne configuration"
      : continuityAfter(answers, selection),
  });

  addItem(items, {
    before: storageBefore(answers),
    after: requiresQuote
      ? "Le volume et les usages seront validés avant activation"
      : storageAfter(catalog, selection),
  });

  return {
    title: requiresQuote ? "Avant cadrage" : "Avant / Après",
    items: items.slice(0, 5),
  };
}

function addItem(items: BeforeAfterItem[], item: BeforeAfterItem) {
  if (!items.some((current) => current.before === item.before)) {
    items.push(item);
  }
}

function backupBefore(answers: DiagnosticAnswers) {
  switch (answers.backupFrequency) {
    case "daily":
      return "Sauvegardes existantes à vérifier";
    case "weekly":
    case "monthly":
    case "rarely":
      return "Rythme de sauvegarde à consolider";
    case "unknown":
      return "Fréquence de sauvegarde inconnue";
  }
}

function backupAfter(selection: BillingV2PublicSelection | null) {
  if (!selection?.backupPersonal && !selection?.backupShared) {
    return "Besoin de sauvegarde à préciser";
  }

  if (selection.backupPersonal && selection.backupShared) {
    return "Sauvegarde du stockage personnel et de l'espace partagé incluse";
  }

  return selection.backupShared
    ? "Sauvegarde de l'espace partagé incluse"
    : "Sauvegarde du stockage personnel incluse";
}

function restoreBefore(answers: DiagnosticAnswers) {
  if (
    answers.restoreTestRecency === "never"
    || answers.restoreTestRecency === "unknown"
  ) {
    return "Restauration non testée";
  }

  if (answers.restoreTestRecency === "more_than_12_months") {
    return "Dernier test de restauration ancien";
  }

  return "Restauration à garder sous contrôle";
}

function remoteAccessBefore(answers: DiagnosticAnswers) {
  if (answers.needsVpn === true) {
    return "Accès sécurisé à distance à mettre en place";
  }

  if (answers.needsRemoteFiles === true) {
    return "Accès distant non encore en place";
  }

  return "Accès distant à confirmer";
}

function remoteAccessAfter(selection: BillingV2PublicSelection | null) {
  if (selection?.vpnTierCode) {
    return "Accès sécurisé à distance inclus dans la configuration";
  }

  return "Accès à vos fichiers prévu par la solution de stockage retenue";
}

function continuityBefore(answers: DiagnosticAnswers) {
  if (answers.recoveryImportance === "high") {
    return "Besoin d'un retour rapide à vos fichiers";
  }

  if (answers.continuityPlan === "no" || answers.continuityPlan === "unknown") {
    return "Plan en cas de panne non défini";
  }

  if (answers.continuityPlan === "partial") {
    return "Plan en cas de panne à compléter";
  }

  return "Organisation de reprise à vérifier";
}

function continuityAfter(
  answers: DiagnosticAnswers,
  selection: BillingV2PublicSelection | null,
) {
  if (selection?.remoteDesktop) {
    return "Accès plus simple à un environnement de travail distant";
  }

  if (answers.recoveryImportance === "high") {
    return "Solution orientée vers un retour plus rapide aux fichiers";
  }

  return "Solution adaptée à votre besoin actuel";
}

function storageBefore(answers: DiagnosticAnswers) {
  if (answers.estimatedStorageGb === null) {
    return "Volume à protéger à confirmer";
  }

  if (answers.estimatedStorageGb === "above_public_max") {
    return "Volume à protéger supérieur aux paliers disponibles en ligne";
  }

  return `Volume à protéger estiné à ${answers.estimatedStorageGb} Go`;
}

function storageAfter(
  catalog: BillingV2PublicCatalog,
  selection: BillingV2PublicSelection | null,
) {
  if (!selection) {
    return "Volume pris en compte dans la recommandation";
  }

  const label = resolveTierLabel(
    findService(catalog, SERVICE_CODES.storagePersonal),
    selection.storagePersonalTierCode,
  );

  return label
    ? `Palier de stockage personnel retenu : ${label}`
    : "Volume pris en compte dans la recommandation";
}
