﻿using System;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ProtocolSettings;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Services;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Content
{
    public class EksMaxageCalculator
    {
        private readonly IUtcDateTimeProvider _DateTimeProvider;
        private readonly IEksConfig _EksConfig;
        private readonly ITaskSchedulingConfig _TaskSchedulingConfig;

        public EksMaxageCalculator(IUtcDateTimeProvider dateTimeProvider, IEksConfig eksConfig, ITaskSchedulingConfig taskSchedulingConfig)
        {
            _DateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _EksConfig = eksConfig ?? throw new ArgumentNullException(nameof(eksConfig));
            _TaskSchedulingConfig = taskSchedulingConfig ?? throw new ArgumentNullException(nameof(taskSchedulingConfig));
        }

        public int Execute(DateTime created)
        {
            var ttl = TimeSpan.FromDays(_EksConfig.LifetimeDays) + TimeSpan.FromHours(_TaskSchedulingConfig.DailyCleanupHoursAfterMidnight);
            var life = _DateTimeProvider.Snapshot - created;
            var remaining = (int)Math.Floor((ttl - life).TotalSeconds);
            return Math.Max(remaining, 60); //Give it another minute in case the Daily Cleanup is late.
        }
    }
}