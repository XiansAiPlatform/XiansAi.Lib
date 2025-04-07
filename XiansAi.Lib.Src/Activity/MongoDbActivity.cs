using MongoDB.Driver;
using MongoDB.Bson;
public class MongoDbActivity
{
   public List<BsonDocument> GetLogsFromMongo(string workflowId, string runId)
    {
        const string connectionString = "mongodb+srv://admin:2O435Onr2khaRZt8@skenx.oh6ezus.mongodb.net/temporal_logs?retryWrites=true&w=majority&appName=SKENX";
        const string databaseName = "temporal_logs";
        const string collectionName = "workflow_logs";

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        var collection = database.GetCollection<BsonDocument>(collectionName);

        var filter = Builders<BsonDocument>.Filter.Eq("Properties.sampleWorkflowId", workflowId) &
                     Builders<BsonDocument>.Filter.Eq("Properties.sampleRunId", runId);

        var logs = collection.Find(filter).ToListAsync();
        return logs.Result;
    }
}
