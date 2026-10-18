# Brassário

Plataforma web de controle de estoque e insumos para cervejarias artesanais e pequenos laboratórios.
O sistema controla insumos por lote e validade, guarda as receitas como fichas digitais e, ao produzir uma
batelada, abate o estoque automaticamente pelo critério FEFO (primeiro a vencer, primeiro a sair).

Projeto da disciplina do curso de Engenharia de Software - UNICV.

## Equipe

| Integrante | RA |
|---|---|
| Pedro Henrique Bernardo Marques | 179427-2024 |
| Charles de Jesus Souza | 164019-2023 |
| João Victor Fernandes Felix | 176448-2025 |
| Vitoria Gabrielli dos Santos | 168020-2024 |

## O problema

Pequenos produtores perdem insumos por validade vencida e por falta de rastreio de lote, e só descobrem que falta
um ingrediente na hora de produzir. O controle costuma ser feito em planilhas e cadernos, atualizados tarde.
O Brassário resolve isso com a metáfora do **Armário de Ingredientes** (estoque por lote e validade, com alertas
coloridos) e da **Ficha de Receita** (receita que consome o armário ao **Produzir Batelada**).

## Principais funcionalidades

- Cadastro de insumos, fornecedores e entrada de estoque por lote, com validade e preço.
- Armário de Ingredientes com alertas de **Validade Crítica** e de escassez (verde, amarelo e vermelho).
- Fichas de receita com verificação de disponibilidade antes de produzir.
- **Produzir Batelada** com baixa automática de estoque (FEFO), em uma única transação, sem saldo negativo.
- Rastreio de quais lotes foram usados em cada batelada e custo por batelada e por litro.
- Login com JWT e perfis ADMIN e OPERADOR.
- Dashboard com consumo e previsão de estoque.

## Tecnologias

| Camada | Tecnologia |
|---|---|
| Backend | C#, ASP.NET Core Web API, Entity Framework Core |
| Banco de dados | PostgreSQL 16 (administrado pelo pgAdmin) |
| Autenticação | JWT Bearer, BCrypt |
| Documentação da API | Swagger (Swashbuckle) |
| Frontend | React, Vite, React Router, Axios, Recharts |
| Infraestrutura | Docker e Docker Compose, Nginx |
| Testes | xUnit, Moq, Testcontainers (PostgreSQL), Vitest |

## Arquitetura

Backend em **camadas** (Controller, Service, Repository), exposto como API REST em JSON e consumido por um
frontend SPA em React. O Nginx serve o frontend e repassa as chamadas `/api` para o backend.

```
Navegador -> Nginx -> Middleware JWT -> Controller -> Service -> Repository -> PostgreSQL
```

- **Controller:** recebe a requisição, valida os dados e chama o Service.
- **Service:** regras de negócio (FEFO, baixa de estoque, custo, alertas) e transações.
- **Repository:** acesso ao banco com Entity Framework Core.
- **Models e DTOs:** entidades do banco e dados de entrada e saída da API.

## Estrutura do projeto

```
brassario/
  backend/
    Brassario.sln
    Brassario.Api/
      Controllers/  
      Services/  
      Repositories/  
      Models/
      DTOs/Requests/  
      DTOs/Responses/
      Data/  
      Security/  
      Middleware/  
      Exceptions/
      Program.cs  
      appsettings.json  
      Dockerfile
    Brassario.Tests/
  frontend/
    src/
      api/  
      auth/  
      pages/  
      components/  
      hooks/  
      utils/
    package.json  vite.config.js  Dockerfile  nginx.conf
  docs/
  docker-compose.yml
  .env.example
  README.md
```

## Como executar

### Pré-requisitos

- Docker Desktop (para rodar tudo com um comando)
- Para desenvolvimento sem Docker: .NET SDK (mesma versão do projeto), Node.js LTS e Visual Studio ou VS Code
- Git

### Opção 1: tudo com Docker (recomendado)

1. Clone o repositório:
   ```
   git clone <URL-DO-REPOSITORIO>
   cd brassario
   ```
2. Crie o arquivo `.env` a partir do modelo e troque as senhas e a chave do JWT:
   ```
   copy .env.example .env        (Windows)
   cp .env.example .env          (Linux/Mac)
   ```
3. Suba os serviços (banco, API e frontend):
   ```
   docker compose up --build
   ```
