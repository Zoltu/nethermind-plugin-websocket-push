# FROM mcr.microsoft.com/dotnet/sdk:7.0 as builder
FROM mcr.microsoft.com/dotnet/sdk@sha256:e049e6a153619337ceb4edd040fb60a220d420414d41d6eb39708d6ce390bc7c as builder
WORKDIR /build/plugin
COPY plugin/*.csproj /build/plugin/
RUN dotnet restore
COPY plugin/source/ /build/plugin/source/
RUN dotnet build --configuration Release --output /output

FROM alpine:latest
COPY --from=builder /output/*.dll /nethermind/plugins/
COPY --from=builder /output/*.pdb /nethermind/plugins/
