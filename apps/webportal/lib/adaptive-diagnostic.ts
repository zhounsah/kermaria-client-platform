import type {
  BillingV2PublicCatalog,
  DiagnosticAnswers,
  DiagnosticRecommendation,
  DiagnosticRecommendationConfig,
} from "@kermaria/shared";

import type {
  DiagnosticAnswerMap,
  DiagnosticContextId,
} from "@/lib/diagnostic-context";
import { recommendOffer } from "@/lib/public-diagnostic";

export type AdaptiveDiagnosticGuidance = {
  title: string;
  body: string;
  points: readonly string[];
};

export type AdaptiveDiagnosticOutcome = {
  guidance: AdaptiveDiagnosticGuidance;
  recommendation: DiagnosticRecommendation | null;
};

export function canContextProduceFormula(context: DiagnosticContextId): boolean {
  return context === "backup" || context === "remote-access";
}

export function buildAdaptiveDiagnosticOutcome(
  context: DiagnosticContextId,
  answers: DiagnosticAnswerMap,
  catalog: BillingV2PublicCatalog,
  recommendationConfig?: DiagnosticRecommendationConfig,
): AdaptiveDiagnosticOutcome {
  const billingAnswers = buildBillingAnswers(context, answers);
  return {
    guidance: buildGuidance(context, answers),
    recommendation: billingAnswers
      ? recommendOffer(billingAnswers, catalog, recommendationConfig)
      : null,
  };
}

function buildBillingAnswers(
  context: DiagnosticContextId,
  answers: DiagnosticAnswerMap,
): DiagnosticAnswers | null {
  if (!canContextProduceFormula(context)) return null;

  const users = readUsers(answers);
  const customerType = readCustomerType(answers);
  if (users === null || customerType === null) return null;

  if (context === "backup") {
    const targets = readMulti(answers, "backup-targets");
    // Les formules publiques représentent ici une protection de fichiers.
    // Un poste complet, un serveur ou un NAS reste volontairement hors
    // sélection automatique afin que le diagnostic ne crée pas une offre.
    if (targets.length !== 1 || targets[0] !== "files") return null;

    const storage = readStorage(answers);
    if (storage === undefined) return null;
    const restoreTest = readSingle(answers, "restore-test");

    return {
      customerType,
      users,
      dataKinds: customerType === "individual"
        ? ["personal_documents"]
        : ["business_documents"],
      estimatedStorageGb: storage,
      needsRemoteFiles: false,
      needsVpn: false,
      needsWindowsDesktop: false,
      recoveryImportance: "normal",
      backupFrequency: "unknown",
      restoreTestRecency:
        restoreTest === "recent"
          ? "less_than_12_months"
          : restoreTest === "old"
            ? "more_than_12_months"
            : restoreTest === "never"
              ? "never"
              : "unknown",
      continuityPlan: "unknown",
    };
  }

  const target = readSingle(answers, "remote-target");
  const sites = readSingle(answers, "sites");
  if (
    !target
    || target === "unknown"
    || target === "several"
    || sites === "several"
    || !["files", "internal-app", "windows-desktop"].includes(target)
  ) {
    return null;
  }

  return {
    customerType,
    users,
    dataKinds: ["work_files"],
    estimatedStorageGb: null,
    needsRemoteFiles: target === "files",
    needsVpn: target === "files" || target === "internal-app",
    needsWindowsDesktop: target === "windows-desktop",
    recoveryImportance: "normal",
    backupFrequency: "unknown",
    restoreTestRecency: "unknown",
    continuityPlan: "unknown",
  };
}

function readSingle(answers: DiagnosticAnswerMap, id: string): string | null {
  const value = answers[id];
  return typeof value === "string" ? value : null;
}

function readMulti(answers: DiagnosticAnswerMap, id: string): readonly string[] {
  const value = answers[id];
  return Array.isArray(value) ? value : [];
}

function readUsers(answers: DiagnosticAnswerMap): number | null {
  const raw = readSingle(answers, "users");
  if (!raw || raw === "unknown") return null;
  if (raw === "12-plus") return 12;
  const users = Number(raw);
  return Number.isInteger(users) && users >= 1 && users <= 11 ? users : null;
}

function readCustomerType(
  answers: DiagnosticAnswerMap,
): DiagnosticAnswers["customerType"] | null {
  const structure = readSingle(answers, "structure");
  return structure === "individual"
    || structure === "business"
    || structure === "association"
    || structure === "other"
    ? structure
    : null;
}

function readStorage(
  answers: DiagnosticAnswerMap,
): DiagnosticAnswers["estimatedStorageGb"] | undefined {
  const raw = readSingle(answers, "storage");
  if (!raw) return undefined;
  if (raw === "unknown") return null;
  if (raw === "above-public-max") return "above_public_max";
  const value = Number(raw);
  return Number.isFinite(value) && value > 0 ? value : undefined;
}

