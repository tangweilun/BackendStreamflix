using Microsoft.EntityFrameworkCore;
using Streamflix.Data;
using Streamflix.DTOs;
using Streamflix.Model;

namespace Streamflix.Services
{
    public class WatchProgressBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWatchHistoryQueue _queue;
        private readonly ILogger<WatchProgressBackgroundService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

        public WatchProgressBackgroundService(IServiceScopeFactory scopeFactory, IWatchProgressQueue queue, ILogger<WatchProgressBackgroundService> logger)
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
                    // Dequeue all available updates
                    var updates = new List<WatchProgressUpdateDto>();

                    while (_queue.TryDequeue(out var update))
                    {
                        updates.Add(update);
                    }

                    if (updates.Any())
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                            foreach (var updateDto in updates)
                            {
                                var existingHistory = await dbContext.WatchHistory
                                    .FirstOrDefaultAsync(wh => wh.UserId == updateDto.UserId && wh.ContentId == updateDto.ContentId, stoppingToken);
                            
                                // Update existing record or create a new one
                                if (existingHistory != null)
                                {
                                    existingHistory.CurrentPosition = updateDto.CurrentPosition;
                                    existingHistory.LastUpdated = DateTime.UtcNow;
                                }
                                else
                                {
                                    var newHistory = new WatchHistory
                                    {
                                        UserId = updateDto.UserId,
                                        ContentId = updateDto.ContentId,
                                        CurrentPosition = updateDto.CurrentPosition,
                                        LastUpdated = DateTime.UtcNow
                                    };

                                    dbContext.WatchHistory.Add(newHistory);
                                }
                            }

                            await dbContext.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Processed {updates.Count} watch history updates.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing watch history updates.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
