# Completion checks
- Build changed production code: `dotnet build .\src\Konqvist.sln --configuration Release`.
- Run all tests for data/gameplay changes: `dotnet test .\src\Konqvist.sln --configuration Release`.
- During iteration, filter one xUnit method via `dotnet test .\tests\Konqvist.Data.Tests\Konqvist.Data.Tests.csproj --filter "FullyQualifiedName=Konqvist.Data.Tests.MapDataStoreTests.<MethodName>"`.
- No repository-specific lint/format target is configured.