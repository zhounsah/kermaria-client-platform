type StatusTone = "success" | "warning" | "danger" | "neutral" | "info";

type StatusBadgeProps = {
  label: string;
  tone?: StatusTone;
};

export function StatusBadge({ label, tone = "neutral" }: StatusBadgeProps) {
  return <span className={`status-badge status-${tone}`}>{label}</span>;
}
