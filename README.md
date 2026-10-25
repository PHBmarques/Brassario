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

## Descrição da solução

- **Armário de Ingredientes:** saldo por insumo, lotes em ordem de uso (FEFO), validade e situação
  (verde, amarelo e vermelho) e escassez em relação ao estoque mínimo.
- **Entrada de estoque:** lote com validade, preço e nota fiscal; insumo e fornecedor novos podem ser criados
  na mesma operação.
- **Receitas e disponibilidade:** fichas de receita e simulação da produção para um volume, antes de produzir.
- **Produzir Batelada:** baixa automática pelos lotes mais próximos do vencimento, em uma única transação, sem
  saldo negativo. A resposta mostra os lotes consumidos e o saldo antes e depois.
- **Finalizar e cancelar produção:** a finalização registra volume e custo por litro; o cancelamento exige
  motivo e devolve a quantidade aos mesmos lotes de origem.
- **Alertas:** vencido, validade crítica, atenção, estoque vazio e estoque baixo, cada um com a ação sugerida.
- **Perdas e ajustes:** registro de perda de lote (com motivo) e histórico de movimentações e produções.
- **Dashboard e compras sugeridas:** consumo, previsão e itens a repor.
- **Acesso:** login com JWT e refresh token, perfis ADMIN e OPERADOR, primeiro acesso com senha temporária,
  confirmação de e-mail de recuperação e "esqueci minha senha".

## Tecnologias

| Camada | Tecnologia |
|---|---|
| Backend | C#, ASP.NET Core 10 (Web API), Entity Framework Core 10, FluentValidation |
| Banco de dados | PostgreSQL 16 (administrado pelo pgAdmin) |
| Autenticação | JWT Bearer, refresh token em cookie, BCrypt |
| E-mail | MailKit; Mailpit como caixa de entrada de desenvolvimento |
| Documentação da API | Swagger (Swashbuckle) |
| Frontend | React 19, Vite, React Router, Axios, Recharts |
| Infraestrutura | Docker e Docker Compose, Nginx |
| Testes | xUnit, EF Core InMemory, Testcontainers (PostgreSQL) |

## Arquitetura

Backend em **camadas** (Controller, Service, Repository), exposto como API REST em JSON e consumido por um
frontend SPA em React. O Nginx serve o frontend e repassa as chamadas `/api` para o backend.

```
Navegador -> Nginx -> Middleware JWT -> Controller -> Service -> Repository -> PostgreSQL
```

- **Controller:** recebe a requisição, valida os dados e chama o Service.
- **Service:** regras de negócio (FEFO, baixa de estoque, custo, alertas) e transações.
- **Repository:** acesso ao banco com Entity Framework Core e bloqueio de linhas (`FOR UPDATE`) na baixa.
- **Models e DTOs:** entidades do banco e dados de entrada e saída da API.
- **Middlewares:** erros padronizados e bloqueio da API enquanto a senha temporária estiver ativa.

## Estrutura do projeto

```
Brassario/
  backend/
    Brassario.slnx
    Brassario.Api/
      Controllers/      rotas HTTP (Auth, Armario, Insumos, Lotes, Receitas, Bateladas, Alertas, ...)
      Services/         regras de negócio (BateladaService, AlertasService, EntradaService, ...)
      Repositories/     acesso ao banco (EF Core)
      Models/           entidades e enums
      DTOs/Request/     dados de entrada
      DTOs/Response/    dados de saída
      Data/             AppDbContext, Configurations/, Migrations/, DbSeeder
      Security/         JWT, hash de senha (BCrypt), política de senha
      Middleware/       tratamento de erros e troca de senha pendente
      Exceptions/       exceções de negócio, validação, conflito e 404
      Swagger/          configuração do Swagger
      Program.cs  appsettings.json  Dockerfile
    Brassario.Tests/    testes unitários, de API e com PostgreSQL real
  frontend/
    src/
      Api/  Auth/  Pages/  components/  hooks/  Theme/  Utils/
    package.json  vite.config.js  Dockerfile  nginx.conf
  docs/
  docker-compose.yml
  .env.example
  README.md
```

## Como executar

### Pré-requisitos

- Docker Desktop (para rodar tudo com um comando)
- Para desenvolvimento sem Docker: .NET SDK 10, Node.js LTS e Visual Studio ou VS Code
- Git

### Opção 1: tudo com Docker (recomendado)

1. Clone o repositório:
   ```
   git clone <URL-DO-REPOSITORIO>
   cd Brassario
   ```
2. Crie o arquivo `.env` a partir do modelo e troque as senhas e a chave do JWT:
   ```
   copy .env.example .env        (Windows)
   cp .env.example .env          (Linux/Mac)
   ```
3. Suba os serviços (banco, pgAdmin, API, frontend e Mailpit):
   ```
   docker compose up --build
   ```
