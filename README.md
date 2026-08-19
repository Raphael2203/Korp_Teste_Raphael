# Korp — Sistema de Emissão de Notas Fiscais

Desafio técnico KORP/Viasoft: aplicação em **Angular** com backend em **arquitetura de
microsserviços (.NET 9)** e persistência real em **PostgreSQL**.

O fluxo central é: cadastrar produtos → emitir uma nota fiscal com vários produtos
(status **Aberta**) → imprimir a nota, o que **baixa o estoque** e move a nota para
**Fechada**.

> **Documentação**
> - Detalhamento técnico (ciclos de vida do Angular, RxJS, bibliotecas, frameworks,
>   tratamento de erros, LINQ): **[documentação/DETALHAMENTO_TECNICO.md](documentação/entregáveis/DETALHAMENTO_TECNICO.md)**


---

## Arquitetura

```
                    ┌──────────────────────────┐
                    │   Angular 22 (nginx)      │
                    │   http://localhost:4200   │
                    └───────────┬──────────────┘
                                │
            ┌───────────────────┴───────────────────┐
            │ HTTP                                  │ HTTP
            ▼                                       ▼
┌───────────────────────────┐          ┌────────────────────────────┐
│  InventoryService (:5159) │◀─────────│  BillingService (:5160)    │
│  Estoque: produtos/saldos │   HTTP   │  Faturamento: notas fiscais│
└─────────────┬─────────────┘          └──────────────┬─────────────┘
              │                                       │
              ▼                                       ▼
   ┌────────────────────┐                  ┌────────────────────┐
   │ DB korp_inventory  │                  │  DB korp_billing   │
   └────────────────────┘                  └────────────────────┘
              └──────────── PostgreSQL (:5432) ───────┘
```

Dois microsserviços independentes, **cada um com o seu próprio database**. Nenhum
serviço lê ou escreve nas tabelas do outro — o BillingService fala com o estoque
exclusivamente por HTTP.

| Serviço | Responsabilidade | Porta |
|---|---|---|
| `frontend` | Interface Angular servida por nginx | 4200 |
| `inventory-service` | Produtos, saldos e baixa de estoque | 5159 |
| `billing-service` | Notas fiscais, numeração e impressão | 5160 |
| `postgres` | `korp_inventory` + `korp_billing` | 5432 |

---

## Como executar

### Docker (recomendado)

Pré-requisito: Docker Desktop com Docker Compose v2.

```bash
# opcional: habilita a funcionalidade de IA
cp .env.example .env      # e preencha ANTHROPIC_API_KEY

docker compose up --build
```

O compose sobe tudo na ordem certa: o PostgreSQL cria os dois databases, cada API
aplica as suas migrations na subida e o frontend só é publicado depois.

| O quê | Endereço |
|---|---|
| Aplicação | http://localhost:4200 |
| Swagger — Estoque | http://localhost:5159/swagger |
| Swagger — Faturamento | http://localhost:5160/swagger |
| Health checks | http://localhost:5159/health · http://localhost:5160/health |

Para derrubar:

```bash
docker compose down        # mantém os dados
docker compose down -v     # descarta o volume do banco
```

### Execução local (sem Docker)

Pré-requisitos: .NET SDK 9, Node.js ≥ 22.22.3 (exigido pelo Angular CLI 22) e um
PostgreSQL local com os databases `korp_inventory` e `korp_billing`.

```bash
# 1. Estoque
cd services/InventoryService
dotnet ef database update
dotnet run                     # http://localhost:5159

# 2. Faturamento
cd services/BillingService
dotnet ef database update
dotnet run                     # http://localhost:5160

# 3. Frontend
cd frontend/korp-web
npm ci
npm start                      # http://localhost:4200
```

---

## Configuração

As APIs leem configuração de `appsettings.json` e aceitam sobrescrita por variável de
ambiente (padrão do ASP.NET Core, com `__` separando os níveis).

| Variável | Serviço | Descrição |
|---|---|---|
| `ConnectionStrings__InventoryDatabase` | Estoque | Conexão com `korp_inventory` |
| `ConnectionStrings__BillingDatabase` | Faturamento | Conexão com `korp_billing` |
| `Services__InventoryUrl` | Faturamento | Endereço do serviço de estoque |
| `Cors__AllowedOrigins__0` | Ambos | Origem liberada para o frontend |
| `Database__AutoMigrate` | Ambos | Aplica as migrations na subida (`true` no Docker) |
| `ANTHROPIC_API_KEY` | Estoque | Opcional — habilita a sugestão de descrição por IA |

---

## Endpoints

### InventoryService — `http://localhost:5159`

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/Products` | Lista os produtos |
| `GET` | `/api/Products/{id}` | Consulta um produto |
| `POST` | `/api/Products` | Cadastra um produto |
| `POST` | `/api/Products/stock/consume` | Baixa de estoque atômica e idempotente |
| `POST` | `/api/Products/description-suggestion` | Sugestão de descrição por IA (opcional) |
| `GET` | `/health` | Health check |

### BillingService — `http://localhost:5160`

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/Invoices` | Lista as notas |
| `GET` | `/api/Invoices/{id}` | Consulta uma nota |
| `POST` | `/api/Invoices` | Cria a nota (status Aberta, numeração sequencial) |
| `POST` | `/api/Invoices/{id}/print` | Imprime: baixa o estoque e fecha a nota |
| `GET` | `/health` | Health check |

---

## Qualidade de código

```bash
# ── Backend (.NET) ──────────────────────────────────────────────────────────
dotnet format Korp.sln --verify-no-changes --exclude "**/Migrations/**"   # verifica
dotnet format Korp.sln --exclude "**/Migrations/**"                       # corrige
dotnet build Korp.sln -c Release -warnaserror                             # zero avisos
dotnet test Korp.sln -c Release                                           # testes

