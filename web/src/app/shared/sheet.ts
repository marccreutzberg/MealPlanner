import { Component, ElementRef, effect, input, output, viewChild } from '@angular/core';

/** A bottom sheet on top of the native <dialog>: focus trap, Escape and backdrop close come for free. */
@Component({
  selector: 'app-sheet',
  template: `
    <dialog #dialog (close)="closed.emit()" (click)="onBackdrop($event)" [attr.aria-labelledby]="titleId">
      <div class="body">
        <h2 [id]="titleId">{{ heading() }}</h2>
        <ng-content />
      </div>
    </dialog>
  `,
  styles: `
    dialog {
      width: 100%;
      max-width: 560px;
      max-height: 92dvh;
      margin: auto auto 0;
      padding: 0;
      border: 0;
      border-radius: 18px 18px 0 0;
      background: var(--surface);
      color: var(--ink);
    }
    dialog[open] { animation: rise 160ms ease-out; }
    dialog::backdrop { background: rgb(10 20 35 / 0.45); }
    .body { padding: 16px var(--gutter) calc(16px + env(safe-area-inset-bottom)); }
    h2 { margin: 0 0 12px; font-size: var(--step-1); }
    @keyframes rise { from { transform: translateY(24px); opacity: 0; } }
  `,
})
export class Sheet {
  private static nextId = 0;
  readonly heading = input.required<string>();
  readonly open = input(false);
  readonly closed = output<void>();
  protected readonly titleId = `sheet-title-${Sheet.nextId++}`;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    effect(() => {
      const el = this.dialog().nativeElement;
      if (this.open() && !el.open) el.showModal();
      if (!this.open() && el.open) el.close();
    });
  }

  protected onBackdrop(event: MouseEvent): void {
    if (event.target === this.dialog().nativeElement) this.dialog().nativeElement.close();
  }
}
