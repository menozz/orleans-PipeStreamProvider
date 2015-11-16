﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Orleans.Serialization;
using Orleans.Streams;

namespace PipeStreamProvider
{
    public class PipeQueueAdapter : IQueueAdapter
    {
        private readonly Logger _logger;
        private readonly IStreamQueueMapper _streamQueueMapper;
        private readonly ConcurrentDictionary<QueueId, Queue<byte[]>> _queues;

        public PipeQueueAdapter(Logger logger, IStreamQueueMapper streamQueueMapper, string name)
        {
            _logger = logger;
            _streamQueueMapper = streamQueueMapper;
            _queues = new ConcurrentDictionary<QueueId, Queue<byte[]>>();

            Name = name;
        }

        public Task QueueMessageBatchAsync<T>(Guid streamGuid, string streamNamespace, IEnumerable<T> events, StreamSequenceToken token,
            Dictionary<string, object> requestContext)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events), "Trying to QueueMessageBatchAsync null data.");
            }

            var queueId = _streamQueueMapper.GetQueueForStream(streamGuid, streamNamespace);
            Queue<byte[]> queue;
            if (!_queues.TryGetValue(queueId, out queue))
            {
                var tmpQueue = new Queue<byte[]>();
                queue = _queues.GetOrAdd(queueId, tmpQueue);
            }

            var eventsAsObjects = events.Cast<object>().ToList();

            var container = new PipeQueueAdapterBatchContainer(streamGuid, streamNamespace, eventsAsObjects, requestContext);

            var bytes = SerializationManager.SerializeToByteArray(container);

            queue.Enqueue(bytes);

            return TaskDone.Done;
        }

        public IQueueAdapterReceiver CreateReceiver(QueueId queueId)
        {
            Queue<byte[]> queue;
            if (!_queues.TryGetValue(queueId, out queue))
            {
                var tmpQueue = new Queue<byte[]>();
                queue = _queues.GetOrAdd(queueId, tmpQueue);
            }

            return new PipeQueueAdapterReceiver(_logger, queueId, queue);
        }

        public string Name { get; }
        public bool IsRewindable { get; } = true;

        public StreamProviderDirection Direction => StreamProviderDirection.ReadWrite;
    }
}