# ── Frontend (Angular) ──────────────────────────────────────────────────────
cd frontend/korp-web
npm run lint            # ESLint + regras do Angular (TypeScript e templates)
npm run lint:fix        # corrige o que for auto-corrigível
npm run format:check    # Prettier: verifica
npm run format          # Prettier: aplica
npm test                # testes (requer ao menos um *.spec.ts)
```

**Sobre os testes:** o projeto não possui testes automatizados — o esforço foi
concentrado nos requisitos funcionais do desafio. Os comandos e as etapas do pipeline já
estão no lugar e passam a valer assim que o primeiro teste for escrito: basta adicionar um
projeto `*.Tests` à solution ou um arquivo `*.spec.ts` ao frontend.

---

## CI/CD

Dois workflows do GitHub Actions, em `.github/workflows/`. **Nada é publicado em nenhum
servidor ou registry** — o pipeline valida e empacota; a publicação será um passo futuro.

### `ci.yml` — a cada push e pull request na `main`

| Etapa | O que faz |
|---|---|
| **Qualidade — backend** | `dotnet format --verify-no-changes` e build com `-warnaserror` |
| **Qualidade — frontend** | ESLint e Prettier |
| **Testes** | Executa as suítes; sem testes, a etapa é ignorada com aviso |
| **Build** | Compila e publica os dois microsserviços e o frontend |
| **Migrations** | Gera os scripts SQL, garantindo que continuam consistentes com o modelo |
| **Stack Docker** | Sobe o ambiente completo e valida os health checks das duas APIs e do frontend |

### `artifacts.yml` — manual ou por tag `v*`

Empacota tudo o que é necessário para instalar o sistema em um servidor: binários
publicados, scripts SQL idempotentes, build estático do frontend, imagens Docker em
tarball (`docker load`, sem registry) e os arquivos de infraestrutura.

Os comandos equivalentes para rodar localmente, e o passo a passo de instalação no
servidor, estão em **[documentação/deploy/DEPLOY.md](documentação/deploy/DEPLOY.md)**.

---

## Requisitos do desafio e onde estão implementados

| Requisito | Onde |
|---|---|
| Cadastro de produtos (código, descrição, saldo) | `ProductsController` · tela **Produtos** |
| Nota com numeração sequencial e status Aberta/Fechada | `BillingDbContext` (sequence do PostgreSQL) · `Invoice` |
| Múltiplos produtos com quantidades | `InvoiceItem` · tela **Nova nota** |
| Botão de impressão com indicador de processamento | `invoice-list` (RxJS `finalize`) |
| Impressão só de notas Abertas | `InvoicesController.Print` → `409` |
| Atualização do saldo conforme a nota | `ProductsController.ConsumeStock` |
| Arquitetura de microsserviços | `InventoryService` + `BillingService` |
| Tratamento de falhas + recuperação + feedback | `InventoryClient` + `MapInventoryFailure` → `503` |
| Persistência real em banco | EF Core + PostgreSQL, via migrations |
| **Opcional** — concorrência | `UPDATE` condicional (`Stock >= quantidade`) |
| **Opcional** — idempotência | `StockOperation.OperationKey` (índice único) |
| **Opcional** — IA | `ProductDescriptionAssistant` |

---

## Roteiro de demonstração

```bash
# 1. Produto com saldo 10 → nota com 2 unidades → imprimir
#    Resultado: nota Fechada, saldo 8.

# 2. Clicar em Imprimir de novo
#    Resultado: bloqueado ("A nota N está Fechada e não pode ser impressa novamente").

# 3. Falha de microsserviço (requisito obrigatório)
docker compose stop inventory-service
#    Imprimir uma nota Aberta → mensagem de indisponibilidade;
#    a nota permanece Aberta e nenhum saldo é debitado.
docker compose start inventory-service
#    Imprimir novamente → agora conclui normalmente.

# 4. Saldo insuficiente
#    Nota pedindo mais do que existe → recusada, nota segue Aberta.

# 5. Concorrência
#    Produto com saldo 1 em duas notas, impressas ao mesmo tempo:
#    uma fecha, a outra é recusada, saldo final 0 (nunca negativo).
```

---

## Estrutura do repositório

```
.
├── .github/workflows/
│   ├── ci.yml                   # qualidade, testes, build e subida da stack
│   └── artifacts.yml            # empacotamento dos artefatos de deploy
├── docker-compose.yml           # stack local (constrói as imagens)
├── docker-compose.prod.yml      # stack de servidor (consome imagens prontas)
├── Korp.sln                     # solution com os dois microsserviços
├── .editorconfig                # convenções de código do backend
├── db/init/                     # criação dos dois databases
├── services/
│   ├── InventoryService/        # microsserviço de estoque
│   └── BillingService/          # microsserviço de faturamento
├── frontend/korp-web/           # aplicação Angular 22
├── documentação/
│   ├── DETALHAMENTO_TECNICO.md
│   └── deploy/
│       └── DEPLOY.md            # geração de artefatos e instalação
└── README.md
```