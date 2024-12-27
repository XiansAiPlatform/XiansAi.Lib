using Microsoft.Extensions.Logging;
using MongoDB.Driver;

public interface IMongoDbService
{
    IMongoDatabase GetDatabase();
}

public class MongoDbService : IMongoDbService
{
    private readonly IMongoClient _client;

    private readonly MongoDBConfig _config;

    public MongoDbService(MongoDBConfig config)
    {
        _config = config;
        _client = new MongoClient(config.ConnectionString);
    }

    public IMongoDatabase GetDatabase()
    {
        var databaseName = _config.DatabaseName;
        return _client.GetDatabase(databaseName);
    }
}