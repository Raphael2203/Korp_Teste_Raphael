# Detalhamento Técnico

Documento exigido no item *"detalhamento técnico da solução"* do desafio. Cada seção
responde diretamente a um dos pontos solicitados.

---

## 1. Ciclos de vida do Angular utilizados

| Hook | Onde | Para quê |
|---|---|---|
| `ngOnInit` | `ProductsComponent` | Carrega a lista de produtos quando a tela entra em cena |
| `ngOnInit` | `InvoiceListComponent` | Carrega as notas fiscais ao abrir a listagem |
| `ngOnInit` | `InvoiceFormComponent` | Busca os produtos disponíveis para seleção na nota |

A escolha do `ngOnInit` (e não do construtor) é proposital: o construtor é usado apenas
para injeção de dependências via `inject()`, mantendo-o livre de efeitos colaterais. As
chamadas HTTP acontecem no `ngOnInit`, quando o componente já está inicializado e suas
propriedades de entrada resolvidas — o que também deixa o componente testável e evita
disparar requisições durante a construção.

Não foram usados `ngOnDestroy` para cancelar inscrições porque todas as chamadas HTTP do
`HttpClient` completam sozinhas após a resposta, não deixando inscrições pendentes.

Além dos hooks, o estado das telas é mantido em **signals** (`signal`, `computed`), a API
reativa do Angular moderno — por exemplo `products`, `loading`, `saving` e `printingId`.
`availableProducts` é um `computed` que deriva, a cada alteração, os produtos que ainda
não foram incluídos na nota.

---

## 2. Uso da biblioteca RxJS

O RxJS é a base da comunicação HTTP: todo método de serviço devolve um `Observable`.

**`finalize`** — usado em toda operação que exibe indicador de processamento. Ele executa
tanto no sucesso quanto no erro, o que garante que o spinner nunca fique preso na tela —
inclusive quando o microsserviço de estoque está fora do ar. É o que sustenta o requisito
"exibir indicador de processamento" da impressão:

```typescript
// invoice-list.ts
print(invoice: Invoice): void {
  this.printingId.set(invoice.id);

  this.invoiceService.print(invoice.id)
    .pipe(finalize(() => this.printingId.set(null)))
    .subscribe({ /* ... */ });
}
```

**`catchError` + `throwError`** — no interceptor global de erros. O interceptor traduz a
falha em mensagem amigável, exibe ao usuário e **repropaga** o erro, para que o componente
ainda possa reagir (a listagem de notas, por exemplo, recarrega os dados após uma falha de
impressão para refletir o estado real do backend):

```typescript
// http-error.interceptor.ts
return next(request).pipe(
  catchError((error: HttpErrorResponse) => {
    notifications.error(buildMessage(error));
    return throwError(() => error);
  })
);
```

**`Observable`** — tipo de retorno de `ProductService` e `InvoiceService`, mantendo as
chamadas *lazy*: nada é disparado até que exista um `subscribe`.

---

## 3. Bibliotecas utilizadas e suas finalidades

### Frontend

| Biblioteca | Finalidade |
|---|---|
| **Angular 22** | Framework da aplicação: componentes standalone, roteamento, formulários reativos e signals |
| **Angular Material 22** | Biblioteca de componentes visuais (detalhada abaixo) |
| **Angular CDK** | Base de comportamento sobre a qual o Material é construído (overlay, a11y) |
| **RxJS 7.8** | Programação reativa nas chamadas HTTP |
| **TypeScript 6** | Tipagem estática em todo o frontend |
| **ESLint + angular-eslint + typescript-eslint** | Análise estática de TypeScript e dos templates |
| **Prettier** | Formatação automática e verificável no CI |

### Backend

| Biblioteca | Finalidade |
|---|---|
| **ASP.NET Core 9** | Framework web dos dois microsserviços |
| **Entity Framework Core 9** | ORM e controle de schema por migrations |
| **Npgsql.EntityFrameworkCore.PostgreSQL** | Provider do PostgreSQL para o EF Core |
| **Microsoft.AspNetCore.OpenApi** | Geração do documento OpenAPI |
| **Swashbuckle.AspNetCore.SwaggerUI** | Interface do Swagger para explorar as APIs |
| **Microsoft.Extensions.Http.Resilience** | Resiliência HTTP entre microsserviços (retry, timeout, circuit breaker sobre Polly) |
| **Anthropic** | SDK oficial para a funcionalidade opcional de IA |
| **dotnet format** (SDK) | Verificação de formatação e convenções, guiada pelo `.editorconfig` |

