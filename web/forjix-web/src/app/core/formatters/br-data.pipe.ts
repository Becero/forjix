import { Pipe, PipeTransform } from '@angular/core';
import { BrDataKind, formatBrData } from './br-data-formatters';

@Pipe({ name: 'brData', standalone: true })
export class BrDataPipe implements PipeTransform {
  transform(value: unknown, kind: BrDataKind): string {
    return formatBrData(value, kind);
  }
}
