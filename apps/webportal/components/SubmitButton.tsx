import type { ButtonHTMLAttributes } from "react";

type SubmitButtonProps = Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  "children"
> & {
  idleLabel: string;
  submittingLabel: string;
  isSubmitting: boolean;
};

export function SubmitButton({
  idleLabel,
  submittingLabel,
  isSubmitting,
  className = "button",
  disabled,
  type = "submit",
  ...buttonProps
}: SubmitButtonProps) {
  return (
    <button
      {...buttonProps}
      aria-busy={isSubmitting}
      className={className}
      disabled={disabled || isSubmitting}
      type={type}
    >
      {isSubmitting ? (
        <span aria-hidden="true" className="button-spinner" />
      ) : null}
      {isSubmitting ? submittingLabel : idleLabel}
    </button>
  );
}
