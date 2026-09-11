import type {
  BillingV2PublicCatalog,
  DiagnosticAnswers,
  DiagnosticRecommendation,
  DiagnosticRecommendationConfig,
} from "@kermaria/shared";

import type {
  PreDiagnosticProfile,
  PreDiagnosticQuestion,
} from "@/lib/pre-diagnostic";
import { recommendOffer } from "@/lib/public-diagnostic";
import type { DiagnosticContextId } from "@/lib/diagnostic-context";

export type CommercialRecommendation = {
  kind: "standard" | "human_review";
  title: string;
  reason: string;
  recommendation: DiagnosticRecommendation | null;
};

const HUMAN_REVIEW_CONTEXTS = new Set<DiagnosticContextId>([
  "network",
  "messaging",
  "domain-dns",
  "server",
  "web-hosting",
]);

export function isPreDiagnosticHumanReviewContext(context: DiagnosticContextId) {
  return HUMAN_REVIEW_CONTEXTS.has(context);
}

const STORAGE_OPTIONS = [
  { value: "16", label: "Jusqu'à 16 Go" },
  { value: "32", label: "Jusqu'à 32 Go" },
  { value: "64", label: "Jusqu'à 64 Go" },
  { value: "128", label: "Jusqu'à 128 Go" },
  { value: "256", label: "Jusqu'à 256 Go" },
  { value: "above-public-max", label: "Plus de 256 Go" },
  { value: "unknown", label: "Je ne sais pas" },
] as const;

const USER_OPTIONS = [
  ...Array.from({ length: 11 }, (_, index) => ({
    value: String(index + 1),
    label: String(index + 1),
  })),
  { value: "12-plus", label: "12 ou plus" },
] as const;

export function commercialQuestionsForProfile(
  profile: PreDiagnosticProfile | null,
  context: DiagnosticContextId,
): readonly PreDiagnosticQuestion[] {
  if (!profile) return [];

  if (isPreDiagnosticHumanReviewContext(context)) {
    return [
      {
        id: "commercialContextDetail",
        category: "Votre besoin",
        label: contextQuestionLabel(context),
        hint: "Ce type de besoin mérite d'être compris avant de proposer une offre en ligne.",
        options: contextQuestionOptions(context),
      },
    ];
  }

  const organisation = profile !== "individual";
  const intentOptions = context === "backup"
    ? organisation
      ? [
          { value: "team_files", label: "Protéger les fichiers et documents de notre structure" },
          { value: "backup_complex", label: "Protéger un ordinateur complet, un NAS, un serveur ou plusieurs éléments" },
        ]
      : [
          { value: "backup_simple", label: "Protéger simplement des fichiers et documents" },
          { value: "backup_complex", label: "Protéger un ordinateur complet, un NAS, un serveur ou plusieurs éléments" },
        ]
    : context === "remote-access"
      ? [
          { value: "remote_files", label: organisation ? "Accéder aux fichiers de notre structure à distance" : "Retrouver des fichiers privés à distance" },
          { value: "windows_desktop", label: organisation ? "Utiliser des bureaux Windows complets à distance" : "Utiliser un bureau Windows complet à distance" },
          { value: "access_complex", label: "Accéder à plusieurs applications, réseaux ou équipements" },
        ]
      : [
          { value: "backup_simple", label: "Protéger simplement mes fichiers ou documents" },
          { value: "remote_files", label: "Accéder à des fichiers privés à distance" },
          { value: "windows_desktop", label: "Utiliser un bureau Windows à distance" },
          ...(organisation ? [
            { value: "team_files", label: "Protéger et partager les fichiers d'une petite structure" },
            { value: "team_windows", label: "Équiper une petite structure avec des bureaux Windows distants" },
          ] : []),
          { value: "access_complex", label: "Traiter un réseau, un serveur, un NAS ou plusieurs besoins" },
          { value: "unknown", label: "Je ne sais pas encore" },
        ];

  const questions: PreDiagnosticQuestion[] = [{
    id: "commercialIntent",
    category: "Votre besoin",
    label: "Quel résultat cherchez-vous principalement ?",
    options: intentOptions,
  }];

  questions.push({
    id: "commercialScope",
    category: "Votre besoin",
    label: "Quel élément correspond le mieux à ce besoin ?",
    hint: "Cette précision permet de distinguer un besoin standard d'un besoin à cadrer.",
    options: [
      { value: "files", label: "Des fichiers et documents" },
      { value: "windows", label: "Un bureau Windows distant" },
      { value: "device_or_nas", label: "Un ordinateur complet ou un NAS" },
      { value: "server_or_multiple", label: "Un serveur ou plusieurs cibles" },
      { value: "unknown", label: "Je ne sais pas" },
    ],
  });

  if (organisation) {
    questions.push({
      id: "commercialUsers",
      category: "Votre besoin",
      label: "Combien de personnes utiliseront ce service ?",
      options: USER_OPTIONS,
    });
    questions.push({
      id: "commercialSites",
      category: "Votre besoin",
      label: "Combien de sites ou de locaux sont concernés ?",
      options: [
        { value: "one", label: "Un seul site" },
        { value: "several", label: "Plusieurs sites ou locaux" },
      ],
    });
  }

  questions.push({
    id: "commercialStorage",
    category: "Votre besoin",
    label: "Quel volume de fichiers faut-il prévoir environ ?",
    hint: "Une estimation suffit. Au-delà des paliers publics ou sans estimation, un cadrage est préférable.",
    options: STORAGE_OPTIONS,
  });

  return questions;
}