---

## 4. Bibliotecas de componentes visuais

Foi utilizado o **Angular Material 22**, com tema Material 3 configurado em
`src/material-theme.scss` via o mixin `mat.theme()` (paletas `azure`/`blue`, tipografia
Roboto).

| Componente | Onde é usado |
|---|---|
| `MatToolbar` | Barra superior com a navegação entre Produtos e Notas Fiscais |
| `MatCard` | Agrupamento visual dos formulários e das listagens |
| `MatFormField` / `MatInput` | Campos de código, descrição, saldo e quantidade |
| `MatSelect` | Seleção do produto na composição da nota |
| `MatTable` | Listagem de produtos, de itens da nota e de notas fiscais |
| `MatButton` / `MatIconButton` | Ações (cadastrar, adicionar, remover, imprimir) |
| `MatIcon` | Ícones das ações e da identidade visual |
| `MatChips` | Indicação do status da nota (Aberta / Fechada) |
| `MatProgressSpinner` | Indicador de processamento (carregamento e impressão) |
| `MatSnackBar` | Mensagens de sucesso e de erro ao usuário |
| `MatTooltip` | Dica do botão de sugestão por IA |

---

## 5. Gerenciamento de dependências no Golang

**Não aplicável.** O backend foi implementado em **C# / .NET 9**, alternativa
explicitamente permitida pelo enunciado ("frameworks utilizados no Golang **ou C#**").

O gerenciamento de dependências equivalente é feito pelo **NuGet**, declarado por
`PackageReference` no arquivo `.csproj` de cada serviço, com versões fixadas
explicitamente. A restauração é feita por `dotnet restore` e o lock efetivo de resolução
fica em `obj/project.assets.json`. No frontend, o gerenciamento é feito pelo **npm**, com
`package.json` e o lock determinístico em `package-lock.json` (`npm ci`).

---

## 6. Frameworks utilizados em C#

- **ASP.NET Core 9 (Web API)** — hospedagem, injeção de dependências, pipeline de
  middlewares e Controllers com `[ApiController]`, que fornece validação automática de
  model binding e respostas `ValidationProblemDetails`.
- **Entity Framework Core 9** — mapeamento objeto-relacional com configuração *fluent* em
  `OnModelCreating`, `DbContext` por serviço e **migrations** como fonte de verdade do
  schema (`InitialCreate`, `AddStockOperations`). Em Docker, as migrations pendentes são
  aplicadas na subida via `Database.MigrateAsync()`.
- **Microsoft.Extensions.Http.Resilience (Polly)** — pipeline de resiliência aplicado ao
  `HttpClient` tipado que fala com o estoque.
- **OpenAPI + Swagger UI** — documentação viva das duas APIs.

---

## 7. Tratamento de erros e exceções no backend

O tratamento é feito em quatro camadas, da mais genérica à mais específica.

### 7.1 Tratamento global de exceções

Cada serviço registra um `IExceptionHandler` (`GlobalExceptionHandler`) junto com
`AddProblemDetails()`. Qualquer exceção não prevista é registrada no log com o método e a
rota, e devolvida ao cliente como **ProblemDetails (RFC 7807)** — sem vazar stack trace:

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// ...
app.UseExceptionHandler();
```

### 7.2 Validação de entrada

Os DTOs usam DataAnnotations (`[Required]`, `[Range]`, `[MaxLength]`, `[MinLength]`) e o
atributo `[ApiController]` converte as violações automaticamente em `400` com
`ValidationProblemDetails`, no mesmo formato dos demais erros. O frontend lê o dicionário
`errors` e exibe as mensagens.

### 7.3 Respostas de negócio explícitas

Situações previstas não são tratadas como exceção: viram códigos HTTP com significado.

| Situação | Código | Onde |
|---|---|---|
| Produto ou nota inexistente | `404` | `GetById`, `Print` |
| Código de produto duplicado | `409` | `ProductsController.Create` |
| Nota já impressa | `409` | `InvoicesController.Print` |
| Saldo insuficiente | `409` | `ProductsController.ConsumeStock` |
| Estoque indisponível | `503` | `InvoicesController.MapInventoryFailure` |
| IA não configurada | `503` | `SuggestDescription` |

### 7.4 Falha entre microsserviços

Este é o requisito obrigatório de tratamento de falhas. O `InventoryClient` encapsula toda
comunicação com o estoque e **converte qualquer falha de transporte em um resultado
tipado**, em vez de deixar a exceção escapar:

```csharp
catch (Exception exception) when (IsTransport(exception, cancellationToken))
{
    _logger.LogError(exception, "Serviço de estoque indisponível ...");

    return InventoryResponse<ConsumeStockResult>.Failure(
        InventoryOutcome.Unavailable,
        "O serviço de estoque está indisponível no momento."
    );
}
```

Sobre o `HttpClient` há um pipeline de resiliência com **retry, timeout por tentativa
(5 s), timeout total (20 s) e circuit breaker**. Os tempos são curtos de propósito: o
usuário precisa de feedback rápido quando o estoque cai.

O controller então traduz o desfecho no código HTTP adequado. O ponto crítico é a **ordem
das operações na impressão**: a nota só é fechada **depois** da confirmação da baixa de
estoque.

```csharp
var consume = await _inventoryClient.ConsumeStockAsync(operationKey, items, ct);

