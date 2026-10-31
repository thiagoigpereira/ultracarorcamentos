# Ultracar Orçamentos

API para sugerir serviços e peças a partir de diagnósticos de veículos e gerenciar orçamentos de oficinas. O projeto inclui controle de concorrência, histórico de alterações, envio idempotente e processamento de notificações por outbox.

## Tecnologias

- .NET 8 / ASP.NET Core
- Entity Framework Core 8
- PostgreSQL 16
- Docker Compose
- xUnit e Testcontainers para testes

## Estrutura

| Projeto | Responsabilidade |
|---|---|
| `src/Api` | API HTTP, controllers, Swagger e inicialização |
| `src/Application` | Serviços, interfaces e DTOs |
| `src/Domain` | Entidades, regras de negócio e exceções |
| `src/Infrastructure` | Persistência PostgreSQL, migrations, repositórios e outbox |
| `tests/UnitTests` | Testes unitários |
| `tests/IntegrationTests` | Testes de integração com PostgreSQL em container |

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) em execução
- Git

## Executar com Docker Compose

Na raiz do repositório, execute:

```powershell
$env:POSTGRES_PASSWORD = "escolha-uma-senha-local"
docker compose up --build -d
docker compose ps
```

Defina `POSTGRES_PASSWORD` no ambiente antes de iniciar; o Compose não inclui uma senha fixa no repositório. O Compose inicia o PostgreSQL e a API. A API aguarda o banco ficar saudável, aplica as migrations pendentes e insere dados de demonstração quando ainda não existem marcas cadastradas.

- API: <http://localhost:5000>
- Swagger: <http://localhost:5000/swagger>
- PostgreSQL: `localhost:5432`

Para acompanhar a inicialização:

```powershell
docker compose logs -f api
```

Para parar os serviços sem apagar o banco:

```powershell
docker compose down
```

Os dados do PostgreSQL ficam no volume `ultracar-postgres`. **Não use `docker compose down -v` se quiser preservá-los**, pois esse comando remove os volumes do projeto.

## Executar localmente

