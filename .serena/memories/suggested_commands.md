# Commands
Run from repository root (PowerShell):
- Restore: `dotnet restore .\src\Konqvist.sln`
- Build: `dotnet build .\src\Konqvist.sln --configuration Release`
- Full tests: `dotnet test .\src\Konqvist.sln --configuration Release`
- One test: `dotnet test .\tests\Konqvist.Data.Tests\Konqvist.Data.Tests.csproj --filter "FullyQualifiedName=Konqvist.Data.Tests.MapDataStoreTests.<MethodName>"`
- Run app: `dotnet run --project .\src\Konqvist.Web\Konqvist.Web.csproj`
- Local network HTTPS setup: run `src\setup-local-ssl.cmd` in an elevated Windows terminal; output is `src\.certs`.