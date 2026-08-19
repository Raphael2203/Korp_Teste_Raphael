FROM node:24-alpine AS build
WORKDIR /src

# As URLs das APIs são compiladas dentro do bundle do Angular. Os valores padrão
# servem ao ambiente local; para publicar em um servidor, informe os endereços
# reais no build:
#
#   docker build \
#     --build-arg INVENTORY_API_URL=https://estoque.seudominio.com \
#     --build-arg BILLING_API_URL=https://faturamento.seudominio.com \
#     -t korp-frontend .
ARG INVENTORY_API_URL=http://localhost:5159
ARG BILLING_API_URL=http://localhost:5160

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

RUN sed -i \
      -e "s#inventoryApiUrl: '.*'#inventoryApiUrl: '${INVENTORY_API_URL}'#" \
      -e "s#billingApiUrl: '.*'#billingApiUrl: '${BILLING_API_URL}'#" \
      src/environments/environment.ts \
    && cat src/environments/environment.ts \
    && npm run build

FROM nginx:alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/korp-web/browser /usr/share/nginx/html

EXPOSE 80
