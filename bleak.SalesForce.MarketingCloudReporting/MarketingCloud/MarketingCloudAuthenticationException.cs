using SalesforceMarketingCloudSoapApi;
using System;
using System.Collections.Generic;
using System.Text;

namespace bleak.Salesforce.MarketingCloud
{
    public abstract class BaseMarketingCloudException : Exception
    {
        internal BaseMarketingCloudException(string message) : base(message) { }
    }
    public abstract class BaseMarketingCloudApiException : BaseMarketingCloudException
    {
        internal BaseMarketingCloudApiException(string status, string message, string requestId) : base(message)
        {
            Status = status;
            RequestId = requestId;
        }

        public string Status { get; private set; }
        public string RequestId { get; private set; }
    }

    public class MarketingCloudStatusException : BaseMarketingCloudApiException
    {
        internal MarketingCloudStatusException(GetSystemStatusResponse response) : base(status: response.OverallStatus, message: response.OverallStatusMessage, requestId: response.RequestID) { }
    }

    public class MarketingCloudAuthenticationException : BaseMarketingCloudException
    {
        internal MarketingCloudAuthenticationException() : base("Login Failed") { }
    }
}
