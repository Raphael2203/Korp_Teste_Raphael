using Anthropic;
using Anthropic.Models.Messages;

namespace InventoryService.Ai;

/// <summary>
/// Funcionalidade opcional de IA: sugere uma descrição comercial para o produto
/// a partir do código e de um rascunho informado pelo usuário.
///
/// Quando a variável de ambiente ANTHROPIC_API_KEY não está configurada, o
/// assistente fica indisponível e o restante do sistema segue funcionando
/// normalmente (degradação graciosa).
/// </summary>
public class ProductDescriptionAssistant
{
    private const string ModelId = "claude-opus-5";

    private const string Prompt =
        """
        Você é um assistente de cadastro de produtos de um ERP.
        A partir do código e do rascunho informados, escreva UMA descrição
        comercial para o produto.

        Regras:
        - Português do Brasil.
        - Entre 3 e 12 palavras, no máximo 200 caracteres.
        - Sem aspas, sem markdown, sem explicações.
        - Responda apenas com a descrição final.

        Código: {0}
        Rascunho: {1}
        """;

    private readonly ILogger<ProductDescriptionAssistant> _logger;
    private readonly string? _apiKey;

    public ProductDescriptionAssistant(
        IConfiguration configuration,
        ILogger<ProductDescriptionAssistant> logger)
    {
        _logger = logger;

        _apiKey = configuration["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<string?> SuggestDescriptionAsync(
        string code,
        string draft,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return null;

        var client = new AnthropicClient { ApiKey = _apiKey };

        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 300,
            OutputConfig = new OutputConfig { Effort = Effort.Low },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = string.Format(Prompt, code, draft)
                }
            ]
        }, cancellationToken: cancellationToken);

        if (response.StopReason == "refusal")
        {
            _logger.LogWarning(
                "O assistente de IA recusou gerar a descrição do produto {Code}.",
                code
            );

            return null;
        }

        var suggestion = string.Concat(
            response.Content
                .Select(block => block.Value)
                .OfType<TextBlock>()
                .Select(block => block.Text)
        ).Trim();

        return string.IsNullOrWhiteSpace(suggestion)
            ? null
            : suggestion;
    }
}
