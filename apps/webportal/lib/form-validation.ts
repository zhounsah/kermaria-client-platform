import type {
  LoginPayload,
  ServiceRequestPayload,
  SupportRequestPayload,
} from "@kermaria/shared";

export type FieldErrors<TField extends string> = Partial<
  Record<TField, string>
>;

type ValidationResult<TPayload, TField extends string> = {
  payload: TPayload;
  errors: FieldErrors<TField>;
};

type LoginField = keyof LoginPayload;
type SupportField = keyof SupportRequestPayload;
type ServiceRequestField = keyof ServiceRequestPayload;

export function validateLoginPayload(
  payload: LoginPayload,
): ValidationResult<LoginPayload, LoginField> {
  const normalized = {
    email: payload.email.trim(),
    password: payload.password,
  };
  const errors: FieldErrors<LoginField> = {};

  if (!normalized.email || !normalized.email.includes("@")) {
    errors.email = "Saisissez une adresse e-mail valide.";
  }

  if (!normalized.password) {
    errors.password = "Saisissez votre mot de passe.";
  }

  return { payload: normalized, errors };
}

export function validateSupportRequest(
  payload: SupportRequestPayload,
): ValidationResult<SupportRequestPayload, SupportField> {
  const normalized = {
    ...payload,
    serviceId: payload.serviceId.trim(),
    subject: payload.subject.trim(),
    description: payload.description.trim(),
  };
  const errors: FieldErrors<SupportField> = {};

  if (!normalized.serviceId) {
    errors.serviceId = "Sélectionnez le service concerné.";
  }

  if (normalized.subject.length < 3) {
    errors.subject = "L’objet doit contenir au moins 3 caractères.";
  }

  if (normalized.description.length < 10) {
    errors.description = "La description doit contenir au moins 10 caractères.";
  }

  return { payload: normalized, errors };
}

export function validateServiceRequest(
  payload: ServiceRequestPayload,
): ValidationResult<ServiceRequestPayload, ServiceRequestField> {
  const normalized = {
    catalogItemId: payload.catalogItemId.trim(),
    subject: payload.subject.trim(),
    description: payload.description.trim(),
  };
  const errors: FieldErrors<ServiceRequestField> = {};

  if (!normalized.catalogItemId) {
    errors.catalogItemId = "Sélectionnez la prestation souhaitée.";
  }

  if (normalized.subject.length < 3) {
    errors.subject = "L’objet doit contenir au moins 3 caractères.";
  }

  if (normalized.description.length < 10) {
    errors.description = "La description doit contenir au moins 10 caractères.";
  }

  return { payload: normalized, errors };
}

export function hasFieldErrors<TField extends string>(
  errors: FieldErrors<TField>,
) {
  return Object.keys(errors).length > 0;
}
