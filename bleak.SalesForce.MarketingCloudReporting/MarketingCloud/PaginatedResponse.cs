
namespace bleak.Salesforce.MarketingCloud
{
    public class PaginatedResponse<T>
    {
        public T Result { get; set; }
        public string RequestID { get; set; }
        public bool HasMoreData { get; set; }
    }
}
