using Streamflix.DTOs;
using Streamflix.Model;
using System.Collections.Concurrent;

namespace Streamflix.Services
{
    public class WatchHistoryQueue
    {
        private readonly ConcurrentQueue<WatchHistoryDto> _queue = new();

        public void Enqueue(WatchHistoryDto history)
        {
            _queue.Enqueue(history);
        }

        public List<WatchHistoryDto> DequeueBatch(int batchSize)
        {
            var batch = new List<WatchHistoryDto>();

            for (int i = 0; i < batchSize; i++)
            {
                if (_queue.TryDequeue(out var history))
                {
                    batch.Add(history);
                }
                else
                {
                    break;
                }
            }
            return batch;
        }

        public int GetQueueSize()
        {
            return _queue.Count;
        }
    }
}
