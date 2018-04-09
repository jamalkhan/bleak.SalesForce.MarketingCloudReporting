using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Xml;
using bleak.Salesforce.MarketingCloud;
using SalesforceMarketingCloudSoapApi;

namespace bleak.Salesforce
{
    public class MarketingCloudSendReporting
    {
        object syncroot = new object();
        SoapClient _client;
        internal SoapClient Client
        {
            get
            {
                if (_client == null)
                {
                    lock (syncroot)
                    {
                        if (_client == null)
                        {
                            _client = new SoapClient(SoapClient.EndpointConfiguration.Soap, _auth.Endpoint);
                            _client.ClientCredentials.UserName.UserName = _auth.Username;
                            _client.ClientCredentials.UserName.Password = _auth.Password;
                            _client.Endpoint.EndpointBehaviors.Add(new SoapSecurityHeaderBehavior(_auth.Username, _auth.Password));
                        }
                    }
                }
                return _client;
            }
        }



        AuthInfo _auth;
        public MarketingCloudSendReporting(AuthInfo auth)
        { 
            _auth = auth; 
        }

        internal RetrieveResponse RetrieveAsync(RetrieveRequest request)
        {
            var request1 = new RetrieveRequest1(request);
            var task = Client.RetrieveAsync(request1);
            task.Wait();
            var result = task.Result;

            if (result.OverallStatus.ToLower() == "moredataavailable")
            {
                // TODO: Implement Paginated Results
                throw new NotImplementedException("Paginated Results have not yet been implemented");
            }
            return result;
        }

        /// <summary>
        ///  This enumerates all sends
        /// </summary>
        /// <returns>The sends.</returns>
        /// <param name="startDate">Start date.</param>
        /// <param name="endDate">End date.</param>
        /// <param name="daysToCountBackForSends">Days to count back for sends.</param>
        public IEnumerable<APIObject> GetSends(DateTime startDate, DateTime endDate, int daysToCountBackForSends = 30)
        {
                SimpleFilterPart dateFilter = new SimpleFilterPart();
                dateFilter.Property = "SendDate";
                dateFilter.SimpleOperator = SimpleOperators.between;
                dateFilter.DateValue = new DateTime[2];
                dateFilter.DateValue[0] = startDate.AddDays(-1 * daysToCountBackForSends);
                dateFilter.DateValue[1] = endDate;

                var rr = new RetrieveRequest();
                rr.ClientIDs = new ClientID[] { new ClientID() { ID = _auth.ClientId.Value, IDSpecified = true } };
                rr.ObjectType = "Send";
                rr.Properties = new string[] { "ID", "Additional", "SendDate", "SentDate", "EmailName" };
                rr.Filter = dateFilter;

                var result = RetrieveAsync(rr);
                if (result.OverallStatus.ToLower().StartsWith("error"))
                {
                    var err = String.Format("ExactTarget API returned status: {0} requestId: {1}", result.OverallStatus, result.RequestID);
                    throw new Exception(err);
                }

                return result.Results;
        }


        protected List<T> RetrieveEtObjects<T>(
            string objType,
            string[] retProps,
            out string continueRequestId,
            FilterPart filter = null,
            string existingRequestId = null
            ) where T : APIObject
        {
            RetrieveRequest rr = null;
            continueRequestId = null;
            if (!string.IsNullOrEmpty(existingRequestId))
            {
                rr = new RetrieveRequest
                {
                    ContinueRequest = existingRequestId
                };
            }
            else if (filter != null)
            {
                rr = new RetrieveRequest
                {
                    ObjectType = objType,
                    Properties = retProps,
                    Filter = filter
                };
            }
            else
            {
                rr = new RetrieveRequest
                {
                    ObjectType = objType,
                    Properties = retProps,
                };
            }

            if (_auth.ClientId.HasValue)
            {
                rr.ClientIDs = new ClientID[] { new ClientID() { ID = _auth.ClientId.Value, IDSpecified = true}};
            }

            RetrieveRequest1 request1 = new RetrieveRequest1(rr);
            var result = Client.RetrieveAsync(request1).Result;

            if (result.OverallStatus.ToLower().StartsWith("error"))
            {
                throw new Exception(result.OverallStatus);
            }

            if (result.OverallStatus.ToLower() == "moredataavailable")
            {
                continueRequestId = result.RequestID;
            }

            return result.Results.ToList().ConvertAll(obj => (T)obj);
        }