Configure a variável `ConnectionStrings__DefaultConnection` com uma connection string válida para PostgreSQL. Por exemplo, com o banco do Compose em execução:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=ultracar_db;Username=postgres;Password=<senha-configurada-no-compose>"
dotnet run --project src\Api\Api.csproj
```

A aplicação aplica as migrations e inicializa os dados de demonstração durante a inicialização. Em ambiente `Development`, o Swagger fica disponível em `/swagger`.

## Decisões de arquitetura

- **Separação por responsabilidades:** Domain concentra entidades e regras; Application coordena os casos de uso; Infrastructure implementa persistência e outbox; Api expõe os endpoints HTTP.
- **PostgreSQL como coordenador de concorrência:** estado, chaves de idempotência e eventos de outbox são persistidos no mesmo banco compartilhado pelas instâncias da API. A correção não depende de estado local de processo.
- **Concorrência otimista:** o orçamento possui uma versão (`RowVersion`) devolvida como `ETag`. Edições e envios usam atualizações condicionais no PostgreSQL; o banco só efetiva a operação se a oficina, o status e a versão ainda corresponderem ao estado esperado.
- **Restrições no banco como última linha de defesa:** há índice único para impedir mais de um orçamento ativo por oficina e ordem de serviço, chave primária composta para idempotência (`OficinaId`, `Chave`) e índice único para a chave de deduplicação do outbox.
- **Transação e outbox:** o envio grava a mudança de estado, histórico, resposta idempotente e evento de outbox na mesma transação. Assim, a transação não confirma o envio sem persistir também os dados necessários ao processamento posterior.
- **Claims do outbox:** workers concorrentes usam `FOR UPDATE SKIP LOCKED` e leases persistidos com expiração para reivindicar mensagens sem sobreposição; uma mensagem abandonada pode ser reivindicada após o lease expirar.
- **Entrega de notificações:** o worker passa uma chave estável de deduplicação ao serviço de notificação. O serviço de exemplo apenas registra a notificação em log; uma integração externa deve aceitar essa chave e deduplicar chamadas, pois a entrega é at-least-once, não exatamente uma vez.
- **Histórico auditável:** edições, transições e tentativas rejeitadas registram usuário quando aplicável, horário, canal e detalhes do conflito/decisão.

## Concorrência — resposta ao requisito

Duplicidade pode surgir de um duplo clique ou de várias abas/dispositivos; de retries automáticos após timeout ou perda da resposta, mesmo quando a primeira chamada já foi processada; de duas instâncias da API atendendo simultaneamente a mesma operação; e da repetição de um evento durante o processamento de uma notificação. Desabilitar um botão no front-end melhora a experiência, mas não cobre retries, outros clientes nem múltiplas instâncias. `lock` e `SemaphoreSlim` protegem somente dentro de um processo e, portanto, não são mecanismos de consistência distribuída.

O fluxo de envio aplica as seguintes camadas:

1. **Idempotência HTTP:** o cliente envia `Idempotency-Key`. A API armazena a chave com oficina, rota, hash do corpo, status e resposta original. Um retry com a mesma chave e o mesmo conteúdo recebe a resposta original; reutilizar a chave com outro conteúdo é rejeitado.
2. **Concorrência otimista:** o cliente envia o `ETag` atual em `If-Match`. Edição e envio executam atualizações condicionais no PostgreSQL, exigindo que a oficina, o status e a versão ainda sejam os esperados. Entre chamadas concorrentes com a mesma versão, apenas uma alteração é aceita; as demais recebem falha de precondição ou conflito.
3. **Atomicidade:** a transição, o histórico, o registro idempotente e o evento de outbox são gravados na mesma transação. Uma falha antes do commit não deve deixar somente parte do envio persistida.
4. **Restrições compartilhadas entre instâncias:** índices e chaves únicos no PostgreSQL evitam duplicar orçamento ativo para a mesma ordem de serviço, registro de idempotência para a mesma oficina/chave e evento de outbox para a mesma versão do orçamento. Essas garantias permanecem válidas independentemente de qual instância recebe a requisição.
5. **Claims e deduplicação na notificação:** workers usam lease persistido e claim transacional para não processar simultaneamente a mesma mensagem. Após falha/crash e expiração do lease, a mensagem pode ser tentada novamente. O consumidor recebe a chave estável `{orcamentoId}:{versao}` e o adaptador externo deve usá-la como idempotency key. A implementação atual usa um serviço de log, não um provedor externo.

O front-end pode desabilitar o botão enquanto a requisição está em andamento, mas isso é apenas uma melhoria de UX; as garantias de consistência ficam na API e no PostgreSQL. Os testes de integração cobrem envios e edições concorrentes, decisão pública concorrente, criação simultânea para a mesma OS, claims concorrentes do outbox e reenvio idempotente.

## Endpoints

### Sugestões e cadastros

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/v1/orcamentos/sugerir` | Gera sugestões de serviços e peças por veículo e diagnósticos |
| `POST` | `/api/v1/sugestoes` | Rota alternativa para gerar sugestões |
| `GET` | `/api/v1/diagnosticos?busca={termo}` | Lista diagnósticos, opcionalmente filtrados |
| `GET` | `/api/v1/veiculos/{placa}` | Busca veículo da oficina indicada no header `X-Oficina-Id` |

Exemplo de sugestão usando os dados de demonstração:

```powershell
curl.exe -X POST http://localhost:5000/api/v1/orcamentos/sugerir `
  -H "Content-Type: application/json" `
  -d "{\"veiculoId\":\"11111111-1111-1111-1111-111111111111\",\"diagnosticoIds\":[\"22222222-2222-2222-2222-111111111111\"]}"
```

O seed de demonstração contém um Volkswagen Gol 1.0 2019, placa `ABC1D23`, e um diagnóstico de ruído na frenagem. O catálogo de demonstração inclui um serviço de substituição de pastilhas e uma peça compatível.

