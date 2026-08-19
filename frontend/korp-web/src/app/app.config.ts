import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { httpErrorInterceptor } from './core/interceptors/http-error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Os componentes do Angular Material 22 usam animações baseadas em CSS,
    // então o pacote @angular/animations não é necessário.
    provideHttpClient(withInterceptors([httpErrorInterceptor])),
  ],
};
