import type { ButtonHTMLAttributes } from "react";

type SubmitButtonProps = Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  "children" | "type"
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
  ...buttonProps
}: SubmitButtonProps) {
  return (
    <button
      {...buttonProps}
      aria-busy={isSubmitting}
      className={className}
      disabled={disabled || isSubmitting}
      type="submit"
    >
      {isSubmitting ? (
        <span aria-hidden="true" className="button-spinner" />
      ) : null}
      {isSubmitting ? submittingLabel : idleLabel}
    </button>
  );
}
