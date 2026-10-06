# Cliente REST Genérico em .NET 9

Biblioteca em C# para consumir APIs REST com **autenticação configurável**, **rate limiting** e **retry com backoff**, construída sobre `HttpClient` + `IHttpClientFactory` + `DelegatingHandler`. Todo o comportamento é controlado por um `appsettings.json`, sem reescrever código.

| Funcionalidade | Detalhes |
|---|---|
| Autenticação | API Key (header ou query string), Bearer (token fixo), OAuth 2.0 client credentials com renovação automática |
| Rate limit | Janela deslizante configurável (ex.: 60 req/min), válida também para chamadas concorrentes |
| Retry | HTTP 408/429/5xx e erros de rede, com backoff exponencial, jitter opcional e suporte a `Retry-After` |
| Interface | `IRestClient` com `GetAsync`, `PostAsync`, `PutAsync` e `DeleteAsync` |
| Configuração | JSON externo (`IOptions<ApiClientOptions>`), validada na inicialização |

---

## 1. Pré-requisitos

| Ferramenta | Link | Verificar |
|---|---|---|
| Git | https://git-scm.com/downloads | `git --version` |
| **.NET 9 SDK** | https://dotnet.microsoft.com/download/dotnet/9.0 (Windows: `winget install Microsoft.DotNet.SDK.9`) | `dotnet --list-sdks` (deve listar `9.0.x`) |
| VS Code (opcional) | https://code.visualstudio.com + extensão **C# Dev Kit** | |

> Instale o **SDK 9** especificamente. O projeto mira `net9.0`, e um SDK mais novo sozinho não traz o runtime 9.
> É necessário acesso à internet (pacotes NuGet e a API pública de exemplo).

---

## 2. Baixar, compilar e executar

```bash
git clone <URL-DO-REPOSITORIO>
cd <PASTA-DO-REPOSITORIO>

dotnet restore
dotnet build
dotnet run --project samples/SampleApp
dotnet test
```

Todos os comandos deste documento devem ser executados na **pasta raiz** (onde está o arquivo `.sln`).

### O que o `dotnet run` faz
O app de exemplo chama a API pública `https://jsonplaceholder.typicode.com/` e executa, em ordem:

| Etapa | O que demonstra |
|---|---|
| 1. GET `posts/1` | Leitura e desserialização do JSON |
| 2. POST `posts` | Envio de JSON no corpo (a API só simula a criação) |
| 3. PUT `posts/1` | Atualização |
| 4. DELETE `posts/1` | Remoção |
| 5. GET `posts/999999` | Erro 404 tratado (`RestClientException`), sem retry: é uma falha permanente |
| 6. 10 chamadas simultâneas | Todas passam pelo mesmo controle de rate limit |

A chave de API do `appsettings.json` é enviada automaticamente no header `x-api-key` em todas as chamadas. O programa termina com código de saída 0.

---

## 3. Testes automatizados

```bash
dotnet test                                          
dotnet test --logger "console;verbosity=detailed"        
dotnet test --filter "FullyQualifiedName~RetryHandler"   
dotnet test --filter "FullyQualifiedName~RateLimiter"    
dotnet test --filter "FullyQualifiedName~AuthProvider"   
dotnet test --filter "FullyQualifiedName~Validator"  
```

Os testes não dependem de internet: usam um servidor HTTP falso dentro do próprio código e um relógio falso (`FakeTimeProvider`), sem esperas reais.

| Grupo | O que comprovam |
|---|---|
| Retry | Reenvia em 503, 429 e falha de rede; para após `MaxRetries`; **não** reenvia 404; respeita `Retry-After`; backoff dobra (500 → 1000 → 2000 ms); não faz nada se desligado |
| Rate limit | Bloqueia ao atingir o limite; com chamadas concorrentes nunca excede o limite; libera após a janela; respeita cancelamento |
| Autenticação | API Key em header e em query string (idempotente); Bearer; OAuth2 com cache do token, renovação antes de expirar e **uma única** requisição de token com 10 chamadas simultâneas |
| Validação | Tipo inválido; campos obrigatórios por tipo; campos desligados não são exigidos; URL inválida |

No VS Code, também é possível abrir o painel **Testing** (ícone de tubo de ensaio) e executar cada teste com um clique.

---

## 4. Testar as funcionalidades pelo terminal

Os argumentos depois do `--` **sobrescrevem o `appsettings.json`**, então nenhum arquivo precisa ser editado.

### 4.1 Autenticação

```bash

dotnet run --project samples/SampleApp

dotnet run --project samples/SampleApp -- --ApiClient:Authentication:ApiKeyHeader=

dotnet run --project samples/SampleApp -- --ApiClient:Authentication:ApiKeyHeader= --ApiClient:Authentication:ApiKeyQueryParameter=key

dotnet run --project samples/SampleApp -- --ApiClient:Authentication:Type=Bearer --ApiClient:Authentication:BearerToken=meu-token
```

