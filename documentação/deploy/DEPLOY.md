# Geração de Artefatos e Deploy

Como transformar o código em artefatos prontos e como instalá-los em um servidor.

> **Nada é publicado automaticamente.** O pipeline apenas *gera* os artefatos e os
> disponibiliza para download. A publicação em servidor é um passo manual e deliberado.

---

## 1. Qualidade de código

Rode antes de gerar qualquer artefato. É exatamente o que o CI executa.

### Backend (.NET)

```bash
# Restaurar dependências
dotnet restore Korp.sln

# Formatação: verifica sem alterar arquivos (as migrations do EF ficam de fora)
dotnet format Korp.sln --verify-no-changes --exclude "**/Migrations/**"

# Formatação: corrige automaticamente
dotnet format Korp.sln --exclude "**/Migrations/**"

# Compilação tratando qualquer aviso como erro
dotnet build Korp.sln --configuration Release -warnaserror
```

### Frontend (Angular)

```bash
cd frontend/korp-web
npm ci

npm run lint           # ESLint + regras do Angular (TypeScript e templates)
npm run lint:fix       # corrige o que for auto-corrigível
npm run format:check   # Prettier: verifica sem alterar
npm run format         # Prettier: aplica a formatação
```

### Testes

O projeto não possui testes automatizados — a decisão foi concentrar o esforço nos
requisitos funcionais. Os comandos já estão no lugar e passam a valer assim que o
primeiro teste for escrito.

```bash
# Backend: hoje a solution não tem projetos de teste e o comando encerra com
# sucesso sem executar nada. Adicione um projeto *.Tests à solution e ele
# passa a rodar aqui e no CI automaticamente:
#   dotnet new xunit -o tests/InventoryService.Tests
#   dotnet sln Korp.sln add tests/InventoryService.Tests
dotnet test Korp.sln --configuration Release

# Frontend: o runner falha se não encontrar nenhum arquivo de teste, então rode
# apenas quando existir algum *.spec.ts
cd frontend/korp-web
npm test -- --watch=false
```

---

## 2. Gerar os artefatos

### Pelo GitHub Actions (recomendado)

O workflow **Artefatos de Deploy** (`.github/workflows/artifacts.yml`) empacota tudo.

- **Manualmente:** aba *Actions* → *Artefatos de Deploy* → *Run workflow*, informando a
  versão (ex.: `1.0.0`).
- **Por tag:** ao publicar uma tag `v*` (ex.: `v1.0.0`), o workflow dispara sozinho.

Ao final, os artefatos ficam disponíveis para download na própria execução:

| Artefato | Conteúdo |
|---|---|
| `InventoryService-<versão>` | Binários publicados do microsserviço de estoque |
| `BillingService-<versão>` | Binários publicados do microsserviço de faturamento |
| `migrations-<versão>` | Scripts SQL idempotentes dos dois bancos |
| `frontend-<versão>` | Build estático do Angular + `nginx.conf` |
| `imagens-docker-<versão>` | Imagens Docker em tarball, para `docker load` |
| `infraestrutura-<versão>` | `docker-compose.yml` de produção, `.env.example` e este guia |

### Localmente

```bash
VERSAO=1.0.0

# ── Binários dos microsserviços ─────────────────────────────────────────────
dotnet publish services/InventoryService -c Release -o artefatos/InventoryService /p:UseAppHost=false
dotnet publish services/BillingService   -c Release -o artefatos/BillingService   /p:UseAppHost=false

# ── Scripts SQL das migrations ──────────────────────────────────────────────
dotnet tool install --global dotnet-ef       # apenas na primeira vez

mkdir -p artefatos/migrations
cp db/init/01-create-databases.sql artefatos/migrations/00-create-databases.sql

dotnet ef migrations script --idempotent \
  --project services/InventoryService \
  --output artefatos/migrations/korp_inventory.sql

dotnet ef migrations script --idempotent \
  --project services/BillingService \
  --output artefatos/migrations/korp_billing.sql

# ── Frontend ────────────────────────────────────────────────────────────────
# As URLs das APIs são compiladas no bundle: informe os endereços do servidor.
cd frontend/korp-web
npm ci
npm run build
cd ../..
mkdir -p artefatos/frontend
cp -r frontend/korp-web/dist/korp-web/browser artefatos/frontend/html
cp frontend/korp-web/nginx.conf artefatos/frontend/nginx.conf

# ── Imagens Docker ──────────────────────────────────────────────────────────
docker build -t korp-inventory-service:$VERSAO services/InventoryService
docker build -t korp-billing-service:$VERSAO   services/BillingService
docker build -t korp-frontend:$VERSAO \
  --build-arg INVENTORY_API_URL=https://estoque.seudominio.com \
  --build-arg BILLING_API_URL=https://faturamento.seudominio.com \
  frontend/korp-web

mkdir -p artefatos/imagens
docker save korp-inventory-service:$VERSAO | gzip > artefatos/imagens/korp-inventory-service.tar.gz
docker save korp-billing-service:$VERSAO   | gzip > artefatos/imagens/korp-billing-service.tar.gz
docker save korp-frontend:$VERSAO          | gzip > artefatos/imagens/korp-frontend.tar.gz
```

