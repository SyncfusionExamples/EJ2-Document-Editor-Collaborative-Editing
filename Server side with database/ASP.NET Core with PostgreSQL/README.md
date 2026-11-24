# Collaborative Editing with ASP.NET Core and PostgreSQL

This sample shows how to use PostgreSQL as the store for real-time collaborative editing in the Syncfusion EJ2 Document Editor. The ASP.NET Core server uses SignalR to broadcast edits and the [Npgsql](https://www.npgsql.org/) provider to persist them.

## How it works

1. A client opens a document and calls `api/CollaborativeEditing/ImportFile` with a room name.
2. The server creates one table per room, named after the room. Its columns are `version SERIAL PRIMARY KEY`, `operation TEXT` and `clientVersion INTEGER`. Each edit made in the editor is stored as a row.
3. The server also keeps a shared `de_version_info` table (`roomName TEXT`, `lastSavedVersion INTEGER`). It records the last version that was saved to the source document.
4. Each edit is sent to `api/CollaborativeEditing/UpdateAction`. The server inserts the edit, transforms it against the edits that other users made in the meantime, and updates the stored row. It then broadcasts the result to the room through the SignalR hub at `/documenteditorhub`.
5. A client that falls behind calls `GetActionsFromServer` to fetch every row with a `version` newer than its own.

The tables are created automatically on first use. You don't need to run any migration.

## Prerequisites

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- A running PostgreSQL server (local, Docker or cloud) and a database you can create tables in
- A [Syncfusion license key](https://help.syncfusion.com/common/essential-studio/licensing/overview)

## Integrating PostgreSQL

### 1. Create a database

```sql
CREATE DATABASE documenteditor;
```

For a quick local server with Docker:

```bash
docker run --name de-postgres -e POSTGRES_PASSWORD=<password> -p 5432:5432 -d postgres
```

### 2. Add the NuGet packages

`WebApplication1.csproj` already references them. To add them to your own project:

```bash
dotnet add package Npgsql
dotnet add package Syncfusion.EJ2.WordEditor.AspNet.Core
dotnet add package Syncfusion.EJ2.SpellChecker.AspNet.Core
```

### 3. Configure the connection string

Replace the placeholder in `appsettings.json`:

```json
"ConnectionStrings": {
  "DocumentEditorDatabase": "Host=localhost;Port=5432;Database=documenteditor;Username=postgres;Password=<password>"
}
```

Don't commit real credentials. For local development, use user secrets or an environment variable instead:

```bash
dotnet user-secrets set "ConnectionStrings:DocumentEditorDatabase" "Host=localhost;Port=5432;Database=documenteditor;Username=postgres;Password=<password>"
# or
export ConnectionStrings__DocumentEditorDatabase="Host=...;"
```

### 4. Read the connection string in the controller

`CollaborativeEditingController` reads it from configuration and opens an `NpgsqlConnection` for each operation:

```csharp
connectionString = config.GetConnectionString("DocumentEditorDatabase");

using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
{
    connection.Open();
    // run NpgsqlCommand ...
}
```

### 5. Register SignalR and CORS in `Program.cs`

```csharp
builder.Services.AddSignalR();
builder.Services.AddSession();
builder.Services.AddCors(o => o.AddPolicy("AllowAllOrigins",
    b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
// ...
app.MapHub<DocumentEditorHub>("/documenteditorhub");
```

Restrict the CORS policy to your own origins in production.

### 6. Add your Syncfusion license key

Register the key at startup, for example with `SyncfusionLicenseProvider.RegisterLicense("<your key>")` in `Program.cs`. Without a valid key, a license banner appears in the editor.

## Run the sample

```bash
dotnet restore
dotnet run
```

Open the application URL that `dotnet run` prints. Open it in two browser windows with different user names and the same document to see edits sync between them. The `Properties/launchSettings.json` file lists the configured URLs.

## Notes

- Room names become PostgreSQL table names. The sample quotes them as identifiers (`"roomName"`). In a production app, validate or sanitize room names, and use parameters for every value, to prevent SQL injection.
- To scale the SignalR hub across several servers, add a Redis backplane or Azure SignalR Service. The project already references both packages.
- Make sure the database user has `CREATE TABLE`, `INSERT`, `SELECT` and `UPDATE` permissions.
