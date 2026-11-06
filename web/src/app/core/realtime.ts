import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';

/** Listens for "the list changed" from the server so both phones stay in sync (spec 4). */
@Injectable({ providedIn: 'root' })
export class Realtime {
  private connection: HubConnection | null = null;
  readonly listChanged = new Subject<void>();
  readonly connected = signal(true);

  async start(): Promise<void> {
    if (this.connection) return;
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/shopping-list')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('ListChanged', () => this.listChanged.next());
    this.connection.onreconnecting(() => this.connected.set(false));
    // Missed messages while offline: reload once we are back.
    this.connection.onreconnected(() => {
      this.connected.set(true);
      this.listChanged.next();
    });
    this.connection.onclose(() => this.connected.set(false));

    try {
      await this.connection.start();
      this.connected.set(this.connection.state === HubConnectionState.Connected);
    } catch {
      this.connected.set(false);
    }
  }
}
