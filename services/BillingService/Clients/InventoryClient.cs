using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace BillingService.Clients;

/// <summary>
/// Comunicação com o microsserviço de estoque.
///
/// Toda falha de rede, timeout ou indisponibilidade é convertida em um
/// <see cref="InventoryResponse{T}"/> com desfecho <c>Unavailable</c>: o
/// BillingService nunca deixa a exceção escapar e nunca fecha uma nota sem a
/// confirmação da baixa de estoque.
/// </summary>
public class InventoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<InventoryClient> _logger;

    public InventoryClient(HttpClient httpClient, ILogger<InventoryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<InventoryResponse<InventoryProduct>> GetProductAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/Products/{productId}",
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.NotFound)
                return InventoryResponse<InventoryProduct>.Failure(
                    InventoryOutcome.ProductNotFound,
                    $"O produto {productId} não foi encontrado no estoque."
                );

            response.EnsureSuccessStatusCode();

            var product = await response.Content.ReadFromJsonAsync<InventoryProduct>(
                JsonOptions,
                cancellationToken
            );

            return product is null
                ? InventoryResponse<InventoryProduct>.Failure(
                    InventoryOutcome.Unavailable,
                    "O serviço de estoque devolveu uma resposta inesperada.")
                : InventoryResponse<InventoryProduct>.Success(product);
        }
        catch (Exception exception) when (IsTransport(exception, cancellationToken))
        {
            _logger.LogError(
                exception,
                "Serviço de estoque indisponível ao consultar o produto {ProductId}.",
                productId
            );

            return InventoryResponse<InventoryProduct>.Failure(
                InventoryOutcome.Unavailable,
                "O serviço de estoque está indisponível no momento."
            );
        }
    }

    /// <summary>
    /// Solicita a baixa de estoque. A <paramref name="operationKey"/> torna a
    /// chamada idempotente: se ela for reenviada (retry, nova tentativa do
    /// usuário), o estoque não é debitado duas vezes.
    /// </summary>
    public async Task<InventoryResponse<ConsumeStockResult>> ConsumeStockAsync(
        string operationKey,
        IReadOnlyList<ConsumeStockItemRequest> items,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/Products/stock/consume",
                new ConsumeStockPayload(operationKey, items),
                JsonOptions,
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.Conflict)
                return InventoryResponse<ConsumeStockResult>.Failure(
                    InventoryOutcome.InsufficientStock,
                    await ReadProblemDetailAsync(response, cancellationToken)
                        ?? "Não há saldo suficiente para os produtos da nota."
                );

            if (response.StatusCode == HttpStatusCode.NotFound)
                return InventoryResponse<ConsumeStockResult>.Failure(
                    InventoryOutcome.ProductNotFound,
                    await ReadProblemDetailAsync(response, cancellationToken)
                        ?? "Um dos produtos da nota não existe mais no estoque."
                );

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ConsumeStockResult>(
                JsonOptions,
                cancellationToken
            );

            return result is null
                ? InventoryResponse<ConsumeStockResult>.Failure(
                    InventoryOutcome.Unavailable,
                    "O serviço de estoque devolveu uma resposta inesperada.")
                : InventoryResponse<ConsumeStockResult>.Success(result);
        }
        catch (Exception exception) when (IsTransport(exception, cancellationToken))
        {
            _logger.LogError(
                exception,
                "Serviço de estoque indisponível ao processar a operação {OperationKey}.",
                operationKey
            );

            return InventoryResponse<ConsumeStockResult>.Failure(
                InventoryOutcome.Unavailable,
                "O serviço de estoque está indisponível no momento."
            );
        }
    }

    private async Task<string?> ReadProblemDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
                JsonOptions,
                cancellationToken
            );

            return problem?.Detail;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Falhas de comunicação com o estoque: serviço fora do ar, DNS, timeout do
    /// pipeline de resiliência (Polly) ou circuito aberto. Só não é tratado o
    /// cancelamento originado pelo próprio cliente da API.
    /// </summary>
    private static bool IsTransport(Exception exception, CancellationToken cancellationToken) =>
        exception is not OperationCanceledException
            || !cancellationToken.IsCancellationRequested;
}
