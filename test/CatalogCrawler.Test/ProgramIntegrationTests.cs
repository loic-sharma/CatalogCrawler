﻿using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.CatalogCrawler
{
    public class ProgramIntegrationTests
    {
        private const string NuGetOrg = "TestData/api.nuget.org";

        private readonly TestDirectory _testDir;
        private readonly string _dataDir;
        private readonly DataDirectoryHelper _dd;

        public ProgramIntegrationTests(ITestOutputHelper output)
        {
            Program.WriteLine = s => output.WriteLine(s);
            _testDir = new TestDirectory();
            _dataDir = Path.Combine(_testDir, "data");
            _dd = new DataDirectoryHelper(_dataDir, DownloadDepth.CatalogLeaf, "api.nuget.org");
        }

        [Fact]
        public async Task VerifyDownloadAgainstNuGetOrg()
        {
            var exitCode = await Program.Main(new[]
            {
                "download",
                "--data-dir", _dataDir,
                "--json-formatting", "Pretty",
                "--max-commits", "1",
                "--parallel-downloads", "4",
            });

            Assert.Equal(0, exitCode);
            _dd.AssertFileExists("v3/index.json");
            _dd.AssertFileExists("v3/catalog0/index.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/page0.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/adam.jsgenerator.1.1.0.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/agatha-rrsl.1.2.0.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/altairis.mailtoolkit.1.0.0.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/altairis.web.security.2.0.0.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/altairis.web.ui.2.0.0.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/antixss.4.0.1.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/antlr.3.1.1.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/antlr.3.1.3.42154.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/argotic.common.2008.0.2.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/argotic.core.2008.0.2.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/argotic.extensions.2008.0.2.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/argotic.web.2008.0.2.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/artem.xmlproviders.2.5.0.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/ashmind.extensions.1.0.3.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/attributerouting.0.5.3967.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/autofac.2.2.4.900.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/autofac.2.3.2.632.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/autofac.mvc2.2.2.4.900.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/autofac.web.2.2.4.900.json");
            _dd.AssertTestData(NuGetOrg, "v3/catalog0/data/2015.02.01.06.22.45/autofac.web.2.3.2.632.json");
            _dd.AssertCursor("v3/catalog0", "\"2015-02-01T06:22:45.8488496+00:00\"");
        }
    }
}
