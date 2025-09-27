using AspireApp.RagService.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspireApp.RagService.Services
{
    public class RagService
    {
        private readonly QdrantClient _client;
        private const string CollectionName = "rag_docs";
        private const int VectorSize = 1536;

        public RagService()
        {
            _client = new QdrantClient("localhost", 6334);

            EnsureCollectionExists().Wait();
        }

        private async Task EnsureCollectionExists()
        {
            var collections = await _client.ListCollectionsAsync();

            if (!collections.Contains(CollectionName))
            {
                await _client.CreateCollectionAsync(collectionName: CollectionName, vectorsConfig: new VectorParams
                {
                    Size = VectorSize,
                    Distance = Distance.Cosine,
                });
            }
        }

        public async Task AddDocument(string text, Guid guid, CancellationToken cancellationToken)
        {
            var vector = await ConvertToVector(text, cancellationToken);
            if (vector == null) return;

            var point = new PointStruct
            {
                Id = new PointId(guid),
                Vectors = new Vectors
                {
                    Vector = new Qdrant.Client.Grpc.Vector
                    {
                        Data = { vector }
                    }
                },
                Payload = { { "text", text } }
            };

            await _client.UpsertAsync(CollectionName, new[] { point });
        }

        public async Task<IEnumerable<DocumentResult>> Search(string query, ulong limit, CancellationToken cancellationToken)
        {
            var queryVector = await ConvertToVector(query, cancellationToken);
            if (queryVector == null) return null;

            var results = await _client.QueryAsync(
                 collectionName: CollectionName,
                 query: new Query(queryVector),
                 limit: limit,
                 cancellationToken: cancellationToken
             );

            return results.Select(r =>
            new Models.DocumentResult()
            {
                Id = r.Id.Uuid,
                Text = r.Payload.TryGetValue("text", out var val)
                    ? val.StringValue : "",
                Score = r.Score
            });
        }

        private async Task<float[]?> ConvertToVector(string input, CancellationToken cancellation)
        {
            var result = await ConvertToVector([input], cancellation);
            if (result != null && result.Count > 0) return result[0];
            return null;
        }

        private async Task<List<float[]>> ConvertToVector(List<string> input, CancellationToken cancellation)
        {
            var client = new HttpClient()
            {
                BaseAddress = new Uri(Environment.GetEnvironmentVariable("TEXT_EMBEDDING_URL"))
            };
            var request = new { texts = input };

            var response = await client.PostAsJsonAsync("/embed", request, cancellation);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<EmbedResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Vectors == null)
                return new List<float[]>();

            return result.Vectors.Select(v => PadVector(v.Select(Convert.ToSingle).ToArray())).ToList();
        }

        public static float[] PadVector(float[] vector)
        {
            if (vector.Length > VectorSize)
            {
                throw new ArgumentException($"Vector size {vector.Length} is larger than target size {VectorSize}.");
            }
            List<float> paddedVector = new List<float>(VectorSize);
            paddedVector.AddRange(vector);
            int paddingCount = VectorSize - vector.Length;
            for (int i = 0; i < paddingCount; i++)
            {
                paddedVector.Add(0f);
            }

            return paddedVector.ToArray();
        }

        class EmbedResponse
        {
            [JsonPropertyName("vectors")]
            public List<List<double>> Vectors { get; set; }
        }
    }
}