A API pública de exemplo ignora credenciais, então esses comandos provam que **a estratégia troca só pela configuração**, sem recompilar. A verificação do conteúdo exato dos headers e da query string, e o fluxo **OAuth2**, estão nos testes (`--filter "FullyQualifiedName~AuthProvider"`).

### 4.2 Rate limit

O app faz 15 chamadas no total (5 nas etapas 1 a 5 e 10 na etapa 6).

```bash

dotnet run --project samples/SampleApp -- --ApiClient:RateLimit:RequestsPerMinute=12


dotnet run --project samples/SampleApp -- --ApiClient:RateLimit:Enabled=false
```

Observe a coluna de milissegundos impressa na etapa 6.

### 4.3 Retry com backoff

Aponte o cliente para uma porta onde não há servidor. A falha de conexão é tratada como falha transitória e o retry entra em ação:

```bash

dotnet run --project samples/SampleApp -- --ApiClient:BaseUrl=http://127.0.0.1:5999/

dotnet run --project samples/SampleApp -- --ApiClient:BaseUrl=http://127.0.0.1:5999/ --ApiClient:Retry:UseJitter=false

dotnet run --project samples/SampleApp -- --ApiClient:BaseUrl=http://127.0.0.1:5999/ --ApiClient:Retry:UseExponentialBackoff=false --ApiClient:Retry:UseJitter=false


dotnet run --project samples/SampleApp -- --ApiClient:BaseUrl=http://127.0.0.1:5999/ --ApiClient:Retry:MaxRetries=1


dotnet run --project samples/SampleApp -- --ApiClient:BaseUrl=http://127.0.0.1:5999/ --ApiClient:Retry:Enabled=false
```

**Esperado:** linhas como `Tentativa 1/4 falhou (HttpRequestException). Nova tentativa em ... ms.` e, ao final, o app **termina com `HttpRequestException`**. Isso é o comportamento correto: o exemplo só trata o erro 404.

Falha de DNS (também transitória):
```bash
dotnet run --project samples/SampleApp -- --ApiClient:BaseUrl=https://host-que-nao-existe.invalid/ --ApiClient:Retry:UseJitter=false
```

O retry de **429 e 5xx**, o limite de tentativas, o header `Retry-After` e a não repetição de 404 são comprovados por `dotnet test --filter "FullyQualifiedName~RetryHandler"`.

### 4.4 Validação da configuração

Cada comando abaixo deve falhar **imediatamente**, sem fazer nenhuma chamada HTTP, imprimindo `Configuração inválida no appsettings.json:`, a lista de problemas e código de saída 1:

```bash
dotnet run --project samples/SampleApp -- --ApiClient:Authentication:Type=Basic     
dotnet run --project samples/SampleApp -- --ApiClient:Authentication:Type=Bearer      
dotnet run --project samples/SampleApp -- --ApiClient:Authentication:Type=OAuth2      
dotnet run --project samples/SampleApp -- --ApiClient:Authentication:ApiKey=           
dotnet run --project samples/SampleApp -- --ApiClient:BaseUrl=isso-nao-e-url           
dotnet run --project samples/SampleApp -- --ApiClient:RateLimit:RequestsPerMinute=0   
dotnet run --project samples/SampleApp -- --ApiClient:Retry:MaxRetries=0              
dotnet run --project samples/SampleApp -- --ApiClient:Retry:MaxDelayMilliseconds=100   
dotnet run --project samples/SampleApp -- --ApiClient:TimeoutSeconds=0                 
```

Ver o código de saída: PowerShell `$LASTEXITCODE`, cmd `echo %errorlevel%`, Linux/macOS `echo $?`.

Campos de funcionalidades **desligadas** não são exigidos (deve rodar normalmente):
```bash
dotnet run --project samples/SampleApp -- --ApiClient:Retry:Enabled=false --ApiClient:Retry:MaxRetries=0
```

### 4.5 Configuração por variável de ambiente

Alternativa aos argumentos. Use `__` (dois underscores) no lugar de `:`.

```bash
# PowerShell
$env:ApiClient__RateLimit__RequestsPerMinute="12"; dotnet run --project samples/SampleApp
# cmd
set ApiClient__RateLimit__RequestsPerMinute=12 && dotnet run --project samples/SampleApp
# Linux / macOS
ApiClient__RateLimit__RequestsPerMinute=12 dotnet run --project samples/SampleApp
```

---

## 5. Outros comandos úteis

