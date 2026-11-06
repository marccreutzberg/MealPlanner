import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// iOS ignores user-scalable=no, so pinch zoom is stopped at the gesture (Safari-only events).
for (const type of ['gesturestart', 'gesturechange']) {
  document.addEventListener(type, event => event.preventDefault(), { passive: false });
}

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
