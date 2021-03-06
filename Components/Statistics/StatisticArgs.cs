﻿using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Statistics
{
    public class StatisticArgs
    {
        public string Name { get; set; }
        public string Qualifier { get; set; }
        public double Value { get; set; }
    }
}
