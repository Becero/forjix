import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-authenticated-layout',
  imports: [RouterOutlet],
  templateUrl: './authenticated-layout.html',
  styleUrl: './authenticated-layout.scss'
})
export class AuthenticatedLayout {}