if (!consume.IsSuccess)
    return MapInventoryFailure(consume.Outcome, consume.Detail);   // nota segue Aberta

invoice.Status = InvoiceStatus.Closed;                             // só após o sucesso
invoice.ClosedAt = DateTime.UtcNow;
await _context.SaveChangesAsync(ct);
```

Consequências verificadas na prática:

- Estoque fora do ar → `503`, a nota **permanece Aberta** e nada é debitado;
- Estoque de volta → a mesma nota é impressa normalmente (**recuperação**);
- Saldo insuficiente → `409`, a nota **permanece Aberta** e o estado do estoque não muda.

Em nenhum cenário a aplicação fica inconsistente: não existe nota Fechada sem baixa de
estoque, nem baixa de estoque sem nota Fechada.

---

## 8. Uso de LINQ

O LINQ é usado tanto para consulta quanto para escrita, sempre traduzido para SQL pelo
EF Core.

### 8.1 Baixa de estoque atômica — o uso mais relevante

`ExecuteUpdateAsync` gera um único `UPDATE` no banco, **com a condição de saldo dentro do
próprio comando**. Não há leitura seguida de escrita, então não existe janela entre
verificar e debitar:

```csharp
var affected = await _context.Products
    .Where(p => p.Id == item.ProductId && p.Stock >= quantity)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(p => p.Stock, p => p.Stock - quantity));

if (affected == 0)
{
    await transaction.RollbackAsync();
    return await BuildUnavailableStockResultAsync(item);
}
```

`affected == 0` significa "não havia saldo suficiente **no instante do UPDATE**" — e a
operação inteira é revertida. É isso que resolve o cenário de concorrência do enunciado.

### 8.2 Projeções

`Select` projetando direto para o DTO faz o SQL trazer apenas as colunas necessárias:

```csharp
var products = await _context.Products
    .AsNoTracking()
    .OrderBy(p => p.Code)
    .Select(p => new ProductResponse
    {
        Id = p.Id, Code = p.Code, Description = p.Description, Stock = p.Stock
    })
    .ToListAsync();