### Orçamentos

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/v1/orcamentos` | Cria um orçamento |
| `POST` | `/api/v1/ordens-servico/{osId}/orcamento` | Obtém ou cria o orçamento de uma ordem de serviço; requer `X-Oficina-Id` |
| `GET` | `/api/v1/orcamentos/{id}` | Consulta o orçamento |
| `PUT` | `/api/v1/orcamentos/{id}/itens` | Atualiza os itens; requer `X-Oficina-Id`, `X-User-Id` e `If-Match` com o ETag recebido |
| `POST` | `/api/v1/orcamentos/{id}/envio` | Envia o orçamento; requer os headers descritos abaixo |
| `POST` | `/api/v1/orcamentos/{id}/revisao` | Cria uma revisão; requer `X-User-Id` |
| `GET` | `/api/v1/orcamentos/{id}/historico` | Consulta o histórico do orçamento |

O ETag retornado na criação ou consulta deve ser enviado, entre aspas, no header `If-Match` ao alterar itens ou enviar o orçamento. Uma versão desatualizada resulta em `412 Precondition Failed` e uma entrada de auditoria é gravada.

Os headers `X-Oficina-Id` e `X-User-Id` usam identificadores numéricos positivos. O `X-Oficina-Id` também delimita a edição de itens; um orçamento de outra oficina não é exposto pelo endpoint.

O envio requer:

- `X-Oficina-Id`: identificador da oficina;
- `If-Match`: ETag atual do orçamento;
- `Idempotency-Key`: chave única da operação para permitir reenvio seguro;
- `X-User-Id`: identificador do usuário que realiza o envio.

Uma segunda tentativa concorrente de envio pode retornar `409 Conflict`. Repetir uma requisição bem-sucedida com a mesma chave de idempotência reproduz a resposta original sem criar outro evento de outbox.

### Área pública do cliente

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/public/v1/orcamentos/{token}` | Consulta um orçamento pelo token público |
| `POST` | `/public/v1/orcamentos/{token}/decisao` | Registra a decisão do cliente |

Links públicos expirados retornam `410 Gone`; tokens inexistentes retornam `404 Not Found`.

## Contrato para o front

O protótipo de tela fornecido pela Ultracar deve orientar a apresentação visual e a navegação. **Não é necessário construir o front-end neste desafio.** Esta seção descreve como uma tela pode consumir a API existente; os nomes JSON abaixo usam `camelCase`.

### Fluxo sugerido para a tela da oficina

1. Pesquise o veículo por placa usando `GET /api/v1/veiculos/{placa}` e envie `X-Oficina-Id`. A resposta fornece `id`, `placa`, `marca`, `modelo` e `ano`.
2. Liste diagnósticos por `GET /api/v1/diagnosticos?busca={termo}` e permita selecionar um ou mais.
3. Peça sugestões com `POST /api/v1/orcamentos/sugerir`, enviando o `veiculoId` e os `diagnosticoIds` selecionados.
4. Crie ou recupere o orçamento da ordem de serviço com `POST /api/v1/ordens-servico/{osId}/orcamento`, enviando `X-Oficina-Id`. Guarde o `id` e o ETag recebido nos headers.
5. Use os serviços e peças selecionados nas sugestões para montar a lista completa de itens do orçamento. Relacione uma peça ao serviço correspondente por `itemPaiId`; se houver `pecaPendente`, resolva a pendência antes do envio.
6. Edite os itens com `PUT /api/v1/orcamentos/{id}/itens`, enviando `X-Oficina-Id`, `X-User-Id` numérico e o ETag atual em `If-Match`. Após sucesso, substitua-o pelo novo ETag da resposta.
7. Envie o orçamento com `POST /api/v1/orcamentos/{id}/envio`. Em caso de sucesso, apresente a confirmação e o link público retornado.

Para a experiência de tela sugerida no desafio, o front pode consultar diagnósticos com debounce de 300 ms, atualizar sugestões quando a seleção mudar e salvar o rascunho automaticamente a cada 10 segundos ou ao sair do campo. Consulte `GET /api/v1/orcamentos/{id}` a cada 15 segundos (ou use SignalR, se vier a ser implementado) para detectar alterações feitas por outra pessoa. Guarde o ETag retornado ao consultar o orçamento e envie-o em `If-Match` nas operações de edição e envio que o aceitam; após cada resposta bem-sucedida que contenha ETag, atualize o valor armazenado. A rota de revisão atual não recebe `If-Match`.

Gere uma `Idempotency-Key` UUID v4 por intenção/clique de envio e preserve a mesma chave e o mesmo corpo em retries. Em timeout ou erro transitório `5xx`, o comportamento de referência é tentar novamente até três vezes, com backoff de 1, 2 e 4 segundos. O botão desabilitado durante o envio é somente uma proteção de UX, não substitui a idempotência e a concorrência no servidor.

Estados de tela sugeridos: `Rascunho` (editável), `Enviando` (indicador de progresso), `Enviado` (somente leitura, aguardando decisão), `AprovadoTotal`, `AprovadoParcial` ou `Recusado` (somente leitura, decisões visíveis) e `Expirado` (oferecer revisão e novo envio explícito). Na visão pública, agrupe peças pelo serviço pai e faça a decisão da peça acompanhar a do serviço.

