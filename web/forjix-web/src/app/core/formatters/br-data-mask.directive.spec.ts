import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { BrDataMaskDirective } from './br-data-mask.directive';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, BrDataMaskDirective],
  template: '<input [formControl]="document" brDataMask="document"><input [formControl]="phone" brDataMask="phone">'
})
class MaskHost {
  readonly document = new FormControl('', { nonNullable: true });
  readonly phone = new FormControl('', { nonNullable: true });
}

describe('BrDataMaskDirective', () => {
  let fixture: ComponentFixture<MaskHost>;
  let inputs: NodeListOf<HTMLInputElement>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MaskHost] }).compileComponents();
    fixture = TestBed.createComponent(MaskHost);
    fixture.detectChanges();
    inputs = fixture.nativeElement.querySelectorAll('input');
  });

  it('formats values loaded from the backend when a record is opened', () => {
    fixture.componentInstance.document.setValue('12345678901');
    fixture.componentInstance.phone.setValue('11987654321');
    fixture.detectChanges();

    expect(inputs[0].value).toBe('123.456.789-01');
    expect(inputs[1].value).toBe('(11) 98765-4321');
  });

  it('formats typed values and updates the reactive form once', () => {
    inputs[0].value = '12.345.678/0001-99';
    inputs[0].dispatchEvent(new Event('input'));
    inputs[1].value = '1134567890';
    inputs[1].dispatchEvent(new Event('input'));

    expect(fixture.componentInstance.document.value).toBe('12.345.678/0001-99');
    expect(fixture.componentInstance.phone.value).toBe('(11) 3456-7890');
  });
});