4. Acesse:

   | Serviço | Endereço |
   |---|---|
   | Aplicação | http://localhost:3000 |
   | Swagger (API) | http://localhost:5000/swagger |
   | Mailpit (e-mails enviados pelo sistema) | http://localhost:8025 |
   | pgAdmin | http://localhost:5050 |
   | PostgreSQL (pgAdmin ou DBeaver instalados no PC) | localhost, porta 5433, usuário e senha do `.env` |

Para parar: `docker compose down`. Para apagar também os dados do banco: `docker compose down -v`.

### Opção 2: desenvolvimento local (backend e frontend fora do Docker)

1. Suba o banco, o Mailpit e, se quiser, o pgAdmin:
   ```
   docker compose up -d db mailpit pgadmin
   ```
2. **Backend:** crie `backend/Brassario.Api/appsettings.Development.json` (arquivo ignorado pelo Git) com a conexão
   local. O banco está exposto na porta 5433:
   ```json
   {
     "ConnectionStrings": {
       "Default": "Host=localhost;Port=5433;Database=brassario;Username=postgres;Password=SUA_SENHA;"
     },
     "Jwt": { "Key": "chave-de-desenvolvimento-com-32-ou-mais-caracteres", "Issuer": "brassario", "ExpiresMinutes": 30 }
   }
   ```
   Abra `backend/Brassario.slnx` no Visual Studio, defina `Brassario.Api` como projeto de inicialização e
   pressione F5. Ou, pelo terminal:
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
   O app abre em http://localhost:5173. O `vite.config.js` encaminha `/api` para `http://localhost:5000`; se a
   API subir em outra porta (veja `Properties/launchSettings.json`), ajuste o `target` do proxy.

### Acessar o banco pelo pgAdmin

1. Com o banco no ar, abra o pgAdmin em http://localhost:5050 e entre com `PGADMIN_EMAIL` e `PGADMIN_PASSWORD`.
2. Clique em **Add New Server** e preencha:
   - Aba **General**: Name = `brassario`
   - Aba **Connection**: Host `db` e porta `5432` (pgAdmin do Docker) ou `localhost` e `5433` (pgAdmin instalado
     no PC); Maintenance database = valor de `POSTGRES_DB`; Username e Password = `POSTGRES_USER` e
     `POSTGRES_PASSWORD`.
3. As tabelas ficam em **Servers > brassario > Databases > brassario > Schemas > public > Tables**.

### Banco de dados e migrations

As tabelas são criadas pelas migrations do Entity Framework Core. **Ao iniciar, a API aplica as migrations e
executa o seed automaticamente**, então normalmente não é preciso rodar nada à mão. Para aplicar manualmente,
com o banco no ar:

```
cd backend/Brassario.Api
dotnet ef database update
```

Nova migration: `dotnet ef migrations add NomeDaMigration`. No Visual Studio (Console do Gerenciador de Pacotes):
`Add-Migration NomeDaMigration` e `Update-Database`.

## Variáveis de ambiente

Definidas no arquivo `.env` (modelo em `.env.example`):

| Variável | Descrição |
|---|---|
| POSTGRES_DB | Nome do banco (padrão: brassario) |
| POSTGRES_USER / POSTGRES_PASSWORD | Usuário e senha do PostgreSQL, usados pela API |
| PGADMIN_EMAIL / PGADMIN_PASSWORD | Login do pgAdmin (e-mail em formato válido) |
| JWT_KEY | Chave secreta do JWT (mínimo de 32 caracteres) |
| JWT_ISSUER | Emissor do token (padrão: brassario) |
| JWT_EXPIRES_MINUTES | Validade do token de acesso, em minutos (padrão: 30) |
| ASPNETCORE_ENVIRONMENT | Ambiente da API (padrão: Development) |
| EMAIL_HOST / EMAIL_PORT / EMAIL_FROM | Servidor SMTP (padrão: Mailpit na porta 1025) |
| APP_URL_FRONT | Endereço do frontend usado nos links dos e-mails |

Os arquivos `.env` e `appsettings.Development.json` não devem ser enviados ao GitHub.

## Dados iniciais (seed)

Na primeira execução (banco vazio), a API cria apenas o usuário ADMIN. **Nenhum insumo, fornecedor, lote ou receita
é cadastrado**: tudo é criado pelo sistema. Para carregar também os dados de exemplo (2 fornecedores, 8 insumos,
16 lotes e a receita IPA Cascade, base de 100 L), suba com `Seed__IncluirDadosExemplo=true`. Se o banco já tiver
dados, o seed é ignorado.

| Situação | Como aparece |
|---|---|
| Validade Crítica (vermelho) | Levedura US-05, lote L-88, vence em 6 dias |
| Atenção (amarelo) | Lúpulo Cascade, lote NF1234-A, vence em 25 dias |
| Lote vencido | Malte Caramelo, lote C-12 (não entra no saldo nem na produção) |
| Estoque baixo | Lúpulo Saaz, 0,5 kg contra mínimo de 1 kg |
| Produzir com sucesso | IPA Cascade com 100 L (usa os dois lotes de Cascade, FEFO) |
| Erro de estoque insuficiente | IPA Cascade com 200 L (faltam 0,05 kg de Cascade) |

