using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Dapper;
using Elastic.Apm;
using Elastic.Apm.AspNetCore.DiagnosticListener;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

var config = new ProducerConfig
{
    BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_HOST"),
    ClientId = Dns.GetHostName(),
};

var topic = "messages";

await CreateTopicAsync(config, topic);

var dbPath = "/sqlite/messages.db";
if (File.Exists(dbPath)) 
    File.Delete(dbPath);

var connectionString = $"Data Source={dbPath};";
await CreateDbAsync(connectionString);

// subscribe to ASP.NET Core diagnostic events
Agent.Subscribe(
    new AspNetCoreDiagnosticSubscriber(), 
    new AspNetCoreErrorDiagnosticsSubscriber());

var app = builder.Build();

app.MapGet("/", (LinkGenerator linker) => 
    Results.Content(
    $"<p><a href=\"{linker.GetPathByName("index", values: null)}\">All messages</a></p><hr />" +
    $"<p>POST a message <pre>{{ \"value\": \"message\" }}</pre> to {linker.GetPathByName("create", values: null)}</p><hr />" +
    $"<p>POST multiple messages <pre>[{{ \"value\": \"message\" }}]</pre> to {linker.GetPathByName("bulk", values: null)}</p>", new MediaTypeHeaderValue("text/html")));

app.MapPost("/messages/create", async (Message message) =>
{
    using var producer = new ProducerBuilder<Null, string>(config).Build();
    await producer.ProduceAsync(topic, new Message<Null, string> { Value = message.Value });
    return Results.Ok();
}).WithName("create");

app.MapPost("/messages/bulk", async (List<Message> messages) =>
{
    using var producer = new ProducerBuilder<Null, string>(config).Build();
    foreach (var message in messages)
        await producer.ProduceAsync(topic, new Message<Null, string> { Value = message.Value });
    return Results.Ok();
}).WithName("bulk");

app.MapGet("/messages", async () =>
{
    await using var connection = new SqliteConnection(connectionString);
    var messages = connection.Query<string>("SELECT Value FROM Messages;");
    return messages;
}).WithName("index");

app.Run();

static async Task CreateTopicAsync(ClientConfig config, string topic)
{
    using var adminClient = new AdminClientBuilder(config).Build();
    try
    {
        Console.WriteLine($"Creating topic '{topic}'");

        await adminClient.CreateTopicsAsync(new List<TopicSpecification>
        {
            new()
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1
            }
        });

        Console.WriteLine($"Topic '{topic}' created");
    }
    catch (CreateTopicsException e)
    {
        if (e.Results[0].Error.Code != ErrorCode.TopicAlreadyExists)
        {
            Console.WriteLine($"An error occurred creating topic '{topic}': {e.Results[0].Error.Reason}");
            throw;
        }
    }
}

async Task CreateDbAsync(string connectionString)
{
    await using var connection = new SqliteConnection(connectionString);
    var table = await connection
        .QueryFirstOrDefaultAsync<string>("SELECT name FROM sqlite_master WHERE type='table' AND name = 'Messages';");
    if (string.IsNullOrEmpty(table))
        await connection.ExecuteAsync("Create Table Messages (Value VARCHAR(1000) NOT NULL);");
}

record Message(string Value);