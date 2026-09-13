import { FormControl } from '@angular/forms';
import { documentValidator, formatCnpj, formatDocument, formatPhone, normalizeDocumentSearch, onlyDigits, phoneValidator } from './br-data-formatters';

describe('Brazilian data formatters', () => {
  it('formats CPF and does not apply the mask twice', () => {
    expect(formatDocument('12345678901')).toBe('123.456.789-01');
    expect(formatDocument('123.456.789-01')).toBe('123.456.789-01');
  });

  it('formats CNPJ and loaded backend values', () => {
    expect(formatDocument('12345678000199')).toBe('12.345.678/0001-99');
    expect(formatCnpj('12.345.678/0001-99')).toBe('12.345.678/0001-99');
  });

  it('formats fixed and mobile phones', () => {
    expect(formatPhone('1134567890')).toBe('(11) 3456-7890');
    expect(formatPhone('(11) 98765-4321')).toBe('(11) 98765-4321');
  });

  it('handles null and empty values', () => {
    expect(formatDocument(null)).toBe('');
    expect(formatPhone(undefined)).toBe('');
    expect(onlyDigits('')).toBe('');
  });

  it('validates document and phone lengths without requiring optional values', () => {
    expect(documentValidator()(new FormControl('12345678901'))).toBeNull();
    expect(documentValidator()(new FormControl('123'))).toEqual({ document: true });
    expect(documentValidator(14)(new FormControl('12345678000199'))).toBeNull();
    expect(documentValidator(14)(new FormControl('12345678901'))).toEqual({ document: true });
    expect(phoneValidator(new FormControl('1134567890'))).toBeNull();
    expect(phoneValidator(new FormControl('11987654321'))).toBeNull();
    expect(phoneValidator(new FormControl('123'))).toEqual({ phone: true });
  });

  it('normalizes only document-shaped grid searches', () => {
    expect(normalizeDocumentSearch('123.456.789-01')).toBe('12345678901');
    expect(normalizeDocumentSearch('Empresa 123')).toBe('Empresa 123');
  });
});
