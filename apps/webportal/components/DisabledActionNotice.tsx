type DisabledActionNoticeProps = {
  title: string;
  description: string;
};

export function DisabledActionNotice({
  title,
  description,
}: DisabledActionNoticeProps) {
  return (
    <div className="security-warning" role="status">
      <div className="warning-symbol" aria-hidden="true">
        !
      </div>
      <div>
        <strong>{title}</strong>
        <p>{description}</p>
      </div>
    </div>
  );
}