4. Acesse:
   - Aplicação: http://localhost:3000
   - Swagger (API): http://localhost:5000/swagger
   - pgAdmin: http://localhost:5050 (veja a seção "Acessar o banco pelo pgAdmin")
   - PostgreSQL (para o pgAdmin instalado no PC ou DBeaver): localhost, porta 5433, usuário e senha do `.env`

Para parar: `docker compose down`. Para apagar também os dados do banco: `docker compose down -v`.

### Opção 2: desenvolvimento local (backend e frontend fora do Docker)

1. Suba só o banco (e o pgAdmin, se quiser):
   ```
   docker compose up -d db pgadmin
   ```
2. **Backend:** crie `backend/Brassario.Api/appsettings.Development.json` com a conexão local
   (o banco está exposto na porta 5433):
   ```json
   {
     "ConnectionStrings": {
       "Default": "Host=localhost;Port=5433;Database=brassario;Username=brassario_user;Password=SUA_SENHA;"
     },
     "Jwt": { "Key": "chave-de-desenvolvimento-com-32-ou-mais-caracteres", "Issuer": "brassario", "ExpiresMinutes": 30 }
   }
   ```
   Abra `backend/Brassario.sln` no Visual Studio, defina `Brassario.Api` como projeto de inicialização e
   pressione F5 (o Swagger abre no navegador). Ou, pelo terminal:
   ```
   cd backend/Brassario.Api
   dotnet run
   ```
3. **Frontend:**
   ```
   cd frontend
   npm install
   npm run dev
   ```
   O app abre em http://localhost:5173. O `vite.config.js` encaminha `/api` para a API; confira se a porta do
   proxy é a da API (veja `Properties/launchSettings.json`).

### Acessar o banco pelo pgAdmin

1. Com o banco no ar, abra o pgAdmin em http://localhost:5050 (ele roda no Docker, serviço `pgadmin`).
2. Clique em **Add New Server** e preencha:
   - Aba **General**: Name = `brassario`
   - Aba **Connection**:
     - Host name/address: `db` (nome do serviço no Docker; se usar o pgAdmin instalado no PC, use `localhost`)
     - Port: `5432` (se usar o pgAdmin instalado no PC, use `5433`)
     - Maintenance database: o valor de `POSTGRES_DB` (padrão: `brassario`)
     - Username e Password: `POSTGRES_USER` e `POSTGRES_PASSWORD` do `.env`
3. Para ver as tabelas: **Servers > brassario > Databases > brassario > Schemas > public > Tables**.
   Para rodar consultas: botão direito no banco, **Query Tool**.

### Banco de dados e migrations

As tabelas são criadas pelas migrations do Entity Framework Core. **Ao iniciar, a API aplica as migrations e
executa o seed automaticamente** (veja "Dados iniciais"), então normalmente não é preciso rodar nada à mão.
Para aplicar as migrations manualmente, com o banco no ar:

```
cd backend/Brassario.Api
dotnet ef database update
```

Para criar uma nova migration: `dotnet ef migrations add NomeDaMigration`.
No Visual Studio, pelo Console do Gerenciador de Pacotes: `Add-Migration NomeDaMigration` e `Update-Database`.

## Variáveis de ambiente

Definidas no arquivo `.env` (modelo em `.env.example`):

| Variável | Descrição |
|---|---|
| POSTGRES_DB | Nome do banco (padrão: brassario) |
| POSTGRES_USER / POSTGRES_PASSWORD | Usuário e senha do PostgreSQL, usados pela API |
| PGADMIN_EMAIL / PGADMIN_PASSWORD | Login do pgAdmin (e-mail em formato válido) |
| JWT_KEY | Chave secreta do JWT (mínimo de 32 caracteres) |
| JWT_ISSUER | Emissor do token (padrão: brassario) |
| JWT_EXPIRES_MINUTES | Validade do token de acesso, em minutos |

O arquivo `.env` não deve ser enviado ao GitHub.

## Dados iniciais (seed)

Na primeira execução (banco vazio), a API cria dados de exemplo para desenvolvimento e demonstração:
1 usuário ADMIN, 2 fornecedores, 8 insumos (3 maltes, 3 lúpulos e 2 leveduras), 16 lotes (2 por insumo) e
1 receita (IPA Cascade, base de 100 L). Se o banco já tiver dados, o seed é ignorado.

