import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { environment } from './environments/environment';

interface RuntimeConfig {
  apiUrl?: string;
}

async function loadRuntimeConfig(): Promise<void> {
  try {
    const resp = await fetch('assets/config.json', { cache: 'no-store' });
    if (!resp.ok) return;
    const cfg = (await resp.json()) as RuntimeConfig;
    if (cfg?.apiUrl) environment.apiUrl = cfg.apiUrl;
  } catch {
    // Fall back to compile-time defaults silently — happens on bare dev runs without an asset.
  }
}

loadRuntimeConfig().then(() =>
  bootstrapApplication(AppComponent, appConfig).catch((err: unknown) => console.error(err))
);
