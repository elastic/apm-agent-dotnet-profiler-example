# Profiler auto instrumentation with .NET APM agent example

This is an example repository to accompany the blog post for the first public beta release
of the profiler auto instrumentation for the .NET APM agent.

## Prerequisites

1. docker and [docker compose](https://docs.docker.com/compose/install/) are installed
2. An Elastic stack deployment is created on [Elastic cloud](https://cloud.elastic.co/).

## Getting started

1. Copy the APM server URL and secret token from the Elastic stack deployment, and populate in the .env file.
2. Run

   ```
   docker compose up -d
   ```
   to bring the services up.

3. Check that the services are running

   ```
   docker compose ps
   ```
4. Make a request to the ASP.NET Core API

   ```
   curl -XPOST http://localhost:5000/messages/bulk -H 'Content-Type: application/json' -d '
   [{"value":"message 1"},
   {"value":"message 2"},
   {"value":"message 3"},
   {"value":"message 4"},
   {"value":"message 5"}]
   '
   ```
5. Check that the messages were sent to Kafka by the API, and consumed from Kafka by the console application, by checking
   ```
   curl -XGET http://localhost:5000/messages
   ```
   which should yield an array of the sent messages.
6. Navigate to the APM UI in the Kibana instance that is part of the Elastic Stack deployment.
7. Observe that the transactions and spans are captured for the request, kafka, and sqlite operations

