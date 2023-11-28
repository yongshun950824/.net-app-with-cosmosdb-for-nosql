using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Linq;

#region Exercise - Create API for NoSQL account and resources
const string connectionString = "AccountEndpoint=https://mslearn-389250096.documents.azure.com:443/;AccountKey=4PfeLKZx9UYaELQL0KDgtW9fsQH56YZMq0eiPUF4jpsMSOQQHKfZTi66F06tEDmsOwd3MToKGZD0ACDbQvEunw==;";

Console.WriteLine($"[Connection string]:\t{connectionString}");

CosmosSerializationOptions serializerOptions = new()
{
    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
};

using CosmosClient client = new CosmosClientBuilder(connectionString)
    .WithSerializerOptions(serializerOptions)
    .Build();

Console.WriteLine("[Client ready]");

Database database = await client.CreateDatabaseIfNotExistsAsync(
    id: "cosmicworks"
);

Console.WriteLine($"[Database created]:\t{database.Id}");

ContainerProperties properties = new(
    id: "products",
    partitionKeyPath: "/categoryId"
);

var throughput = ThroughputProperties.CreateAutoscaleThroughput(
    autoscaleMaxThroughput: 1000
);

Container container = await database.CreateContainerIfNotExistsAsync(
    containerProperties: properties,
    throughputProperties: throughput
);

Console.WriteLine($"[Container created]:\t{container.Id}");
#endregion Exercise - Create API for NoSQL account and resources

#region Exercise - Create new items
Category goggles = new(
    Id: "ef7fa0f1-0e9d-4435-aaaf-a778179a94ad",
    CategoryId: "gear-snow-goggles"
);

PartitionKey gogglesKey = new("gear-snow-goggles");

Category result = await container.UpsertItemAsync(goggles, gogglesKey);

Console.WriteLine($"[New item created]:\t{result.Id}\t(Type: {result.Type})");

Category helmets = new(
    Id: "91f79374-8611-4505-9c28-3bbbf1aa7df7",
    CategoryId: "gear-climb-helmets"
);

PartitionKey helmetsKey = new("gear-climb-helmets");

ItemResponse<Category> response = await container.UpsertItemAsync(helmets, helmetsKey);

Console.WriteLine($"[New item created]:\t{response.Resource.Id}\t(Type: {response.Resource.Type})\t(RUs: {response.RequestCharge})");

Category tents = new(
    Id: "5df21ec5-813c-423e-9ee9-1a2aaead0be4",
    CategoryId: "gear-camp-tents"
);

Product cirroa = new(
    Id: "e8dddee4-9f43-4d15-9b08-0d7f36adcac8",
    CategoryId: "gear-camp-tents"
){
    Name = "Cirroa Tent",
    Price = 490.00m,
    Archived = false,
    Quantity = 15
};
Product kuloar = new(
    Id: "e6f87b8d-8cd7-4ade-a005-14d3e2fbd1aa",
    CategoryId: "gear-camp-tents"
){
    Name = "Kuloar Tent",
    Price = 530.00m,
    Archived = false,
    Quantity = 8
};
Product mammatin = new(
    Id: "f7653468-c4b8-47c9-97ff-451ee55f4fd5",
    CategoryId: "gear-camp-tents"
){
    Name = "Mammatin Tent",
    Price = 0.00m,
    Archived = true,
    Quantity = 0
};
Product nimbolo = new(
    Id: "6e3b7275-57d4-4418-914d-14d1baca0979",
    CategoryId: "gear-camp-tents"
){
    Name = "Nimbolo Tent",
    Price = 330.00m,
    Archived = false,
    Quantity = 35
};

PartitionKey tentsKey = new("gear-camp-tents");

TransactionalBatch batch = container.CreateTransactionalBatch(tentsKey)
    .UpsertItem<Category>(tents)
    .UpsertItem<Product>(cirroa)
    .UpsertItem<Product>(kuloar)
    .UpsertItem<Product>(mammatin)
    .UpsertItem<Product>(nimbolo);

Console.WriteLine("[Batch started]");

using TransactionalBatchResponse batchResponse = await batch.ExecuteAsync();

for (int i = 0; i < batchResponse.Count; i++)
{
    TransactionalBatchOperationResult<Item> batchResult = batchResponse.GetOperationResultAtIndex<Item>(i);
    Console.WriteLine($"[New item created]:\t{batchResult.Resource.Id}\t(Type: {batchResult.Resource.Type})");
}

Console.WriteLine($"[Batch completed]:\t(RUs: {batchResponse.RequestCharge})");
#endregion Exercise - Create new items

#region Exercise - Read and query items
PartitionKey readKey = new("gear-climb-helmets");

ItemResponse<Category> readResponse = await container.ReadItemAsync<Category>(
    id: "91f79374-8611-4505-9c28-3bbbf1aa7df7",
    partitionKey: readKey
);

Category readItem = readResponse.Resource;

Console.WriteLine($"[Point read item]:\t{readItem.Id}\t(RUs: {readResponse.RequestCharge})");

string statement = "SELECT * FROM products p WHERE p.categoryId = @partitionKey";
var query = new QueryDefinition(
    query: statement
);

var parameterizedQuery = query.WithParameter("@partitionKey", "gear-camp-tents");
using FeedIterator<Product> feed = container.GetItemQueryIterator<Product>(
    queryDefinition: parameterizedQuery
);

Console.WriteLine($"[Start query]:\t{statement}");
double totalRequestCharge = 0d;

while (feed.HasMoreResults)
{
    FeedResponse<Product> page = await feed.ReadNextAsync();

    totalRequestCharge += page.RequestCharge;

    foreach (Product item in page)
    {
        Console.WriteLine($"[Returned item]:\t{item.Id}\t(Name: {item.Name ?? "N/A"})");
    }
}

Console.WriteLine($"[Query metrics]:\t(RUs: {totalRequestCharge})");
#endregion Exercise - Read and query items

#region Exercise - Enumerate items using language-integrated query (LINQ)
IOrderedQueryable<Product> queryable = container.GetItemLinqQueryable<Product>();

var matches = queryable
    .Where(p => p.Type == nameof(Product))
    .Where(p => !p.Archived)
    .OrderBy(p => p.Price);

using FeedIterator<Product> linqFeed = matches.ToFeedIterator();

Console.WriteLine($"[Start LINQ query]");

while (linqFeed.HasMoreResults)
{    
    FeedResponse<Product> page = await linqFeed.ReadNextAsync();
    Console.WriteLine($"[Page RU charge]:\t{page.RequestCharge}");

    foreach (Product item in page)
    {
        Console.WriteLine($"[Returned item]:\t{item}");
    }
}
#endregion Exercise - Enumerate items using language-integrated query (LINQ)