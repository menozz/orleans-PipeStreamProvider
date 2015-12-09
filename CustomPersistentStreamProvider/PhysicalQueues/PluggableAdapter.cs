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
using StackExchange.Redis;
using Orleans.Providers;

namespace PipeStreamProvider.PhysicalQueues.Redis
{
    public class GenericQueueAdapter : IQueueAdapter
    {
        private Logger _logger;
        private IStreamQueueMapper _streamQueueMapper;
        private ConnectionMultiplexer _connection;
        //private readonly int _databaseNum;
        //private readonly string _server;
        private IDatabase _database;
        private string _redisListBaseName = "OrleansQueue";
        private readonly IProviderConfiguration _config;
        private readonly IProviderQueue _queueProvider;

        public GenericQueueAdapter(Logger logger, IStreamQueueMapper streamQueueMapper, string name, IProviderConfiguration config, IProviderQueue queueProvider)
        {
            Name = name;
            _config = config;
            _logger = logger;
            _streamQueueMapper = streamQueueMapper;
            _queueProvider = queueProvider;

            queueProvider.Init(_config);
        }

        public Task QueueMessageBatchAsync<T>(Guid streamGuid, string streamNamespace, IEnumerable<T> events, StreamSequenceToken token,
            Dictionary<string, object> requestContext)
        {

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events), "Trying to QueueMessageBatchAsync null data.");
            }

            var queueId = _streamQueueMapper.GetQueueForStream(streamGuid, streamNamespace);

            var eventsAsObjects = events.Cast<object>().ToList();

            var container = new PipeQueueAdapterBatchContainer(streamGuid, streamNamespace, eventsAsObjects, requestContext);

            var bytes = SerializationManager.SerializeToByteArray(container);

            _queueProvider.Enqueue(queueId, bytes);

            return TaskDone.Done;
        }


        public IQueueAdapterReceiver CreateReceiver(QueueId queueId)
        {
            return new GenericQueueAdapterReceiver(_logger, queueId, _database, queueId, _queueProvider);
        }

        public string Name { get; private set; }
        public bool IsRewindable { get; } = true;

        public StreamProviderDirection Direction => StreamProviderDirection.ReadWrite;
    }
}