using Streamflix.DTOs;
using System.Collections.Concurrent;

namespace Streamflix.Services
{
    public interface IWatchHistoryQueue
    {
        void Enqueue(WatchProgressUpdateDto update);
        bool TryDequeue(out WatchProgressUpdateDto update);
        IEnumerable<WatchProgressUpdateDto> GetAll();
        void Clear();
    }

    public class WatchProgressQueue : IWatchHistoryQueue
    {
        private readonly ConcurrentQueue<WatchProgressUpdateDto> _queue = new ConcurrentQueue<WatchProgressUpdateDto>();

        public void Enqueue(WatchProgressUpdateDto update)
        {
            _queue.Enqueue(update);
        }

        public bool TryDequeue(out WatchProgressUpdateDto update)
        {
            return _queue.TryDequeue(out update);
        }

        public IEnumerable<WatchProgressUpdateDto> GetAll()
        {
            return _queue.ToList();
        }

        public void Clear()
        {
            while (_queue.TryDequeue(out _)) { }
        }
    }
}