export function recommendPreDiagnosticOffer(
  answers: Record<string, string>,
  profile: PreDiagnosticProfile,
  context: DiagnosticContextId,
  catalog: BillingV2PublicCatalog,
  recommendationConfig: DiagnosticRecommendationConfig,
): CommercialRecommendation {
  if (isPreDiagnosticHumanReviewContext(context)) {
    return humanReview(
      "Ce sujet demande une vérification avant toute proposition commerciale.",
    );
  }

  const intent = answers.commercialIntent;
  const scope = answers.commercialScope;
  const storage = readStorage(answers.commercialStorage);
  const organisation = profile !== "individual";
  const users = organisation ? readUsers(answers.commercialUsers) : 1;
  const expectedScope = intent === "windows_desktop" || intent === "team_windows"
    ? "windows"
    : "files";

  if (
    !intent
    || intent === "access_complex"
    || intent === "backup_complex"
    || intent === "unknown"
    || scope !== expectedScope
    || storage === null
    || users === null
    || (organisation && answers.commercialSites !== "one")
  ) {
    return humanReview(
      "Vos réponses décrivent un besoin qui mérite un cadrage plutôt qu'une formule automatique.",
    );
  }

  const billingAnswers = toBillingAnswers(intent, profile, users, storage);
  if (!billingAnswers) {
    return humanReview(
      "Aucune formule standard ne représente honnêtement le besoin indiqué.",
    );
  }

  const recommendation = recommendOffer(
    billingAnswers,
    catalog,
    recommendationConfig,
  );
  if (recommendation.status !== "standard" || !recommendation.selection) {
    return humanReview(
      "La formule associée à ce profil n'est pas disponible ou compatible avec le catalogue public actuel.",
      recommendation,
    );
  }

  return {
    kind: "standard",
    title: "Une formule correspond à votre besoin",
    reason: commercialReason(intent, organisation),
    recommendation,
  };
}

function toBillingAnswers(
  intent: string,
  profile: PreDiagnosticProfile,
  users: number,
  storage: number,
): DiagnosticAnswers | null {
  const customerType = profile === "individual"
    ? "individual"
    : profile === "association" ? "association" : "business";
  const teamIntent = intent === "team_files" || intent === "team_windows";
  if (profile === "individual" && teamIntent) return null;
  if (profile !== "individual" && !teamIntent && intent === "backup_simple") return null;

  return {
    customerType,
    users,
    dataKinds: [profile === "association" ? "association_data" : profile === "individual" ? "personal_documents" : "business_documents"],
    estimatedStorageGb: storage,
    needsRemoteFiles: intent === "remote_files",
    needsVpn: intent === "remote_files",
    needsWindowsDesktop: intent === "windows_desktop" || intent === "team_windows",
    recoveryImportance: "normal",
    backupFrequency: "unknown",
    restoreTestRecency: "unknown",
    continuityPlan: "unknown",
  };
}

function readStorage(value: string | undefined): number | null {
  if (!value || value === "unknown" || value === "above-public-max") return null;
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 && parsed <= 256 ? parsed : null;
}

function readUsers(value: string | undefined): number | null {
  if (!value || value === "12-plus") return null;
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 1 && parsed <= 11 ? parsed : null;
}

function humanReview(reason: string, recommendation: DiagnosticRecommendation | null = null): CommercialRecommendation {
  return {
    kind: "human_review",
    title: "Ce besoin mérite un échange avant de choisir une offre",
    reason,
    recommendation,
  };
}

function commercialReason(intent: string, organisation: boolean) {
  if (intent === "remote_files") return "Vous avez indiqué un accès privé et simple à des fichiers à distance.";
  if (intent === "windows_desktop" || intent === "team_windows") return "Vous recherchez un environnement Windows distant dans un cadre standard.";
  if (organisation) return "Vous avez indiqué un besoin de petite structure, sur un seul site et dans les limites du catalogue public.";
  return "Vous souhaitez protéger des fichiers avec un volume compatible avec les paliers disponibles en ligne.";
}

function contextQuestionLabel(context: DiagnosticContextId) {
  return context === "web-hosting"
    ? "Quel sujet d'hébergement souhaitez-vous examiner ?"
    : context === "network"
      ? "Quel problème réseau ou Wi-Fi souhaitez-vous résoudre ?"
      : context === "messaging"
        ? "Quel besoin de messagerie avez-vous ?"
        : context === "domain-dns"
          ? "Que souhaitez-vous faire avec votre domaine ou DNS ?"
          : "Quel besoin serveur ou VPS souhaitez-vous examiner ?";
}

function contextQuestionOptions(context: DiagnosticContextId) {
  if (context === "web-hosting") return [
    { value: "site", label: "Créer, migrer ou maintenir un site" },
    { value: "wordpress", label: "Sécuriser ou maintenir WordPress" },
    { value: "access", label: "Retrouver les accès ou reprendre un hébergement" },
  ];
  if (context === "network") return [
    { value: "wifi", label: "Améliorer la couverture ou la stabilité du Wi-Fi" },
    { value: "separation", label: "Séparer les usages ou les équipements" },
    { value: "installation", label: "Préparer ou reprendre une installation" },
  ];
  if (context === "messaging") return [
    { value: "migration", label: "Migrer ou reprendre une messagerie" },
    { value: "addresses", label: "Créer ou organiser des adresses professionnelles" },
    { value: "deliverability", label: "Résoudre un problème d'envoi ou de réception" },
  ];
  if (context === "domain-dns") return [
    { value: "access", label: "Retrouver ou transférer les accès" },
    { value: "configuration", label: "Raccorder le domaine à un site ou une messagerie" },
    { value: "incident", label: "Résoudre un incident en cours" },
  ];
  return [
    { value: "new", label: "Mettre en place un nouveau serveur ou VPS" },
    { value: "takeover", label: "Reprendre, maintenir ou migrer un existant" },
    { value: "incident", label: "Résoudre un incident ou une indisponibilité" },
  ];
}
