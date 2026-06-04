import { Component, inject } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  template: ` <h2 mat-dialog-title>Report</h2>
    <mat-dialog-content><div [innerHTML]="safe"></div></mat-dialog-content>
    <mat-dialog-actions align="end"
      ><button mat-button mat-dialog-close>Close</button></mat-dialog-actions
    >`,
  styles: [
    `
      mat-dialog-content {
        min-width: 420px;
      }
    `,
  ],
})
export class ReportViewerComponent {
  private sanitizer = inject(DomSanitizer);
  private data = inject<{ html: string }>(MAT_DIALOG_DATA);
  safe = this.sanitizer.bypassSecurityTrustHtml(this.data.html);
}
