import { StatusBadge } from "@/components/StatusBadge";

const statuses = {
  active: { label: "Active", tone: "success" },
  revoked: { label: "Révoquée", tone: "danger" },
  expired: { label: "Expirée", tone: "warning" },
} as const;

export function SessionStatusBadge({
  status,
}: {
  status: keyof typeof statuses;
}) {
  const presentation = statuses[status];
  return (
    <StatusBadge
      label={presentation.label}
      tone={presentation.tone}
    />
  );
}
