using System;
using System.Collections.Generic;
using System.Linq;
using Orleans.Providers.Streams.Common;
using Orleans.Runtime;
using Orleans.Streams;

namespace PipeStreamProvider
{
    // This is based on AzureQueueBatchContainer
    [Serializable]
    public class PipeQueueAdapterBatchContainer : IBatchContainer
    {
        private readonly Dictionary<string, object> _requestContext;
        private readonly List<object> _events;

        /// <summary>
        /// A timestamp for this container
        /// </summary>
        public TimeSequenceToken RealToken { get; }

        public Guid StreamGuid { get; }
        public string StreamNamespace { get; }

        public StreamSequenceToken SequenceToken => RealToken;

        public PipeQueueAdapterBatchContainer(Guid streamGuid, string streamNamespace, List<object> events, TimeSequenceToken token, Dictionary<string, object> requestContext)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events), "Message contains no events");
            StreamGuid = streamGuid;
            StreamNamespace = streamNamespace;
            RealToken = token;
            _events = events;
            _requestContext = requestContext;
        }

        public IEnumerable<Tuple<T, StreamSequenceToken>> GetEvents<T>()
        {
            // TODO: optimise this
            return _events.OfType<T>().Select((e, i) => Tuple.Create(e, (StreamSequenceToken)RealToken.CreateSequenceTokenForEvent(i)));
        }

        public bool ImportRequestContext()
        {
            if (_requestContext == null)
                return false;
            RequestContext.Import(_requestContext);
            return true;
        }

        public bool ShouldDeliver(IStreamIdentity stream, object filterData, StreamFilterPredicate shouldReceiveFunc)
        {
            foreach (var item in _events)
            {
                if (shouldReceiveFunc(stream, filterData, item))
                    return true; // There is something in this batch that the consumer is interested in, so we should send it.
            }
            return false; // Consumer is not interested in any of these events, so don't send.
        }

        public override string ToString()
        {
            return $"[PipeQueueBatchContainer:Stream={StreamGuid},#Items={_events.Count}]";
        }
    }
}