function buildGuidance(
  context: DiagnosticContextId,
  answers: DiagnosticAnswerMap,
): AdaptiveDiagnosticGuidance {
  switch (context) {
    case "backup": {
      const targets = readMulti(answers, "backup-targets");
      const standardFiles = targets.length === 1 && targets[0] === "files";
      return standardFiles
        ? {
            title: "Votre besoin ressemble à une protection de fichiers standard.",
            body:
              "Le volume et le nombre de personnes permettent de vérifier si une formule en ligne peut couvrir ce cas simple. Le tarif reste recalculé à partir du catalogue au moment utile.",
            points: [
              "Vérifier qu'une restauration réelle est possible.",
              "Conserver une copie distincte des fichiers principaux.",
            ],
          }
        : {
            title: "Un cadrage de la sauvegarde est préférable avant de chiffrer.",
            body:
              "Un poste complet, un serveur, un NAS ou un périmètre encore incertain demande de vérifier la source des données, la restauration et les accès avant de choisir une solution.",
            points: [
              "Confirmer précisément ce qui doit être protégé.",
              "Vérifier où se trouvent les données aujourd'hui.",
              "Définir comment une restauration devra se dérouler.",
            ],
          };
    }

    case "remote-access": {
      const target = readSingle(answers, "remote-target");
      const multipleSites = readSingle(answers, "sites") === "several";
      if (target === "windows-desktop") {
        return {
          title: "Votre usage se rapproche d'un bureau Windows distant.",
          body:
            "Vous cherchez surtout à retrouver un environnement Windows complet avec ses logiciels, plutôt qu'à ouvrir simplement un accès au réseau.",
          points: [
            "Vérifier les logiciels et appareils utilisés.",
            "Confirmer le nombre de personnes concernées.",
            "Préciser les données qui doivent rester dans l'environnement distant.",
          ],
        };
      }
      if (target === "files" || target === "internal-app") {
        return {
          title: multipleSites
            ? "L'accès distant doit être cadré avec l'architecture des différents sites."
            : "Votre usage se rapproche d'un accès privé aux ressources existantes.",
          body: multipleSites
            ? "Relier plusieurs lieux peut demander des règles réseau et des chemins d'accès différents. Un échange est préférable avant de choisir la mise en place."
            : "L'objectif est d'atteindre des fichiers ou une application interne sans les exposer directement sur Internet.",
          points: [
            "Lister les ressources réellement nécessaires à distance.",
            "Limiter l'accès aux personnes et appareils autorisés.",
            "Vérifier l'existant avant d'ouvrir ou de remplacer un accès.",
          ],
        };
      }
      return {
        title: "Le type d'accès doit encore être précisé.",
        body:
          "Vos réponses ne permettent pas de choisir proprement entre un accès au réseau et un environnement de travail hébergé.",
        points: [
          "Identifier les applications et fichiers réellement utilisés.",
          "Vérifier où ils sont hébergés aujourd'hui.",
        ],
      };
    }

    case "network":
      return {
        title: "Un audit réseau ciblé est la prochaine étape utile.",
        body:
          "La couverture, les coupures et les performances dépendent des locaux, de l'installation existante et des usages. Le diagnostic prépare l'audit, pas un achat de matériel à l'aveugle.",
        points: [
          "Identifier les zones ou usages réellement gênés.",
          "Relever l'installation et les accès d'administration existants.",
          "Distinguer couverture Wi-Fi, réseau filaire et besoin de séparation.",
        ],
      };

    case "messaging":
      return {
        title: "Votre messagerie peut maintenant être étudiée sur le bon périmètre.",
        body:
          "Le nombre de boîtes, le domaine, l'existant et les éventuelles données à reprendre déterminent la suite. Les licences éventuelles restent distinctes de l'accompagnement.",
        points: [
          "Confirmer qui contrôle le domaine et les comptes administrateurs.",
          "Lister les boîtes, alias et données à conserver.",
          "Vérifier les services qui envoient déjà des messages avec votre domaine.",
        ],
      };

    case "domain-dns":
      return {
        title: "Le domaine doit être repris sans casser les services qui en dépendent.",
        body:
          "Avant toute modification, il faut confirmer les accès et repérer le site, la messagerie ou les autres services déjà raccordés.",
        points: [
          "Identifier le compte qui contrôle le domaine.",
          "Recenser les services actuellement liés au domaine.",
          "Préparer les changements avant de modifier les réglages.",
        ],
      };

    case "server":
      return {
        title: "Une reprise ou une mise en place serveur doit commencer par l'état de l'existant.",
        body:
          "Les usages, les accès, la maintenance actuelle et l'impact d'une panne permettent de définir un périmètre réaliste avant devis.",
        points: [
          "Confirmer les accès d'administration disponibles.",
          "Identifier ce qui tourne réellement sur le serveur.",
          "Vérifier sauvegardes, mises à jour et dépendances avant intervention.",
        ],
      };

    case "web-hosting":
      return {
        title: "L'hébergement doit être adapté au site réel, pas l'inverse.",
        body:
          "Le type de site, le domaine, les accès et l'état des sauvegardes ou mises à jour permettent de préparer une migration ou une reprise sans promettre une compatibilité automatique.",
        points: [
          "Confirmer les accès au site, au domaine et à l'hébergement.",
          "Vérifier le site, ses extensions éventuelles et sa maintenance actuelle.",
          "Prévoir une copie restaurable avant une migration ou une reprise importante.",
        ],
      };

    case "general":
    default:
      return {
        title: "Votre besoin doit d'abord être orienté vers le bon sujet.",
        body: "Choisissez le problème principal pour obtenir des questions réellement pertinentes.",
        points: [],
      };
  }
}