Para voltar ao estado inicial: `docker compose down -v` e `docker compose up --build`.

## Usuários de teste

| Perfil | E-mail | Senha | Como é criado |
|---|---|---|---|
| ADMIN | admin@brassario.com | Admin@2026 | Seed, automático |
| OPERADOR | operador@brassario.com | Operador@2026 | Seed, só com `Seed__IncluirOperador=true` |

- **OPERADOR sem a opção do seed:** entre como ADMIN, abra a tela **Usuários** e cadastre o operador. O sistema
  gera uma senha temporária, e o primeiro acesso exige a troca da senha.
- **E-mails do sistema** (confirmação, recuperação de senha) chegam ao Mailpit, em http://localhost:8025.
- As credenciais são só de demonstração. Podem ser trocadas em `Seed:AdminEmail`, `Seed:AdminSenha`,
  `Seed:OperadorEmail` e `Seed:OperadorSenha` (appsettings ou variáveis de ambiente, com `__` no lugar de `:`).

| Perfil | Pode |
|---|---|
| ADMIN | Tudo do OPERADOR, mais cadastrar insumos, receitas, fornecedores e usuários, e ajustar estoque |
| OPERADOR | Consultar, registrar entradas e perdas e produzir bateladas |

## Autenticação

1. `POST /api/v1/auth/login` com e-mail e senha devolve o token JWT (30 minutos) e grava o refresh token em cookie.
2. As demais rotas exigem `Authorization: Bearer <token>`.
3. Sem token a API responde 401; sem permissão para a ação, 403.
4. No Swagger, use o botão **Authorize** e cole o token.

## Principais endpoints

Prefixo `/api/v1`. A lista completa e atualizada está no Swagger.

| Método | Rota | Descrição | Acesso |
|---|---|---|---|
| POST | /auth/login | Autenticar e receber o token | Público |
| GET | /armario | Resumo do Armário de Ingredientes | Autenticado |
| GET, POST | /insumos | Listar e cadastrar insumos | Autenticado / ADMIN |
| POST | /lotes | Entrada de estoque (novo lote) | ADMIN, OPERADOR |
| POST | /lotes/{id}/perdas | Registrar perda de lote | ADMIN, OPERADOR |
| GET, POST | /receitas | Listar e criar fichas de receita | Autenticado / ADMIN |
| GET | /receitas/{id}/disponibilidade | Simular a produção para um volume | Autenticado |
| POST | /bateladas | Produzir Batelada (baixa automática) | ADMIN, OPERADOR |
| GET | /bateladas/{id} | Detalhe da produção (lotes, estornos) | Autenticado |
| PATCH | /bateladas/{id}/finalizar | Registrar volume final | ADMIN, OPERADOR |
| POST | /bateladas/{id}/cancelar | Cancelar e estornar o estoque | ADMIN, OPERADOR |
| GET | /alertas | Alertas de validade e escassez | Autenticado |
| GET | /usuarios | Gerenciar usuários | ADMIN |

Erros seguem um formato único: `{ status, codigo, mensagem, detalhes }`.

## Banco de dados

Principais tabelas: `usuario`, `insumo`, `fornecedor`, `lote_insumo`, `receita`, `receita_item`, `batelada`,
`batelada_consumo`, `movimentacao_estoque`, `refresh_token` e `token_usuario`. O estoque é controlado por lote, e o
saldo de um lote nunca fica negativo (restrição CHECK no banco). O estorno de um cancelamento devolve a quantidade
ao lote de origem.

## Testes

```
cd backend
dotnet test
```

São 777 casos: unitários (FEFO, escala da receita, custo, alertas, perdas, cancelamento), de API (login, perfis e
matriz de permissões) e de concorrência e integração com PostgreSQL real via Testcontainers (32 casos, exigem o
Docker ativo). Para rodar só os que usam o banco, ou todos menos eles:

```
dotnet test --filter "Categoria=Docker"
dotnet test --filter "Categoria!=Docker"
```

Para gerar o relatório: `dotnet test --logger "trx;LogFileName=resultados.trx"`. O registro dos casos executados
e dos bugs encontrados está em `docs/`.

## Fluxo de trabalho com Git

- A branch `main` recebe alterações por Pull Request.
- Cada tarefa usa uma branch própria (`feature/api-27-finalizar-cancelar-producao`, `test/tst-7-...`).
- Commits no padrão `feat:`, `fix:`, `docs:` e `test:`, com o código do card no fim da mensagem.
- Antes de abrir o PR, atualize a branch com a `main` para evitar conflitos.
- As tarefas e os responsáveis estão organizados no Trello.

## Documentação

A documentação do projeto (concepção, pesquisa com usuários, especificação técnica, casos de teste e registro de
bugs) fica na pasta `docs/`.
