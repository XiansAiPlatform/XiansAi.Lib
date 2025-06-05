using OpenAI.Embeddings;
using Microsoft.Extensions.Configuration;

namespace XiansAi.VectorStore;
public class EmbeddingService
{
    private readonly EmbeddingClient _client;

    public EmbeddingService()
    {
        var apiKey = "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";
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("OpenAI API key is missing in configuration.");
        }

        _client = new EmbeddingClient("text-embedding-3-small", apiKey);
    }

    public float[] GenerateEmbedding(string input)
    {
        OpenAIEmbedding embedding = _client.GenerateEmbedding(input);
        return embedding.ToFloats().ToArray();
    }
}
