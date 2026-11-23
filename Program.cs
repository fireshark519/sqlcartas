using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();

string Base(HttpRequest r)
{
    var local = r.Host.Host == "localhost" || r.Host.Host == "127.0.0.1";
    return $"{(local ? r.Scheme : "https")}://{r.Host}";
}

// Lê o activityID da query string ou do corpo (JSON ou form)
async Task<string> GetActivityId(HttpRequest r)
{
    if (r.Query.TryGetValue("activityID", out var q) && q.Count > 0) return q.ToString();
    try
    {
        if (r.HasFormContentType)
        {
            var f = await r.ReadFormAsync();
            if (f.TryGetValue("activityID", out var v)) return v.ToString();
        }
        else if (r.ContentLength > 0)
        {
            using var doc = await JsonDocument.ParseAsync(r.Body);
            if (doc.RootElement.TryGetProperty("activityID", out var p)) return p.ToString();
        }
    }
    catch { }
    return "12345";
}

// 1. config_url
app.MapGet("/sqlcartas/configuracao.html", () => Results.Content(@"<!DOCTYPE html>
<html lang='pt'><head><meta charset='utf-8'><title>SQL Cartas: configuração</title></head>
<body>
<h2>SQL Cartas: configuração</h2>
<form>
  <label>Nível de dificuldade
    <select name='nivel_dificuldade'>
      <option value='basico'>Básico</option>
      <option value='intermedio'>Intermédio</option>
      <option value='avancado'>Avançado</option>
    </select>
  </label><br>
  <label><input type='checkbox' name='permitir_dicas' value='true' checked> Permitir dicas</label><br>
  <label>Máximo de tentativas (0 = ilimitado)
    <input type='number' name='max_tentativas' value='0' min='0'></label><br>
  <input type='hidden' name='desafios' value='1,2,3'>
  <p>Desafios de exemplo: 1, 2, 3</p>
</form>
</body></html>", "text/html; charset=utf-8"));

// 2. json_params_url
app.MapGet("/sqlcartas/json-params", () => Results.Json(new object[] {
    new { name = "desafios", type = "text/plain" },
    new { name = "nivel_dificuldade", type = "text/plain" },
    new { name = "permitir_dicas", type = "boolean" },
    new { name = "max_tentativas", type = "integer" }
}));

// 3. user_url (deploy): aceita GET e POST
app.MapMethods("/sqlcartas/deploy", new[] { "GET", "POST" }, async (HttpRequest r) =>
{
    var id = await GetActivityId(r);
    return Results.Json($"{Base(r)}/sqlcartas/{id}");
});

// 4. analytics_list_url
app.MapGet("/sqlcartas/lista-analytics", () => Results.Json(new
{
    qualAnalytics = new object[] {
        new { name = "Histórico de queries", type = "URL" }
    },
    quantAnalytics = new object[] {
        new { name = "Acedeu à atividade", type = "boolean" },
        new { name = "Desafios resolvidos", type = "integer" },
        new { name = "Queries executadas", type = "integer" },
        new { name = "Queries com erro", type = "integer" },
        new { name = "Dicas pedidas", type = "integer" },
        new { name = "Tempo na atividade (min)", type = "integer" }
    }
}));

// 5. analytics_url (POST)
app.MapPost("/sqlcartas/analytics", async (HttpRequest r) =>
{
    var id = await GetActivityId(r);
    return Results.Json(new object[] {
        new {
            inveniraStdID = "1001",
            quantAnalytics = new object[] {
                new { name = "Acedeu à atividade", type = "boolean", value = true },
                new { name = "Desafios resolvidos", type = "integer", value = 4 },
                new { name = "Queries executadas", type = "integer", value = 17 },
                new { name = "Queries com erro", type = "integer", value = 6 },
                new { name = "Dicas pedidas", type = "integer", value = 2 },
                new { name = "Tempo na atividade (min)", type = "integer", value = 38 }
            },
            qualAnalytics = new object[] {
                new { name = "Histórico de queries", type = "URL",
                      value = $"{Base(r)}/sqlcartas/{id}/historico/1001" }
            }
        }
    });
});

app.MapGet("/", () => "SQL Cartas Activity Provider");
app.Run();