### Payloads e respostas principais

**Sugestão — `POST /api/v1/orcamentos/sugerir`:**

```json
{
  "veiculoId": "11111111-1111-1111-1111-111111111111",
  "diagnosticoIds": ["22222222-2222-2222-2222-111111111111"]
}
```

A resposta contém `veiculo` (`id`, `marca`, `modelo`, `ano`) e `servicos`. Cada serviço inclui `servicoId`, `descricao`, `origemDiagnosticoId`, `tempoHoras`, `valorMaoDeObra` e `pecas`. Cada tipo de peça informa `tipoPeca`, `quantidade`, `pecaPendente` e `opcoes`; cada opção traz `pecaId`, `codigo`, `descricao`, `preco` e `preferencial`.

**Criação/consulta de orçamento:** `POST /api/v1/ordens-servico/{osId}/orcamento` aceita `X-Oficina-Id` e este corpo. O header define a oficina usada para localizar/criar o orçamento e deve corresponder a `oficinaId`:

```json
{
  "oficinaId": 42,
  "ordemServicoId": 123,
  "veiculoId": "11111111-1111-1111-1111-111111111111",
  "clienteId": "33333333-3333-3333-3333-333333333333"
}
```

Retorna `201 Created` se criou ou `200 OK` se já havia um orçamento ativo. A representação inclui `id`, `oficinaId`, `ordemServicoId`, `veiculoId`, `clienteId`, `status`, `versao`, `total`, `desconto`, `descontoPercentual`, `valorAprovado`, `rowVersionBase64` e `itens`. Cada item inclui `id`, `tipo`, `referenciaId`, `itemPaiId`, `descricao`, `quantidade`, `valorUnitario`, `valorTotal`, `pecaPendente` e `decisao`. O ETag é retornado também no header HTTP `ETag`.

**Atualização de itens — `PUT /api/v1/orcamentos/{id}/itens`:**

Envie `X-Oficina-Id`, `X-User-Id` numérico, o ETag atual em `If-Match` e o conjunto completo de itens a persistir. O desconto pode ser fixo ou percentual:

```json
{
  "itens": [
    {
      "tipo": "Servico",
      "referenciaId": "44444444-4444-4444-4444-444444444444",
      "itemPaiId": null,
      "descricao": "Substituição de pastilhas dianteiras",
      "quantidade": 1,
      "valorUnitario": 150.00,
      "pecaPendente": false
    }
  ],
  "desconto": 10,
  "descontoPercentual": true
}
```

Em `200 OK`, a API retorna o orçamento atualizado e o novo ETag. `descontoPercentual: true` interpreta `desconto` como uma porcentagem de 0 a 100; `false` interpreta o valor como quantia fixa. Se outra requisição alterou o orçamento, a API retorna `412 Precondition Failed` (`ORC_VERSAO_DESATUALIZADA`) e registra a tentativa no histórico; recarregue os dados antes de permitir nova tentativa. A API substitui a lista de itens pelo conteúdo enviado, então o front deve enviar a lista completa pretendida, não apenas os itens alterados.

**Envio — `POST /api/v1/orcamentos/{id}/envio`:**

Envie `X-Oficina-Id`, `X-User-Id` numérico, `If-Match` e `Idempotency-Key`, além do corpo:

```json
{
  "canal": "Email",
  "mensagem": "Seu orçamento está disponível para avaliação."
}
```

Em `200 OK`, a resposta contém `status`, `enviadoPor`, `enviadoEm` e `linkCliente`. O front pode exibir a confirmação e compartilhar o link. Se houver timeout ou a resposta se perder, repita **a mesma operação com a mesma chave e o mesmo corpo**; gere outra chave somente para uma nova intenção de envio.

O identificador numérico informado em `X-User-Id` é retornado como `enviadoPor.id` e registrado no histórico. Como não há integração com cadastro de usuários, o nome é apresentado no formato `Usuário {id}`.

### Respostas que a tela deve tratar

