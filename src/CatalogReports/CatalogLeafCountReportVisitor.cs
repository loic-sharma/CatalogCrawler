﻿using Knapcode.CatalogDownloader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    class CatalogLeafCountReportVisitor : ICsvAggregateReportVisitor<DateTimeOffset, int>
    {
        public string Name => "CatalogLeafCount";
        public IComparer<DateTimeOffset> KeyComparer => Comparer<DateTimeOffset>.Default;

        public int Merge(int existingValue, int newValue)
        {
            return existingValue + newValue;
        }

        public Task<IReadOnlyDictionary<DateTimeOffset, int>> OnCatalogPageAsync(CatalogPage catalogPage)
        {
            var result = catalogPage
                .Items
                .Select(x => x.CommitTimestamp.ToUniversalTime())
                .GroupBy(x => new DateTimeOffset(x.Year, x.Month, x.Day, x.Hour, 0, 0, TimeSpan.Zero))
                .ToDictionary(x => x.Key, x => x.Count());
            return Task.FromResult<IReadOnlyDictionary<DateTimeOffset, int>>(result);
        }
    }
}
