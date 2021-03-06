﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Contexts;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Services;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Content
{
    
    [Obsolete("Remove this class as soon as the Manifest Engine Mk2 is in place.")]
    public class RemoveExpiredManifestsV2Command
    {
        private readonly IUtcDateTimeProvider _DateTimeProvider;
        private readonly Func<ContentDbContext> _DbContextProvider;
        private readonly ILogger<RemoveExpiredManifestsV2Command> _Logger;
        private readonly IManifestConfig _ManifestConfig;
        private RemoveExpiredManifestsCommandResult? _Result;

        public RemoveExpiredManifestsV2Command(Func<ContentDbContext> dbContextProvider, ILogger<RemoveExpiredManifestsV2Command> logger, IManifestConfig manifestConfig, IUtcDateTimeProvider dateTimeProvider)
        {
            _DbContextProvider = dbContextProvider ?? throw new ArgumentNullException(nameof(dbContextProvider));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ManifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
            _DateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(manifestConfig));
        }

        /// <summary>
        /// Manifests are updated regularly.
        /// </summary>
        public async Task<RemoveExpiredManifestsCommandResult> Execute()
        {
            if (_Result != null)
                throw new InvalidOperationException("Object already used.");

            _Result = new RemoveExpiredManifestsCommandResult();

            _Logger.LogInformation("Begin removing expired ManifestV2s - Keep Alive Count:{count}", _ManifestConfig.KeepAliveCount);

            await using (var dbContext = _DbContextProvider())
            await using (var tx = dbContext.BeginTransaction())
            {
                _Result.Found = dbContext.Content.Count();

                var zombies = dbContext.Content
                    .Where(x => x.Type == ContentTypes.ManifestV2)
                    .OrderByDescending(x => x.Release)
                    .Skip(_ManifestConfig.KeepAliveCount)
                    .Select(x => new { x.PublishingId, x.Release })
                    .ToList();

                _Result.Zombies = zombies.Count;
                _Logger.LogInformation("Removing expired ManifestV2s - Count:{count}", zombies.Count);
                foreach (var i in zombies)
                    _Logger.LogInformation("Removing expired ManifestV2 - PublishingId:{PublishingId} Release:{Release}", i.PublishingId, i.Release);

                if (zombies.Count == 0)
                {
                    _Logger.LogInformation("Finished removing expired ManifestV2s - Nothing to remove.");
                    return _Result;
                }

                _Result.GivenMercy = dbContext.Database.ExecuteSqlInterpolated(
                    $"WITH Zombies AS (SELECT Id FROM [Content] WHERE [Type] = {ContentTypes.ManifestV2} AND [Release] < {_DateTimeProvider.Snapshot} ORDER BY [Release] DESC OFFSET {_ManifestConfig.KeepAliveCount} ROWS) DELETE Zombies");

                _Result.GivenMercy += dbContext.Database.ExecuteSqlInterpolated(
                    $"WITH Zombies AS (SELECT Id FROM [Content] WHERE [Type] = {ContentTypes.ManifestV2} AND [Release] > {_DateTimeProvider.Snapshot}) DELETE Zombies");

                _Result.Remaining = dbContext.Content.Count();

                tx.Commit();
            }

            _Logger.LogInformation("Finished removing expired ManifestV2s - ExpectedCount:{count} ActualCount:{givenMercy}", _Result.Zombies, _Result.GivenMercy);

            if (_Result.Reconciliation != 0)
                _Logger.LogError("Reconciliation failed removing expired ManifestV2s - Found-GivenMercy-Remaining={reconciliation}.", _Result.Reconciliation);

            if (_Result.DeletionReconciliation != 0)
                _Logger.LogError("Reconciliation failed removing expired ManifestV2s - Zombies-GivenMercy={deadReconciliation}.", _Result.DeletionReconciliation);

            return _Result;
        }
    }
}