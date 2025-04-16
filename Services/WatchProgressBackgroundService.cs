using Microsoft.EntityFrameworkCore;
using Streamflix.Data;
using Streamflix.DTOs;
using Streamflix.Model;

namespace Streamflix.Services
{
    public class WatchProgressBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly WatchHistoryQueue _queue;
        private readonly ILogger<WatchProgressBackgroundService> _logger;
        private const int BatchSize = 50;

        public WatchProgressBackgroundService(IServiceScopeFactory scopeFactory, WatchHistoryQueue queue, ILogger<WatchProgressBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WatchProgressBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_queue.GetQueueSize() > 0)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var batch = _queue.DequeueBatch(BatchSize);

                        if (batch.Count > 0)
                        {
                            foreach (var history in batch)
                            {
                                var existingHistory = await dbContext.WatchHistory
                                    .FirstOrDefaultAsync(h => h.UserId == history.UserId && h.VideoTitle == history.VideoTitle);

                                if (existingHistory != null)
                                {
                                    existingHistory.CurrentPosition = history.CurrentPosition;
                                    existingHistory.LastUpdated = DateTime.UtcNow;
                                }
                                else
                                {
                                    var watchHistory = new WatchHistory
                                    {
                                        UserId = history.UserId,
                                        VideoTitle = history.VideoTitle,
                                        CurrentPosition = history.CurrentPosition,
                                        LastUpdated = DateTime.UtcNow
                                    };

                                    dbContext.WatchHistory.Add(watchHistory);
                                }

                                await dbContext.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation($"Saved {batch.Count} watch history updates.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing watch history updates.");
                }

                await Task.Delay(5000, stoppingToken); // Save into database every 5 seconds
            }

            _logger.LogInformation("WatchProgressBackgroundService stopping.");
        }
    }
}
