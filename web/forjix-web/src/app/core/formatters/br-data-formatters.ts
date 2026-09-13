import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export type BrDataKind = 'document' | 'cnpj' | 'phone';

export function onlyDigits(value: unknown, maxLength?: number): string {
  const digits = String(value ?? '').replace(/\D/g, '');
  return maxLength === undefined ? digits : digits.slice(0, maxLength);
}

export function formatDocument(value: unknown): string {
  const digits = onlyDigits(value, 14);
  return digits.length > 11 ? formatCnpjDigits(digits) : formatCpfDigits(digits);
}

export function formatCnpj(value: unknown): string {
  return formatCnpjDigits(onlyDigits(value, 14));
}

export function formatPhone(value: unknown): string {
  const digits = onlyDigits(value, 11);
  if (!digits) return '';
  if (digits.length <= 2) return `(${digits}`;

  const areaCode = digits.slice(0, 2);
  const localNumber = digits.slice(2);
  const prefixLength = digits.length === 11 ? 5 : 4;
  const prefix = localNumber.slice(0, prefixLength);
  const suffix = localNumber.slice(prefixLength);
  return `(${areaCode}) ${prefix}${suffix ? `-${suffix}` : ''}`;
}

export function formatBrData(value: unknown, kind: BrDataKind): string {
  if (kind === 'phone') return formatPhone(value);
  if (kind === 'cnpj') return formatCnpj(value);
  return formatDocument(value);
}

export function normalizeDocumentSearch(value: string): string {
  const trimmed = value.trim();
  if (!/^[\d.\-/\s]+$/.test(trimmed)) return value;
  const digits = onlyDigits(trimmed);
  return digits.length === 11 || digits.length === 14 ? digits : value;
}

export function documentValidator(requiredLength?: 14): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const digits = onlyDigits(control.value);
    if (!digits) return null;
    const valid = requiredLength === 14 ? digits.length === 14 : digits.length === 11 || digits.length === 14;
    return valid ? null : { document: true };
  };
}

export const phoneValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const digits = onlyDigits(control.value);
  return !digits || digits.length === 10 || digits.length === 11 ? null : { phone: true };
};

function formatCpfDigits(digits: string): string {
  if (digits.length <= 3) return digits;
  if (digits.length <= 6) return `${digits.slice(0, 3)}.${digits.slice(3)}`;
  if (digits.length <= 9) return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6)}`;
  return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6, 9)}-${digits.slice(9)}`;
}

function formatCnpjDigits(digits: string): string {
  if (digits.length <= 2) return digits;
  if (digits.length <= 5) return `${digits.slice(0, 2)}.${digits.slice(2)}`;
  if (digits.length <= 8) return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5)}`;
  if (digits.length <= 12) return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8)}`;
  return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8, 12)}-${digits.slice(12)}`;
}
