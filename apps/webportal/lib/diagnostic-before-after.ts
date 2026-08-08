import type {
  DiagnosticAnswers,
  DiagnosticRecommendation,
  ResolvedPublicPackManifest,
} from "@kermaria/shared";

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
  pack,
}: {
  answers: DiagnosticAnswers;
  recommendation: DiagnosticRecommendation;
  pack: ResolvedPublicPackManifest | null;
}): BeforeAfterSummary {
  const items: BeforeAfterItem[] = [];
  const requiresQuote = recommendation.status === "requires_quote";

  addItem(items, {
    before: backupBefore(answers),
    after: requiresQuote
      ? "La fréquence sera validée avant de proposer la bonne solution"
      : backupAfter(pack),
  });

  addItem(items, {
    before: restoreBefore(answers),
    after: requiresQuote
      ? "Les modalités de restauration seront précisées avec vous"
      : pack?.capabilities.supportsBackup
        ? "Possibilité de restaurer vos fichiers selon les conditions du service"
        : "Le besoin de restauration sera clarifié avant activation",
  });

  if (
    answers.needsWindowsDesktop === true
    || pack?.capabilities.supportsWindowsDesktop
  ) {
    addItem(items, {
      before: "Bureau Windows distant non encore en place",
      after: requiresQuote
        ? "L'usage du bureau Windows sera cadré avant proposition"
        : "Bureau Windows accessible à distance",
    });
  } else if (
    answers.needsRemoteFiles === true
    || answers.needsVpn === true
    || pack?.capabilities.supportsRemoteFiles
  ) {
    addItem(items, {
      before: remoteAccessBefore(answers),
      after: requiresQuote
        ? "Le mode d'accès à distance sera confirmé avec vous"
        : remoteAccessAfter(pack),
    });
  }

  addItem(items, {
    before: continuityBefore(answers),
    after: requiresQuote
      ? "Un cadrage permettra de définir la bonne configuration"
      : continuityAfter(answers, pack),
  });

  addItem(items, {
    before: storageBefore(answers),
    after: requiresQuote
      ? "Le volume et les usages seront validés avant activation"
      : storageAfter(answers, pack),
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

function backupAfter(pack: ResolvedPublicPackManifest | null) {
  if (!pack?.capabilities.supportsBackup) {
    return "Besoin de sauvegarde à préciser";
  }

  return findPackText(pack, /sauvegardes?\s+quotidiennes?/i)
    ?? "Sauvegarde incluse dans l'offre recommandée";
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

function remoteAccessAfter(pack: ResolvedPublicPackManifest | null) {
  if (pack?.capabilities.supportsVpn) {
    return "Accès sécurisé inclus selon l'offre recommandée";
  }

  if (pack?.capabilities.supportsRemoteFiles) {
    return "Accès distant à vos fichiers";
  }

  return "Solution adaptée au besoin retenu";
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
  pack: ResolvedPublicPackManifest | null,
) {
  if (pack?.capabilities.supportsWindowsDesktop) {
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

  return `Volume à protéger estimé à ${answers.estimatedStorageGb} Go`;
}

function storageAfter(
  answers: DiagnosticAnswers,
  pack: ResolvedPublicPackManifest | null,
) {
  if (!pack || answers.estimatedStorageGb === null) {
    return "Volume pris en compte dans la recommandation";
  }

  if (answers.estimatedStorageGb <= pack.capabilities.includedStorageGb) {
    return "Volume compatible avec votre besoin estimé";
  }

  return "Volume à confirmer avec l'offre la plus adaptée";
}

function findPackText(
  pack: ResolvedPublicPackManifest,
  pattern: RegExp,
): string | null {
  return pack.included.find((item) => pattern.test(item)) ?? null;
}
