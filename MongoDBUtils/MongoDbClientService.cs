using MongoDB.Driver;
using System.Security.Cryptography.X509Certificates;

public interface IMongoDbClientService
{
    IMongoDatabase GetDatabase();
    IMongoCollection<T> GetCollection<T>(string collectionName);
    IMongoClient GetClient();
}

public class MongoDbClientService : IMongoDbClientService
{
    private readonly IMongoClient _mongoClient;
    public  MongoDBConfig Config { get; init; }


    public MongoDbClientService(MongoDBConfig config)
    {
        Config = config;
        var serverApi = new ServerApi(ServerApiVersion.V1);

        #pragma warning disable SYSLIB0057 // Type or member is obsolete
        var cert = new X509Certificate2(Config.PfxPath, Config.PfxPassphrase);
        #pragma warning restore SYSLIB0057 // Type or member is obsolete

        var sslSettings = new SslSettings
        {
            ClientCertificates = new List<X509Certificate>() { cert }
        };

        _mongoClient = new MongoClient(new MongoClientSettings
        {
            ServerApi = serverApi,
            SslSettings = sslSettings
        });
    }

    public IMongoDatabase GetDatabase()
    {
        return _mongoClient.GetDatabase(Config.DatabaseName);
    }

    public IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        var database = GetDatabase();
        return database.GetCollection<T>(collectionName);
    }

    public IMongoClient GetClient()
    {
        return _mongoClient;
    }
}

