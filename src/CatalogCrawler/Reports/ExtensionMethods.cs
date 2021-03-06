﻿using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;

namespace Knapcode.CatalogCrawler
{
    static class ExtensionMethods
    {
        public static void SetDefaultConfiguration(this CsvReader reader)
        {
            reader.Configuration.TypeConverterOptionsCache.SetDefaultConfiguration();
        }

        public static void SetDefaultConfiguration(this CsvWriter writer)
        {
            writer.Configuration.NewLine = NewLine.Environment;
            writer.Configuration.TypeConverterOptionsCache.SetDefaultConfiguration();
        }

        public static void SetDefaultConfiguration(this TypeConverterOptionsCache cache)
        {
            var options = new TypeConverterOptions { Formats = new[] { "O" } };
            cache.AddOptions<DateTimeOffset>(options);
        }
    }
}
