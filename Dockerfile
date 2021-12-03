# FROM mcr.microsoft.com/dotnet/sdk:6.0 as builder
FROM mcr.microsoft.com/dotnet/sdk@sha256:ca4344774139fabfb58eed70381710c8912900d92cf879019d2eb52abc307102 as builder
WORKDIR /build/plugin
COPY plugin/*.csproj /build/plugin/
COPY references/ /build/references/
RUN dotnet restore
COPY plugin/source/ /build/plugin/source/
RUN dotnet build --configuration Release --output /output

FROM alpine:latest
COPY --from=builder /output/*.dll /nethermind/plugins/
COPY --from=builder /output/*.pdb /nethermind/plugins/
