using Streamflix.DTOs;
using Streamflix.Model;
using System.Collections.Concurrent;

namespace Streamflix.Services
{
    //public interface IWatchHistoryQueue
    //{
    //    void Enqueue(WatchProgressUpdateDto update);
    //    bool TryDequeue(out WatchProgressUpdateDto update);
    //    IEnumerable<WatchProgressUpdateDto> GetAll();
    //    void Clear();
    //}

    //public class WatchProgressQueue : IWatchHistoryQueue
    //{
    //    private readonly ConcurrentQueue<WatchProgressUpdateDto> _queue = new ConcurrentQueue<WatchProgressUpdateDto>();

    //    public void Enqueue(WatchProgressUpdateDto update)
    //    {
    //        _queue.Enqueue(update);
    //    }

    //    public bool TryDequeue(out WatchProgressUpdateDto update)
    //    {
    //        return _queue.TryDequeue(out update);
    //    }

    //    public IEnumerable<WatchProgressUpdateDto> GetAll()
    //    {
    //        return _queue.ToList();
    //    }

    //    public void Clear()
    //    {
    //        while (_queue.TryDequeue(out _)) { }
    //    }
    //}

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
