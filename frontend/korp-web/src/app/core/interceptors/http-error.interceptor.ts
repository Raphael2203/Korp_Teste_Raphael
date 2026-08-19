import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { NotificationService } from '../services/notification.service';

/**
 * Traduz os erros HTTP dos microsserviços em mensagens compreensíveis e as
 * exibe ao usuário, mantendo o erro fluindo para quem chamou decidir o resto.
 *
 * O backend responde no formato ProblemDetails (RFC 7807), então na maioria dos
 * casos basta aproveitar o `detail` que já veio pronto.
 */
export const httpErrorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      notifications.error(buildMessage(error));

      return throwError(() => error);
    }),
  );
};

function buildMessage(error: HttpErrorResponse): string {
  // status 0 = o navegador nem conseguiu falar com a API (serviço fora do ar,
  // rede indisponível ou bloqueio de CORS).
  if (error.status === 0) {
    return 'Não foi possível contatar o servidor. Verifique se os serviços estão no ar.';
  }

  const problem = error.error;

  // Erros de validação do ASP.NET Core trazem um dicionário campo -> mensagens.
  if (problem?.errors) {
    const messages = Object.values(problem.errors as Record<string, string[]>).flat();

    if (messages.length > 0) {
      return messages.join(' ');
    }
  }

  if (typeof problem?.detail === 'string' && problem.detail.length > 0) {
    return problem.detail;
  }

  if (typeof problem?.title === 'string' && problem.title.length > 0) {
    return problem.title;
  }

  if (error.status === 503) {
    return 'Serviço temporariamente indisponível. Tente novamente em instantes.';
  }

  return 'Ocorreu um erro inesperado. Tente novamente.';
}
