import { PublicPackCard } from "@/components/PublicPackCard";
import type { PublicPackView } from "@/lib/public-packs";

type PublicPackOverviewGridProps = {
  packs: readonly PublicPackView[];
  signupEnabled: boolean;
};

export function PublicPackOverviewGrid({
  packs,
  signupEnabled,
}: PublicPackOverviewGridProps) {
  return (
    <div className="public-pack-grid">
      {packs.map((pack) => (
        <PublicPackCard
          key={pack.key}
          pack={pack}
          signupEnabled={signupEnabled}
        />
      ))}
    </div>
  );
}
