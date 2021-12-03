using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Dapper;
using Microsoft.Data.Sqlite;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

var dbPath = "/sqlite/messages.db";
var connectionString = $"Data Source={dbPath};";

await CreateDbAsync(connectionString);

var config = new ConsumerConfig
{
    BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_HOST"),
    GroupId = "consumer",
    AutoOffsetReset = AutoOffsetReset.Earliest
};

var topic = "messages";
await CreateTopicAsync(config, topic);
using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
consumer.Subscribe(topic);

while (!cts.IsCancellationRequested)
{
    try
    {
        var consumeResult = consumer.Consume(cts.Token);
        if (consumeResult.Message is not null)
        {
            var message = consumeResult.Message.Value;
            Console.WriteLine($"Received message '{message}' from Kafka");
            await using var connection = new SqliteConnection(connectionString);
            var result =
                await connection.ExecuteAsync("INSERT INTO Messages (Value) VALUES (@message);", new { message });
            Console.WriteLine($"Inserted message '{message}' into Sqlite, result '{result}");
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"Exception while consuming message from Kafka: {e}");
        throw;
    }
}

Console.WriteLine("Consume cancellation requested. Closing consumer");
consumer.Close();
Console.WriteLine("Finished");

static async Task CreateTopicAsync(ClientConfig config, string topic)
{
    using var adminClient = new AdminClientBuilder(config).Build();
    try
    {
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