        /// <summary>
        /// This method iterates through all Events for a Send
        /// </summary>
        /// <returns>The tracking events for send and event type.</returns>
        /// <param name="startDate">Start date.</param>
        /// <param name="endDate">End date.</param>
        /// <param name="sendId">Send identifier.</param>
        /// <param name="eventType">Event type.</param>
        /// <param name="nextPageId">Next page identifier.</param>
        /// <param name="batchSize">Batch size.</param>
        /// <param name="continuePageId">Continue page identifier.</param>
        public List<TrackingEvent> LoadTrackingEventsForSendAndEventType(DateTime startDate, DateTime endDate, string sendId, string eventType, out string nextPageId, int batchSize, string continuePageId = null)
        {
            List<TrackingEvent> trackingEvents;
            string newRequestId = null;
            if (string.IsNullOrEmpty(continuePageId))
            {
                trackingEvents = RetrieveEtObjects<TrackingEvent>(
                    objType: eventType,
                    retProps: new[] { "SendID", "EventDate", "EventType", "SubscriberKey" },
                    continueRequestId: out newRequestId,
                    filter: new ComplexFilterPart()
                    {
                        LeftOperand = new SimpleFilterPart()
                        {
                            Property = "SendID",
                            SimpleOperator = SimpleOperators.equals,
                            Value = new[] { sendId }
                        },
                        LogicalOperator = LogicalOperators.AND,
                        RightOperand = new SimpleFilterPart()
                        {
                            Property = "EventDate",
                            SimpleOperator = SimpleOperators.between,
                            DateValue = new DateTime[] { startDate, endDate }
                        }
                    });
            }
            else
            {
                trackingEvents = RetrieveEtObjects<TrackingEvent>(
                    objType: eventType,
                    retProps: new[] { "SendID", "EventDate", "EventType", "SubscriberKey" },
                    continueRequestId: out newRequestId,
                    existingRequestId: continuePageId
                    );
            }

            nextPageId = null;
            while (!string.IsNullOrEmpty(newRequestId))
            {
                // assign existingRequestId = the lastRun's newRequestId
                string existingRequestId = newRequestId;

                // then set newRequestId to blank for the incoming one.
                newRequestId = null;

                // use the existingRequestId as the query parameter this time.
                var nextResults = RetrieveEtObjects<TrackingEvent>(
                    objType: eventType,
                    retProps: new[] { "SendID", "EventDate", "EventType", "SubscriberKey" },
                    continueRequestId: out newRequestId,
                    existingRequestId: existingRequestId
                    );

                trackingEvents.AddRange(nextResults);

                if (trackingEvents.Count >= batchSize && !string.IsNullOrEmpty(newRequestId))
                {
                    nextPageId = newRequestId;
                    return trackingEvents;
                }
            }

            return trackingEvents;
        }



    }









    public class SoapSecurityHeader : MessageHeader
    {
        private readonly string _username;
        private readonly string _password;

        public SoapSecurityHeader(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public override string Name { get; } = "Security";

        public override string Namespace { get; } = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

        private const string WsuNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteStartElement("wsse", "UsernameToken", Namespace);
            writer.WriteAttributeString("wsu", "Id", WsuNamespace, Guid.NewGuid().ToString());

            writer.WriteStartElement("wsse", "Username", Namespace);
            writer.WriteValue(_username);
            writer.WriteEndElement();

            writer.WriteStartElement("wsse", "Password", Namespace);
            writer.WriteAttributeString("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText");
            writer.WriteValue(_password);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteStartElement("wsse", Name, Namespace);
            writer.WriteXmlnsAttribute("wsse", Namespace);
        }
    }

    public class SoapSecurityHeaderInspector : IClientMessageInspector
    {
        private readonly string _username;
        private readonly string _password;

        public SoapSecurityHeaderInspector(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public void AfterReceiveReply(ref System.ServiceModel.Channels.Message reply, object correlationState)
        {

        }

        public object BeforeSendRequest(ref System.ServiceModel.Channels.Message request, IClientChannel channel)
        {
            request.Headers.Add(new SoapSecurityHeader(_username, _password));

            return null;
        }
    }

    public class SoapSecurityHeaderBehavior : IEndpointBehavior
    {
        private readonly string _username;
        private readonly string _password;

        public SoapSecurityHeaderBehavior(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {

        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.ClientMessageInspectors.Add(new SoapSecurityHeaderInspector(_username, _password));
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {

        }

        public void Validate(ServiceEndpoint endpoint)
        {

        }
    }



    public class NextPageDetails
    {
        public string NextPageId { get; set; }
        public string CurrentEventType { get; set; }
        public List<string> ProcessedEventTypes { get; set; }
    }

}
