using System.Diagnostics;
using GenericRestClient.Core;
using GenericRestClient.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SampleApp;


var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});


builder.Services.AddRestClient(builder.Configuration);

using var host = builder.Build();

try
{
    await host.StartAsync(); 
}
catch (OptionsValidationException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Configuração inválida no appsettings.json:");
    foreach (var failure in ex.Failures)
        Console.WriteLine($"  - {failure}");
    Console.ResetColor();
    return 1;
}


var client = host.Services.GetRequiredService<IRestClient>();

Console.WriteLine("=== 1) GET ===");
var post = await client.GetAsync<Post>("posts/1");
Console.WriteLine($"  #{post?.Id}: {post?.Title}");

Console.WriteLine("\n=== 2) POST ===");
var created = await client.PostAsync<NewPost, Post>("posts", new NewPost("Meu título", "Meu corpo", 1));
Console.WriteLine($"  Criado com Id = {created?.Id} (a API de teste simula a criação)");

Console.WriteLine("\n=== 3) PUT ===");
var updated = await client.PutAsync<Post, Post>("posts/1", new Post(1, 1, "Título alterado", "Corpo alterado"));
Console.WriteLine($"  Atualizado: {updated?.Title}");

Console.WriteLine("\n=== 4) DELETE ===");
await client.DeleteAsync("posts/1");
Console.WriteLine("  Removido (sem exceção = sucesso)");

Console.WriteLine("\n=== 5) Falha permanente (404 não é reenviado) ===");
try
{
    await client.GetAsync<Post>("posts/999999");
}
catch (RestClientException ex)
{
    Console.WriteLine($"  Erro tratado: HTTP {(int?)ex.StatusCode} | transitório? {ex.IsTransient}");
}

Console.WriteLine("\n=== 6) 10 chamadas concorrentes (rate limit vale para todas) ===");
var sw = Stopwatch.StartNew();
var tasks = Enumerable.Range(1, 10).Select(async i =>
{
    var p = await client.GetAsync<Post>($"posts/{i}");
    Console.WriteLine($"  [{sw.ElapsedMilliseconds,5} ms] posts/{i} -> {Short(p?.Title)}");
});
await Task.WhenAll(tasks);
Console.WriteLine("  Dica: troque RequestsPerMinute para 5 no appsettings.json e rode de novo.");

await host.StopAsync();
return 0;

static string Short(string? text) =>
    text is null ? "(vazio)" : text.Length <= 30 ? text : text[..30] + "...";
