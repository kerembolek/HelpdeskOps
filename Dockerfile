# HelpdeskOps container image: builds the app with the .NET SDK, then runs it on the smaller ASP.NET runtime image.
# docker-compose.yml runs it together with SQL Server; see docs/INSTALL_TR.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so a code change does not download the packages again
COPY HelpdeskOps/HelpdeskOps.csproj HelpdeskOps/
RUN dotnet restore HelpdeskOps/HelpdeskOps.csproj

COPY HelpdeskOps/ HelpdeskOps/
RUN dotnet publish HelpdeskOps/HelpdeskOps.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0

# ping for the device status check and the OpenLDAP library for Active Directory sign-in, which the base image lacks;
# the zimmet form PDFs use QuestPDF's own fonts. The library's package name differs between distribution releases.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends iputils-ping \
    && (apt-get install --yes --no-install-recommends libldap2 || apt-get install --yes --no-install-recommends libldap-2.5-0) \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Attachments, data protection keys, and the mail outbox live in App_Data; mount it as a volume so they survive updates
RUN mkdir -p /app/App_Data && chown "$APP_UID" /app/App_Data
VOLUME /app/App_Data

# The image's unprivileged user, not root
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HelpdeskOps.dll"]
