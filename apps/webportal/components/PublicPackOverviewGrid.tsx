import type {
  PublicPackCatalogContent,
  ResolvedPublicPackManifest,
} from "@kermaria/shared";

import { PublicPackCard } from "@/components/PublicPackCard";

type PublicPackOverviewGridProps = {
  content: PublicPackCatalogContent;
  packs: readonly ResolvedPublicPackManifest[];
  signupEnabled: boolean;
};

export function PublicPackOverviewGrid({
  content,
  packs,
  signupEnabled,
}: PublicPackOverviewGridProps) {
  const highlightByPackCode = new Map(
    content.packs.map((pack) => [pack.packCode, pack.highlightLabel]),
  );

  return (
    <div className="public-pack-grid">
      {packs.map((pack) => (
        <PublicPackCard
          highlightLabel={highlightByPackCode.get(pack.key) ?? null}
          key={pack.key}
          mode="signup"
          pack={pack}
          signupEnabled={signupEnabled}
        />
      ))}
    </div>
  );
}
