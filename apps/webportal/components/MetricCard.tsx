type MetricCardProps = {
  label: string;
  value: string;
  detail: string;
  tone?: "blue" | "green" | "amber" | "slate";
};

export function MetricCard({
  label,
  value,
  detail,
  tone = "blue",
}: MetricCardProps) {
  return (
    <article className={`metric-card metric-${tone}`}>
      <span className="metric-label">{label}</span>
      <strong className="metric-value">{value}</strong>
      <span className="metric-detail">{detail}</span>
    </article>
  );
}
