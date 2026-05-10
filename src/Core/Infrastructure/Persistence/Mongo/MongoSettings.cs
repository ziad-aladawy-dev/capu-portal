namespace CapitalUniversity.Core.Infrastructure.Persistence.Mongo;

public class MongoSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string LogsCollection { get; set; } = string.Empty;
}