Situações preparadas para a demonstração:

| Situação | Como aparece |
|---|---|
| Validade Crítica (vermelho) | Levedura US-05, lote L-88, vence em 6 dias |
| Atenção (amarelo) | Lúpulo Cascade, lote NF1234-A, vence em 25 dias |
| Lote vencido | Malte Caramelo, lote C-12 (não entra no saldo nem na produção) |
| Estoque baixo | Lúpulo Saaz, 0,5 kg contra mínimo de 1 kg |
| Produzir com sucesso | IPA Cascade com 100 L (usa os dois lotes de Cascade, FEFO) |
| Erro de estoque insuficiente | IPA Cascade com 200 L (faltam 0,05 kg de Cascade) |

Para voltar ao estado inicial: `docker compose down -v` e `docker compose up --build`.

## Usuários para teste

Criados automaticamente pelos dados iniciais (seed) do banco.

| Perfil | E-mail | Senha |
|---|---|---|
| ADMIN | admin@brassario.com | Admin@2026 |
| OPERADOR | (cadastrado pelo ADMIN na tela de Usuários) | (definida no cadastro) |

Essas credenciais são só de demonstração e podem ser trocadas em `Seed:AdminEmail` e `Seed:AdminSenha`
(appsettings ou variáveis de ambiente). O OPERADOR só é criado no seed com `Seed:IncluirOperador=true`.

- **ADMIN:** cadastra insumos, receitas, fornecedores e usuários, e ajusta estoque.
- **OPERADOR:** consulta, registra entradas e perdas e produz bateladas.

## Autenticação

1. `POST /api/v1/auth/login` com e-mail e senha devolve um token JWT (validade de 30 minutos).
2. As demais rotas exigem o cabeçalho `Authorization: Bearer <token>`.
3. Sem token a API responde 401, e sem permissão para a ação responde 403.
4. No Swagger, use o botão **Authorize** e cole o token.

## Principais endpoints

Todos com o prefixo `/api/v1`. A lista completa e atualizada está no Swagger.

| Método | Rota | Descrição | Acesso |
|---|---|---|---|
| POST | /auth/login | Autenticar e receber o token | Público |
| GET | /armario | Resumo do Armário de Ingredientes | Autenticado |
| GET, POST | /insumos | Listar e cadastrar insumos | Autenticado / ADMIN |
| POST | /lotes | Entrada de estoque (novo lote) | ADMIN, OPERADOR |
| GET, POST | /receitas | Listar e criar fichas de receita | Autenticado / ADMIN |
| GET | /receitas/{id}/disponibilidade | Simular a produção para um volume | Autenticado |
| POST | /bateladas | Produzir Batelada (baixa automática) | ADMIN, OPERADOR |
| PATCH | /bateladas/{id}/finalizar | Registrar volume final | ADMIN, OPERADOR |
| POST | /bateladas/{id}/cancelar | Cancelar e estornar o estoque | ADMIN, OPERADOR |
| GET | /alertas | Alertas de validade e escassez | Autenticado |

Erros seguem um formato único: `{ timestamp, status, codigo, mensagem, detalhes }`.

## Banco de dados

Principais tabelas: `usuario`, `insumo`, `fornecedor`, `lote_insumo`, `receita`, `receita_item`, `batelada`,
`batelada_consumo`, `movimentacao_estoque` e `refresh_token`. O estoque é controlado por lote, e o saldo de um
lote nunca fica negativo (restrição CHECK no banco). O diagrama (DER) está em `docs/`.

## Testes

```
cd backend
dotnet test
```

Inclui testes unitários (FEFO, escala da receita, custo), de API (login e perfis) e de concorrência (duas
produções simultâneas disputando o mesmo insumo, com PostgreSQL via Testcontainers, que precisa do Docker ativo).
Frontend: `cd frontend && npm test`. Os casos de teste executados e as evidências estão em `docs/`.

## Fluxo de trabalho com Git

- A branch `main` recebe alterações apenas por Pull Request revisado por outro integrante.
- Cada tarefa usa uma branch `feature/nome-da-tarefa`.
- Mensagens de commit no padrão `feat:`, `fix:`, `docs:` e `test:`.
- As tarefas e os responsáveis estão organizados no Trello.

## Documentação

A documentação completa do projeto (concepção, pesquisa com usuários e especificação técnica) está na pasta `docs/`.