| HTTP | Significado | Comportamento sugerido |
|---|---|---|
| `200 OK` | Consulta, atualização ou operação concluída | Atualize a tela com os dados retornados e guarde o ETag novo quando disponível |
| `201 Created` | Orçamento criado | Guarde o `id` e o ETag para as próximas operações |
| `400 Bad Request` | Corpo inválido ou regra de domínio em endpoints que retornam 400 | Mostre a mensagem recebida e permita corrigir a entrada |
| `404 Not Found` | Recurso não encontrado | Informe que o veículo/orçamento/link não foi encontrado |
| `409 Conflict` | Orçamento já enviado por outra chamada ou decisão já registrada | Recarregue o estado; não repita como uma nova ação automaticamente |
| `410 Gone` | Link público expirado | Informe que o link venceu e solicite um novo contato com a oficina |
| `412 Precondition Failed` | ETag desatualizado | Recarregue o orçamento antes de editar ou enviar |
| `422 Unprocessable Entity` | Operação rejeitada por regra de negócio, por exemplo peça pendente ou decisão inválida | Mostre `code` e `message`/`detail`, quando presentes |

O formato de erro pode variar entre endpoints: alguns retornam `{ "code": "...", "message": "..." }`; erros de domínio tratados pelo middleware podem incluir `code`, `detail`, `title`, `status` e `traceId`. A tela deve usar os campos disponíveis sem depender de um único formato de envelope.

Tratamento de conflito sugerido pelo desafio:

- **`409 ORC_JA_ENVIADO`:** informar que outra pessoa já enviou o orçamento, atualizar os dados e deixar a tela em modo de leitura. A resposta inclui o identificador/nome disponível e o horário do envio.
- **`412 ORC_VERSAO_DESATUALIZADA`:** avisar que outra pessoa alterou o orçamento e oferecer recarregar ou comparar versões. Se as alterações precisarem ser aplicadas após o envio, use a ação explícita de revisão e envie uma nova versão.
- **`422 ORC_PECA_PENDENTE`:** destacar os itens sem peça definida e direcionar o usuário ao primeiro item pendente antes de permitir nova tentativa de envio.

Tentativas rejeitadas de edição, envio e decisão pública são registradas no histórico com horário, canal, usuário quando disponível e motivo. A API atual não oferece presença de edição nem SignalR; o polling descrito acima é uma orientação para o front, não um recurso já fornecido pelo servidor.

### Contrato da área pública do cliente

`GET /public/v1/orcamentos/{token}` retorna `id`, `status`, `versao`, `total`, `valorAprovado`, `motivoRecusa` e `itens`, contendo `id`, `tipo`, `descricao`, `quantidade`, `valorUnitario`, `valorTotal`, `itemPaiId` e `decisao`.

Para registrar uma decisão, use `POST /public/v1/orcamentos/{token}/decisao`:

```json
{
  "tipo": "Parcial",
  "itens": [
    {
      "itemId": "44444444-4444-4444-4444-444444444444",
      "decisao": "Aprovado"
    }
  ],
  "motivoRecusa": null
}
```

`tipo` aceita `Total`, `Recusa` ou `Parcial`; em decisão parcial, envie a decisão (`Aprovado` ou `Recusado`) para cada item. Em `Recusa`, `motivoRecusa` é persistido e devolvido na consulta pública quando fornecido. As decisões aceitas e rejeitadas são registradas no histórico. Sucesso retorna `sucesso: true` e `statusFinal`. Link expirado resulta em `410`; decisão já registrada, em `409`; outras decisões inválidas, em `422`.

## Testes

Execute todos os testes da solução:

```powershell
dotnet test UltracarOrcamentos.slnx
```

Os testes de integração usam Testcontainers e precisam do Docker em execução. Para rodar cada conjunto separadamente:

```powershell
dotnet test tests\UnitTests\UnitTests.csproj
dotnet test tests\IntegrationTests\IntegrationTests.csproj
```

Para coletar cobertura nos testes unitários:

```powershell
dotnet test tests\UnitTests\UnitTests.csproj --collect:"XPlat Code Coverage"
```

Os testes de integração exercitam concorrência real contra PostgreSQL: criação por OS, edição com ETag, envio, decisão pública e claim de outbox, além do replay idempotente.

## Migrations

As migrations são aplicadas automaticamente quando a API inicia. Para verificar se o modelo atual diverge do snapshot, configure `ConnectionStrings__DefaultConnection` e execute:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project src\Infrastructure\Infrastructure.csproj `
  --startup-project src\Api\Api.csproj
```
