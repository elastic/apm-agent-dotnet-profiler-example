ARG AGENT_VERSION=1.12.1
ARG PROFILER_VERSION=1.12.1-beta.1

# pull down the profiler artifact zip and unzip
FROM alpine:latest AS zip
ARG AGENT_VERSION
ARG PROFILER_VERSION

WORKDIR /source

RUN apk add -U unzip && apk add -U curl
RUN curl -L -o elastic_apm_profiler.zip https://github.com/elastic/apm-agent-dotnet/releases/download/${AGENT_VERSION}/elastic_apm_profiler_${PROFILER_VERSION}.zip
RUN unzip elastic_apm_profiler.zip
RUN mkdir /elastic_apm_profiler && mv elastic_apm_profiler_${PROFILER_VERSION}/* /elastic_apm_profiler/

# compile the application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /source

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c release -o /app --no-restore

# pull in the profiler artifacts and compiled application
# set the profiler environment variables
FROM mcr.microsoft.com/dotnet/runtime:6.0
ARG AGENT_VERSION
ARG PROFILER_VERSION

WORKDIR /app
COPY --from=zip /elastic_apm_profiler /elastic_apm_profiler
COPY --from=build /app ./

ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={FA65FE15-F085-4681-9B20-95E04F6C03CC}
ENV CORECLR_PROFILER_PATH=/elastic_apm_profiler/libelastic_apm_profiler.so
ENV ELASTIC_APM_PROFILER_HOME=/elastic_apm_profiler
ENV ELASTIC_APM_PROFILER_INTEGRATIONS=/elastic_apm_profiler/integrations.yml
ENV ELASTIC_APM_PROFILER_LOG_DIR=/logs
ENV ELASTIC_APM_PROFILER_LOG=trace

ENTRYPOINT ["dotnet", "Consumer.dll"]
