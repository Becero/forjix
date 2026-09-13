import { Directive, ElementRef, forwardRef, HostBinding, HostListener, Input } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { BrDataKind, formatBrData } from './br-data-formatters';

@Directive({
  selector: 'input[brDataMask]',
  standalone: true,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => BrDataMaskDirective), multi: true }]
})
export class BrDataMaskDirective implements ControlValueAccessor {
  @Input() brDataMask: BrDataKind = 'document';
  @HostBinding('attr.inputmode') readonly inputMode = 'numeric';
  @HostBinding('attr.autocomplete') readonly autocomplete = 'off';

  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  constructor(private readonly element: ElementRef<HTMLInputElement>) {}

  @HostBinding('attr.maxlength')
  get maxLength(): number {
    return this.brDataMask === 'phone' ? 15 : this.brDataMask === 'cnpj' ? 18 : 18;
  }

  @HostListener('input', ['$event'])
  handleInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    const formatted = formatBrData(value, this.brDataMask);
    this.element.nativeElement.value = formatted;
    this.onChange(formatted);
  }

  @HostListener('blur')
  handleBlur(): void {
    this.onTouched();
  }

  writeValue(value: unknown): void {
    this.element.nativeElement.value = formatBrData(value, this.brDataMask);
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.element.nativeElement.disabled = disabled;
  }
}