```bash
dotnet build -c Release                                 
dotnet run --project samples/SampleApp -c Release         
dotnet watch --project samples/SampleApp run              
dotnet clean                                             
dotnet publish samples/SampleApp -c Release -o publish     
dotnet publish/SampleApp.dll                               
dotnet sln list                                            
dotnet list package                                        
dotnet --info                                            
```

### Usando o VS Code
1. Instale a extensão **C# Dev Kit**.
2. **File → Open Folder** e escolha a pasta do projeto.
3. Abra o terminal integrado com `` Ctrl+` `` e execute os comandos acima.
4. `F5` executa o app com o depurador; o painel **Testing** executa os testes.

---

## 6. Usar a biblioteca em outro projeto

```bash
dotnet new console -n MinhaApp
cd MinhaApp
dotnet add reference ../src/GenericRestClient/GenericRestClient.csproj
dotnet add package Microsoft.Extensions.Hosting
```

Copie o `appsettings.json` da raiz para o novo projeto (ajustando `BaseUrl` e `Authentication`) e use:

```csharp
builder.Services.AddRestClient(builder.Configuration);  


public class MeuServico(IRestClient api)
{
    public Task<UserDto?> Buscar(int id) => api.GetAsync<UserDto>($"users/{id}");
}
```

Também é possível configurar por código: `services.AddRestClient(o => { o.BaseUrl = "..."; ... });`

---

## 7. Configuração (`appsettings.json`)

```json
{
  "ApiClient": {
    "BaseUrl": "https://jsonplaceholder.typicode.com/",
    "TimeoutSeconds": 100,
    "Authentication": { "Type": "ApiKey", "ApiKey": "minha-chave-api", "ApiKeyHeader": "x-api-key" },
    "RateLimit": { "Enabled": true, "RequestsPerMinute": 60 },
    "Retry": {
      "Enabled": true,
      "MaxRetries": 3,
      "BaseDelayMilliseconds": 500,
      "UseExponentialBackoff": true,
      "UseJitter": true,
      "MaxDelayMilliseconds": 30000
    }
  }
}
```

| Campo | Descrição |
|---|---|
| `BaseUrl` | URL base (obrigatório). A barra final é adicionada automaticamente se faltar. |
| `TimeoutSeconds` | Timeout **total** da chamada, somando todas as tentativas (padrão 100). |
| `Authentication.Type` | `ApiKey`, `Bearer` ou `OAuth2` (sem diferenciar maiúsculas/minúsculas). |
| `Authentication.ApiKey` / `ApiKeyHeader` | Chave e nome do header. Sem `ApiKeyHeader`, vai na query string (parâmetro `ApiKeyQueryParameter`, padrão `api_key`). |
| `Authentication.BearerToken` | Token fixo (campo acrescentado: o enunciado não definia onde guardar o token do tipo Bearer). |
| `Authentication.ClientId` / `ClientSecret` / `TokenEndpoint` / `Scope` | OAuth2 client credentials. |
| `Authentication.TokenExpirationSkewSeconds` | Renova o token X segundos antes de expirar (padrão 60). |
| `RateLimit.Enabled` / `RequestsPerMinute` | Janela deslizante de 1 minuto. |
| `Retry.Enabled` / `MaxRetries` / `BaseDelayMilliseconds` / `UseExponentialBackoff` | Conforme o enunciado. |
| `Retry.UseJitter` / `MaxDelayMilliseconds` | Extras opcionais (padrões: `true` e `30000`). |

Exemplos de `Authentication` para os outros tipos:
```json
{ "Type": "Bearer", "BearerToken": "meu-token" }
{ "Type": "OAuth2", "ClientId": "id", "ClientSecret": "segredo", "TokenEndpoint": "https://auth.exemplo.com/oauth2/token" }
```

---

## 8. Estrutura do repositório

```
├── RestClientChallenge.sln
├── appsettings.json                      ← exemplo funcional de configuração
├── src/GenericRestClient/                ← a biblioteca
│   ├── Core/            IRestClient, RestClient, RestClientException
│   ├── Auth/            IAuthProvider + ApiKey / Bearer / OAuth2
│   ├── Handlers/        AuthenticationHandler, RateLimitHandler, RetryHandler
│   ├── RateLimiting/    IRateLimiter + SlidingWindowRateLimiter
│   ├── Configuration/   ApiClientOptions + validador
│   └── Extensions/      services.AddRestClient(...)
├── samples/SampleApp/                    ← app de demonstração (console)
└── tests/GenericRestClient.Tests/        ← testes automatizados (xUnit)
```

---

## 9. Arquitetura

```
IRestClient ──► HttpClient (IHttpClientFactory)
                  Request → RetryHandler → RateLimitHandler → AuthenticationHandler → Internet
