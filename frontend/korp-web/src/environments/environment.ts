/**
 * Endereços dos microsserviços.
 *
 * As mesmas URLs valem para `ng serve` e para o ambiente Docker: em ambos os
 * casos quem chama as APIs é o navegador, e o docker-compose publica os
 * serviços nas mesmas portas do host.
 */
export const environment = {
  inventoryApiUrl: 'http://localhost:5159',
  billingApiUrl: 'http://localhost:5160',
};