> ⚠️ **O endereço das APIs entra no bundle do Angular durante o build.** Uma imagem de
> frontend construída com os valores padrão aponta para `http://localhost:5159` e
> `http://localhost:5160` — o que só funciona na máquina do desenvolvedor. Para o
> servidor, sempre construa passando `--build-arg INVENTORY_API_URL` e
> `--build-arg BILLING_API_URL`.

---

## 3. Instalar no servidor

Pré-requisitos: Docker Engine e Docker Compose v2.

```bash
# 1. Enviar os artefatos para o servidor
scp -r artefatos/ usuario@servidor:/opt/korp/

# 2. No servidor: carregar as imagens (dispensa registry)
cd /opt/korp
for imagem in artefatos/imagens/*.tar.gz; do
  gunzip -c "$imagem" | docker load
done
docker images | grep korp

# 3. Configurar o ambiente
cp artefatos/infra/docker-compose.yml .
cp -r artefatos/infra/db .
cp artefatos/infra/.env.example .env
nano .env        # senha do banco, FRONTEND_ORIGIN, portas, KORP_VERSION

# 4. Subir apenas o banco e aplicar as migrations
docker compose up -d postgres
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d postgres \
  < artefatos/migrations/00-create-databases.sql
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d korp_inventory \
  < artefatos/migrations/korp_inventory.sql
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d korp_billing \
  < artefatos/migrations/korp_billing.sql

# 5. Subir o restante
docker compose up -d

# 6. Conferir
docker compose ps
curl -fsS http://localhost:5159/health && echo
curl -fsS http://localhost:5160/health && echo
```

Os scripts SQL são **idempotentes**: aplicam apenas as migrations que ainda faltam, então
podem ser reexecutados a cada atualização sem risco.

### Atualizar uma versão já instalada

```bash
cd /opt/korp

# 1. Carregar as imagens novas
for imagem in artefatos/imagens/*.tar.gz; do gunzip -c "$imagem" | docker load; done

# 2. Aplicar as migrations pendentes (idempotente)
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d korp_inventory < artefatos/migrations/korp_inventory.sql
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d korp_billing   < artefatos/migrations/korp_billing.sql

# 3. Atualizar KORP_VERSION no .env e recriar os contêineres
docker compose up -d
```

### Rollback

```bash
# Basta apontar o .env para a versão anterior e recriar
sed -i 's/^KORP_VERSION=.*/KORP_VERSION=1.0.0/' .env
docker compose up -d
```

> As migrations **não** têm rollback automático. Se a nova versão alterou o schema,
> gere o script reverso antes de voltar:
> `dotnet ef migrations script <MigrationDestino> <MigrationAtual> --project services/...`

---

## 4. Deploy sem Docker (opcional)

Se o servidor não tiver Docker, use os binários publicados.

**Pré-requisitos:** ASP.NET Core Runtime 9, PostgreSQL e nginx.

```bash
# APIs — a configuração vem por variável de ambiente
export ConnectionStrings__InventoryDatabase="Host=localhost;Port=5432;Database=korp_inventory;Username=korp;Password=..."
export Cors__AllowedOrigins__0="https://korp.seudominio.com"
export ASPNETCORE_URLS="http://0.0.0.0:5159"
dotnet artefatos/InventoryService/InventoryService.dll

export ConnectionStrings__BillingDatabase="Host=localhost;Port=5432;Database=korp_billing;Username=korp;Password=..."
export Services__InventoryUrl="http://localhost:5159"
export ASPNETCORE_URLS="http://0.0.0.0:5160"
dotnet artefatos/BillingService/BillingService.dll

# Frontend — arquivos estáticos servidos pelo nginx
cp -r artefatos/frontend/html/* /var/www/korp/
cp artefatos/frontend/nginx.conf /etc/nginx/sites-available/korp
```

Em produção, registre cada API como serviço do systemd para reinício automático.

---

## 5. Checklist antes do primeiro deploy

- [ ] `POSTGRES_PASSWORD` trocada por uma senha real
- [ ] `FRONTEND_ORIGIN` igual ao endereço público do sistema (o CORS depende disso)
- [ ] Imagem do frontend construída com as URLs reais das APIs
- [ ] `ASPNETCORE_ENVIRONMENT=Production` (desliga o Swagger)
- [ ] `DATABASE_AUTO_MIGRATE=false` e migrations aplicadas pelo script SQL
- [ ] Porta do PostgreSQL **não** publicada para fora
- [ ] HTTPS terminando em um proxy reverso à frente dos contêineres
- [ ] Rotina de backup do volume `postgres-data`
