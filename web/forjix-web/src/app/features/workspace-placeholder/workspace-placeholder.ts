import { Component, inject } from '@angular/core';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

@Component({
  selector: 'app-workspace-placeholder',
  templateUrl: './workspace-placeholder.html',
  styleUrl: './workspace-placeholder.scss'
})
export class WorkspacePlaceholder {
  protected readonly context = inject(AuthSessionStore).context;
}