```

A ordem segue o diagrama do enunciado. Com o **Retry por fora**, cada tentativa passa de novo pelo rate limit (retries também contam no limite) e pela autenticação (um token renovado é usado já na nova tentativa).

| Classe | Responsabilidade |
|---|---|
| `RestClient` | Montar a requisição, serializar/desserializar JSON, transformar erro HTTP em `RestClientException`. |
| `AuthenticationHandler` + `IAuthProvider` | Aplicar credenciais (`ApiKeyAuthProvider`, `BearerTokenProvider`, `OAuth2AuthProvider`). |
| `RateLimitHandler` + `IRateLimiter` | Esperar a vez antes de enviar. |
| `RetryHandler` | Reenviar em falhas transitórias. |
| `ApiClientOptionsValidator` | Validar a configuração na inicialização. |

### Autenticação
- **API Key:** header ou query string; operação idempotente (seguro reenviar a mesma requisição).
- **Bearer:** devolve o token da configuração.
- **OAuth2 (client credentials):** `POST` ao `TokenEndpoint` com `grant_type=client_credentials`, token guardado em memória e renovado 60 s antes de expirar. Um `SemaphoreSlim` com *double-check* garante uma única renovação sob concorrência. A busca do token usa um `HttpClient` nomeado, fora do pipeline principal, para evitar dependência circular.

### Rate limiting
`SlidingWindowRateLimiter` guarda o horário das últimas requisições e só libera a próxima se houver menos de N na janela de 1 minuto. Um `SemaphoreSlim` serializa os chamadores, então o limite vale também para chamadas concorrentes. O limiter é **singleton** e os handlers são **transient** (exigência do `IHttpClientFactory`), por isso o limite é compartilhado entre todas as instâncias de handler.

### Retry
- **Retenta:** HTTP 408, 429, 5xx, `HttpRequestException` (rede/DNS) e timeouts.
- **Não retenta:** 400, 401, 403, 404 etc. (falhas permanentes) nem cancelamento do usuário.
- **Delay:** `Base × 2^(tentativa-1)` (ou fixo), limitado a `MaxDelayMilliseconds`, com *equal jitter* opcional (50–100 % do valor).
- Respeita o header `Retry-After` quando ele pede espera maior que o delay calculado.
- O corpo da requisição é bufferizado antes do primeiro envio para poder ser reenviado.

---

## 10. Decisões de design e limitações

1. **Retry feito à mão, sem Polly.** O enunciado recomenda Polly, mas não o exige. Um `DelegatingHandler` pequeno deixa a lógica visível e didática e evita dependência extra. Trocar por Polly é simples: basta substituir o `RetryHandler`.
2. **`IAuthProvider.AuthenticateAsync(request)` em vez de `GetAccessTokenAsync()`.** A API Key pode ir na query string, então o provider precisa enxergar a requisição.
3. **Implementações `internal`.** O consumidor só vê `IRestClient`, `IAuthProvider`, `IRateLimiter`, `ApiClientOptions`, `RestClientException` e `AddRestClient`. Como os registros usam `TryAdd`, é possível registrar a **própria** implementação de `IAuthProvider` ou `IRateLimiter` antes de chamar `AddRestClient`.
4. **`TimeProvider`** (nativo do .NET 8+) em vez de `DateTime.Now`, o que permite testar expiração de token e janela de rate limit sem esperar.

**Limitações conhecidas:**
- Um `IRestClient` por container de DI. Para duas APIs com configurações diferentes na mesma aplicação, o próximo passo é usar *named options* + *named clients*.
- `TimeoutSeconds` cobre a chamada inteira (todas as tentativas); timeout por tentativa exigiria um handler adicional.
- Em resposta 401 com OAuth2, uma melhoria seria invalidar o token e tentar uma vez mais.
- Extensões previstas no enunciado (`PatchAsync`, headers customizados, multipart, resposta bruta) podem ser feitas via interfaces adicionais, sem alterar `IRestClient`.

---

## 11. Problemas comuns

| Sintoma | Causa / solução |
|---|---|
| `You must install or update .NET to run this application` | Instale o **SDK 9** (o projeto mira `net9.0`). |
| `Unable to find package ...` no `dotnet restore` | Sem acesso ao nuget.org (proxy ou firewall). |
| `Configuração inválida no appsettings.json` | Cada linha listada aponta o campo a corrigir. |
| `HttpRequestException` ao rodar o app | Sem internet, ou o `jsonplaceholder` fora do ar. Rode `dotnet test`, que não depende de rede. |
| `project not found` / `MSB1003` | O terminal não está na pasta raiz do projeto (use `cd`). |
| Terminou com exceção nos testes de retry (seção 4.3) | Esperado: o exemplo só trata o erro 404. |
