namespace DarkVelocity.E2E.Fixtures;

[CollectionDefinition(Name)]
public class E2ECollection : ICollectionFixture<ServiceFixture>
{
    public const string Name = "E2E";
}
