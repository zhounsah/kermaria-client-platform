import type { ServiceSummary } from "@kermaria/shared";

const serviceSymbols: Record<string, string> = {
  personal_hosting: "HDP",
  storage: "HDP",
  backup: "SAV",
  vpn: "VPN",
  rds: "RDS",
  support: "SUP",
  cloud: "CLD",
  documentation: "DOC",
  monitoring: "MON",
  user: "USR",
  other: "SRV",
};

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
