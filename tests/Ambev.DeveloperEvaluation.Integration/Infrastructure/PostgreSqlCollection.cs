using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Infrastructure;

[CollectionDefinition("PostgreSql")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture> { }
