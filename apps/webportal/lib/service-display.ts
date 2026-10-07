import type { ServiceSummary } from "@kermaria/shared";

const serviceSymbols: Record<string, string> = {
  personal_hosting: "DOC",
  storage: "DOC",
  backup: "SAV",
  vpn: "ACC",
  rds: "BUR",
  support: "AID",
  cloud: "CLD",
  documentation: "DOC",
  monitoring: "MON",
  user: "USR",
  other: "SRV",
};

const legacyClientServiceNames: Readonly<Record<string, string>> = {
  "Hébergement dossier personnel": "Mon espace de fichiers",
  "Sauvegarde dossier personnel": "Protection de mes fichiers",
  "Accès VPN privé": "Connexion à distance sécurisée",
  "Accès bureau distant / RDS": "Mon bureau à distance",
  "Support technique niveau 1": "Aide informatique",
};

const legacyClientServiceDescriptions: Readonly<Record<string, string>> = {
  "Espace d'hébergement fictif pour un dossier personnel, selon le périmètre convenu.":
    "Espace de démonstration pour retrouver vos fichiers.",
  "Sauvegarde planifiée avec vérifications prévues, sans garantie absolue de récupération.":
    "Copie régulière de vos fichiers, avec des contrôles prévus. Leur récupération dépend de leur état au moment du problème.",
  "Sauvegarde quotidienne avec vérifications prévues, sans garantie absolue de récupération.":
    "Copie régulière de vos fichiers, avec des contrôles prévus. Leur récupération dépend de leur état au moment du problème.",
  "Accès VPN chiffré en cours de qualification, adapté au besoin exprimé.":
    "Connexion à distance en préparation, selon votre besoin.",
  "Accès distant fictif suspendu dans la démonstration, sans action sur une infrastructure réelle.":
    "Bureau à distance actuellement suspendu dans cette démonstration.",
  "Premier niveau d'assistance et d'orientation sur les services inclus au périmètre.":
    "Une première aide pour utiliser les services inclus dans votre offre.",
};

const legacyClientServiceScopes: Readonly<Record<string, string>> = {
  "Espace personnel et accès nominatif de démonstration": "Un espace personnel de démonstration",
  "Dossier personnel inclus dans la démonstration": "Les fichiers de démonstration",
  "Un accès nominatif, sous réserve de validation technique": "Un accès personnel, après vérification",
  "Un environnement distant défini selon le besoin": "Un bureau à distance adapté à votre situation",
  "Diagnostic initial et accompagnement selon périmètre convenu": "Première analyse et aide selon votre offre",
};

export function getClientServiceName(service: Pick<ServiceSummary, "name">): string {
  return legacyClientServiceNames[service.name] ?? service.name;
}

export function getClientServiceDescription(service: Pick<ServiceSummary, "description">): string {
  return legacyClientServiceDescriptions[service.description] ?? service.description;
}

export function getClientServiceScope(service: Pick<ServiceSummary, "scope">): string {
  return legacyClientServiceScopes[service.scope] ?? service.scope;
}

export function getClientServiceNextStep(service: Pick<ServiceSummary, "nextStep">): string | undefined {
  if (service.nextStep === "Vérifications techniques prévues avant toute activation") {
    return "Nous effectuerons les vérifications nécessaires avant d'ouvrir cet accès.";
  }
  return service.nextStep;
}

export function getServiceSymbol(service: Pick<ServiceSummary, "type" | "reference">) {
  const explicitSymbol = serviceSymbols[service.type];
  if (explicitSymbol) {
    return explicitSymbol;
  }

  const tokens = service.reference
    .split(/[-_.]/)
    .map((token) => token.trim())
    .filter(Boolean);
  const candidate = tokens[tokens.length - 1] ?? service.reference;

  return candidate.slice(0, 3).toUpperCase();
}