```

### 8.3 Demais usos

| Operador | Onde | Finalidade |
|---|---|---|
| `AnyAsync` | Código duplicado, chave de operação já aplicada | Verificação de existência sem materializar registros |
| `FirstOrDefaultAsync` | `GetById`, `Print` | Busca de um único registro |
| `Where` + `Contains` | `BuildResponseAsync` | Traz num só `IN (...)` os produtos afetados |
| `Include` | `GetAll`, `GetById`, `Print` | Carrega os itens junto com a nota |
| `OrderBy` / `OrderByDescending` | Listagens | Ordenação no banco |
| `GroupBy` + `Any` | Validação de itens | Detecta o mesmo produto repetido na nota |
| `AsNoTracking` | Consultas de leitura | Dispensa o change tracker em dados só de leitura |
| `Select` (LINQ to Objects) | `InvoiceResponse.FromInvoice`, `totalItems` | Transformações em memória |

---

## 9. Tratamento de concorrência (opcional)

**Cenário do enunciado:** produto com saldo 1 sendo usado por duas notas ao mesmo tempo.

A solução não usa lock otimista com coluna de versão. A condição de saldo faz parte do
`UPDATE` (seção 8.1), então o próprio PostgreSQL serializa as duas tentativas: a primeira
debita, a segunda não encontra linha que satisfaça `Stock >= quantidade` e é revertida.

Complementos:

- toda a baixa roda dentro de uma **transação explícita** — ou todos os itens são
  debitados, ou nenhum;
- os itens são **ordenados por `ProductId`** antes do `UPDATE`, para que requisições
  concorrentes adquiram os locks na mesma ordem e não gerem deadlock;
- a **numeração das notas** vem de uma *sequence* do PostgreSQL, que nunca entrega o mesmo
  número a duas transações.

**Resultado verificado:** duas impressões simultâneas de um produto com saldo 1 →
uma nota fecha, a outra recebe `409`, saldo final `0`. O saldo nunca fica negativo.

---

## 10. Idempotência (opcional)

Toda baixa de estoque carrega uma `OperationKey`, derivada da nota (`invoice-{id}`). A
tabela `StockOperations` tem **índice único** nessa chave.

Antes de debitar, o serviço verifica se a chave já foi processada; em caso positivo,
devolve `200` com `status: "AlreadyApplied"` **sem tocar no estoque**. O registro da chave
é gravado na mesma transação da baixa, de modo que os dois efeitos são indivisíveis. Se
duas requisições com a mesma chave chegarem juntas, o índice único barra a segunda e o
`DbUpdateException` é traduzido no mesmo `AlreadyApplied`.

Isso torna seguros os retries automáticos do pipeline de resiliência e cobre o caso em que
a baixa foi aplicada mas a resposta se perdeu no caminho: repetir a operação não gera
efeito colateral.

**Resultado verificado:** repetir a mesma operação devolve `AlreadyApplied` e o saldo
permanece inalterado.

---

## 11. Uso de Inteligência Artificial (opcional)

Na tela de cadastro de produtos, o botão **"Sugerir descrição"** usa a Claude API
(modelo `claude-opus-5`, SDK oficial `Anthropic` para .NET) para transformar um rascunho
em uma descrição comercial padronizada.

A integração vive no `InventoryService` (`Ai/ProductDescriptionAssistant.cs`) e foi
construída para **degradar graciosamente**: sem a variável `ANTHROPIC_API_KEY`, o endpoint
responde `503` com uma mensagem clara e todo o restante do sistema continua funcionando —
a descrição é simplesmente digitada à mão. Falhas de chamada e recusas do modelo
(`stop_reason == "refusal"`) também são tratadas e nunca derrubam o cadastro.

---

## 12. Integração contínua e qualidade de código

O repositório tem dois workflows do GitHub Actions em `.github/workflows/`. **Nenhum dos
dois publica em servidor ou registry** — o pipeline valida e empacota; a publicação é um
passo futuro e deliberado.

### 12.1 `ci.yml` — validação a cada push e pull request

| Etapa | Comando executado |
|---|---|
| Qualidade — backend | `dotnet format --verify-no-changes` e `dotnet build -warnaserror` |
| Qualidade — frontend | `npm run lint` e `npm run format:check` |
| Testes — backend | `dotnet test Korp.sln` |
| Testes — frontend | `npm test`, condicionado à existência de arquivos de teste |
| Build | `dotnet publish` dos dois serviços e `npm run build` |
| Migrations | `dotnet ef migrations script --idempotent` |
| Stack Docker | `docker compose up -d` + verificação dos health checks |

A etapa de **stack** é a mais relevante do ponto de vista de garantia: ela sobe o ambiente
inteiro — PostgreSQL, os dois microsserviços e o frontend — e só passa se os health checks
responderem. Isso valida a integração real, não apenas a compilação isolada de cada parte.

A etapa de **migrations** tem um efeito colateral útil: gerar o script SQL falha se as
migrations divergirem do modelo do EF Core, o que transforma o pipeline em uma proteção
contra migrations esquecidas.

### 12.2 Ferramentas de qualidade

**Backend** — `dotnet format` (incluso no SDK), guiado pelo `.editorconfig` da raiz, que
fixa indentação, organização de `using`, namespaces com escopo de arquivo, preferência por
campos `readonly` e a nomenclatura `_camelCase` para campos privados. As migrations, por
serem geradas pelo EF Core, ficam fora da verificação. O build roda com `-warnaserror`:
qualquer aviso do compilador ou dos analisadores do .NET quebra o pipeline.

**Frontend** — ESLint com `angular-eslint` e `typescript-eslint` em configuração *flat*
(`eslint.config.js`), cobrindo tanto o TypeScript quanto os templates HTML, incluindo as
regras de acessibilidade do Angular. A formatação fica a cargo do Prettier, verificável no
CI com `npm run format:check`.

Existe também um `.gitattributes` normalizando as quebras de linha para LF. Sem ele, o
repositório editado no Windows e validado em Linux produziria divergências de formatação
que não têm nada a ver com o código.

### 12.3 Sobre os testes automatizados

O projeto **não possui testes automatizados**: o esforço foi concentrado nos requisitos
funcionais e nos cenários de falha exigidos pelo desafio, todos verificados manualmente
(fluxo completo, saldo insuficiente, queda e recuperação do microsserviço, concorrência e
idempotência).

As etapas de teste, porém, já estão no pipeline e passam a valer sem nenhuma alteração
assim que o primeiro teste existir:

- **Backend** — `dotnet test Korp.sln` hoje encerra com sucesso sem executar nada, porque
  a solution não tem projetos de teste. Adicionar um projeto `*.Tests` à solution é
  suficiente para que ele passe a rodar no CI.
- **Frontend** — o runner do Angular falha quando não encontra nenhum arquivo de teste,
  então a etapa detecta a ausência de `*.spec.ts` e é ignorada com um aviso explícito, em
  vez de quebrar o pipeline por um motivo que não é um defeito.

### 12.4 `artifacts.yml` — empacotamento para deploy

Disparado manualmente ou por uma tag `v*`, gera os artefatos necessários para instalar o
sistema em um servidor:

| Artefato | Conteúdo |
|---|---|
| Binários dos dois microsserviços | Saída de `dotnet publish`, dependente de framework |
| Scripts SQL das migrations | `--idempotent`: aplicam apenas o que falta, e podem ser reexecutados |
| Build estático do frontend | Arquivos do Angular + `nginx.conf` |
| Imagens Docker | Tarballs para `docker load`, dispensando registry |
| Infraestrutura | `docker-compose.prod.yml`, `.env.example` e o guia de deploy |

Duas decisões merecem registro:

**Scripts SQL idempotentes em vez de migrar pela aplicação.** Em desenvolvimento, as APIs
aplicam as migrations na subida (`Database__AutoMigrate=true`), o que torna o
`docker compose up` de um clique. Em servidor isso é indesejável: duas réplicas subindo ao
mesmo tempo tentariam migrar simultaneamente, e a aplicação precisaria de permissão de DDL
no banco. Por isso o artefato traz o SQL pronto, para ser aplicado antes do deploy, com
`Database__AutoMigrate=false`.

**URLs das APIs parametrizadas no build do frontend.** O Angular compila a configuração
dentro do bundle, então uma imagem construída com os valores padrão aponta para
`localhost` e não serviria a um servidor. O `Dockerfile` do frontend expõe os argumentos
`INVENTORY_API_URL` e `BILLING_API_URL`, o que torna a mesma base de código utilizável em
qualquer ambiente sem alterar o código-fonte.

---

## 13. Decisões de arquitetura

**Database por serviço.** Um único PostgreSQL hospeda `korp_inventory` e `korp_billing`,
mas cada serviço enxerga apenas o seu. Isso preserva o isolamento de dados dos
microsserviços sem o custo de dois servidores no ambiente local.

**Snapshot do produto na nota.** `InvoiceItem` guarda `ProductCode` e
`ProductDescription` copiados no momento da emissão. A nota é um documento fiscal: alterar
o cadastro do produto depois não pode reescrever o que já foi emitido.

**Status como texto no banco.** `InvoiceStatus` é persistido como string
(`HasConversion<string>()`) — o dado fica legível em consultas diretas e não quebra se a
ordem do enum mudar.

**Consistência sem transação distribuída.** Não há two-phase commit entre os serviços. A
combinação de **ordem das operações** (fechar a nota só após a confirmação da baixa) com
**idempotência** dá o mesmo resultado prático com muito menos acoplamento: qualquer falha
deixa o sistema em um estado válido, e a repetição da operação converge para o estado
correto.
