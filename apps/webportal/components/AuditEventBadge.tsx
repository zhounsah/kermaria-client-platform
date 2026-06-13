import { StatusBadge } from "@/components/StatusBadge";

export function AuditEventBadge({ outcome }: { outcome: string }) {
  const tone =
    outcome === "success"
      ? "success"
      : outcome === "refused"
        ? "danger"
        : "warning";

  return <StatusBadge label={outcome} tone={tone} />;
}
