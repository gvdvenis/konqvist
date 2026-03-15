namespace Konqvist.Server.Tests;

public static class ServerAppFactoryCollection
{
    public const string Name = "Server app factory";
}

[CollectionDefinition(ServerAppFactoryCollection.Name, DisableParallelization = true)]
public sealed class ServerAppFactoryCollectionDefinition;
