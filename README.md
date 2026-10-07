# POS_MT

ASP.NET Core MVC Point of Sale application (.NET 8) using SQL Server and Entity Framework Core (Database First).

## Setup

1. Copy `appsettings.Example.json` values into `appsettings.Development.json` (local only; not committed).
2. Set the `ConnectionStrings:POS_MT` value for your SQL Server.
3. Run:

```bash
dotnet restore
dotnet run --launch-profile http
```

Open http://localhost:5227 and sign in with a user from the `Users` table.
