using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.CatalogCrawler
{
    public class DownloadCommandHandlerIntegrationTests : IClassFixture<DefaultWebApplicationFactory<StaticFilesStartup>>, IDisposable
    {
        private const string Step1 = "TestData/Step1";
        private const string Step2a = "TestData/Step2a";
        private const string Step2b = "TestData/Step2b";
        private const string Step3 = "TestData/Step3";
        private const string Step4 = "TestData/Step4";

        private readonly DepthLogger _logger;
        private readonly DefaultWebApplicationFactory<StaticFilesStartup> _factory;
        private readonly TestDirectory _testDir;
        private readonly string _dataDir;
        private readonly string _webRoot;
        private readonly WebApplicationFactory<StaticFilesStartup> _builder;
        private readonly ConcurrentQueue<string> _paths;
        private HttpClient _httpClient;
        private readonly DataDirectoryHelper _dd;

        private int? _maxPages;
        private int? _maxCommits;

        private int _expectedRequestCount;

        public DownloadCommandHandlerIntegrationTests(
            ITestOutputHelper output,
            DefaultWebApplicationFactory<StaticFilesStartup> factory)
        {
            _logger = new DepthLogger(new TestLogger(output));
            _factory = factory;
            _testDir = new TestDirectory();
            _dataDir = Path.Combine(_testDir, "data");
            _webRoot = Path.Combine(_testDir, "wwwroot");

            _builder = _factory.WithWebHostBuilder(b => b
                .ConfigureLogging(b => b.SetMinimumLevel(LogLevel.Error))
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseWebRoot(_webRoot));
            _paths = _builder.Services.GetRequiredService<ConcurrentQueue<string>>();
            _httpClient = _builder.CreateClient();

            _dd = new DataDirectoryHelper(_dataDir, DownloadDepth.CatalogLeaf, "localhost");
        }

        private static IEnumerable<DownloadDepth> AllDepths => Enum
            .GetValues(typeof(DownloadDepth))
            .Cast<DownloadDepth>();

        public static IEnumerable<object[]> CatalogIndexAndDeeperTestData => AllDepths
            .Where(x => x >= DownloadDepth.CatalogIndex)
            .Select(x => new object[] { x });

        public static IEnumerable<object[]> CatalogPageAndDeeperTestData => AllDepths
            .Where(x => x >= DownloadDepth.CatalogPage)
            .Select(x => new object[] { x });

        public static IEnumerable<object[]> AllDepthsTestData => AllDepths
            .Select(x => new object[] { x });

        public void Dispose()
        {
            _httpClient.Dispose();
            _builder.Dispose();
            _testDir.Dispose();
        }

        [Theory]
        [MemberData(nameof(CatalogIndexAndDeeperTestData))]
        public async Task VerifyStep123And4_MaxPages2(DownloadDepth depth)
        {
            _dd.Depth = depth;
            _maxPages = 2;

            CopyFilesToWebRoot(Step1);
            CopyFilesToWebRoot(Step2a);
            CopyFilesToWebRoot(Step2b);
            CopyFilesToWebRoot(Step3);
            CopyFilesToWebRoot(Step4);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step4, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2a, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogPage, Step4, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step4, "catalog/2020.10.23.00.00.00/c.1.0.0.json", "catalog/2020/10/23/00/00.00/c.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step4, "catalog/2020.10.24.00.00.00/c.2.0.0.json", "catalog/2020/10/24/00/00.00/c.2.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-24T00:00:00+00:00\"");

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step4, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step4, "catalog/page2.json", "catalog/page0-page499/page2.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step4, "catalog/2020.10.25.00.00.00/c.3.0.0.json", "catalog/2020/10/25/00/00.00/c.3.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-25T00:00:00+00:00\"");
        }

        [Theory]
        [MemberData(nameof(CatalogPageAndDeeperTestData))]
        public async Task VerifyStep123And4_MaxCommits4(DownloadDepth depth)
        {
            _dd.Depth = depth;
            _maxCommits = 4;

            CopyFilesToWebRoot(Step1);
            CopyFilesToWebRoot(Step2a);
            CopyFilesToWebRoot(Step2b);
            CopyFilesToWebRoot(Step3);
            CopyFilesToWebRoot(Step4);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step4, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2a, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogPage, Step4, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step4, "catalog/2020.10.23.00.00.00/c.1.0.0.json", "catalog/2020/10/23/00/00.00/c.1.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-23T00:00:00+00:00\"");

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step4, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step4, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogPage, Step4, "catalog/page2.json", "catalog/page0-page499/page2.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step4, "catalog/2020.10.24.00.00.00/c.2.0.0.json", "catalog/2020/10/24/00/00.00/c.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step4, "catalog/2020.10.25.00.00.00/c.3.0.0.json", "catalog/2020/10/25/00/00.00/c.3.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-25T00:00:00+00:00\"");
        }

        [Theory]
        [MemberData(nameof(AllDepthsTestData))]
        public async Task VerifyStep12And3(DownloadDepth depth)
        {
            _dd.Depth = depth;

            CopyFilesToWebRoot(Step1);
            CopyFilesToWebRoot(Step2a);
            CopyFilesToWebRoot(Step2b);
            CopyFilesToWebRoot(Step3);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step3, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2a, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogPage, Step3, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-22T00:00:00+00:00\"");
        }

        [Theory]
        [MemberData(nameof(AllDepthsTestData))]
        public async Task VerifyStep1_ThenStep2And3(DownloadDepth depth)
        {
            _dd.Depth = depth;

            await VerifyStep1Async();

            CopyFilesToWebRoot(Step2a);
            CopyFilesToWebRoot(Step2b);
            CopyFilesToWebRoot(Step3);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step3, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2a, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogPage, Step3, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-22T00:00:00+00:00\"");
        }

        [Theory]
        [MemberData(nameof(AllDepthsTestData))]
        public async Task VerifyStep1And2_ThenStep3(DownloadDepth depth)
        {
            _dd.Depth = depth;

            CopyFilesToWebRoot(Step1);
            CopyFilesToWebRoot(Step2a);
            CopyFilesToWebRoot(Step2b);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step2b, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2a, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-21T00:00:00+00:00\"");

            await VerifyStep3Async();
        }

        [Theory]
        [MemberData(nameof(AllDepthsTestData))]
        public async Task VerifyStep1And2a_ThenStep2bAnd3(DownloadDepth depth)
        {
            _dd.Depth = depth;

            CopyFilesToWebRoot(Step1);
            CopyFilesToWebRoot(Step2a);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step1, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2a, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-20T00:00:00+00:00\"");

            CopyFilesToWebRoot(Step2b);
            CopyFilesToWebRoot(Step3);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step3, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2a, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogPage, Step3, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-22T00:00:00+00:00\"");
        }

        [Theory]
        [MemberData(nameof(AllDepthsTestData))]
        public async Task VerifyStep1_ThenStep2_ThenStep3(DownloadDepth depth)
        {
            _dd.Depth = depth;

            await VerifyStep1Async();

            CopyFilesToWebRoot(Step2a);
            CopyFilesToWebRoot(Step2b);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step2b, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2a, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2a, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-21T00:00:00+00:00\"");

            await VerifyStep3Async();
        }

        private async Task VerifyStep1Async()
        {
            CopyFilesToWebRoot(Step1);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step1, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step1, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-20T00:00:00+00:00\"");
        }

        private async Task VerifyStep3Async()
        {
            CopyFilesToWebRoot(Step3);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step3, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step3, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertRequestCount();
            _dd.AssertDownloadCursor("catalog", "\"2020-10-22T00:00:00+00:00\"");
        }

        private async Task ExecuteAsync()
        {
            await DownloadCommandHandler.ExecuteAsync(
                httpClient: _httpClient,
                serviceIndexUrl: "https://localhost/index.json",
                dataDir: _dataDir,
                depth: _dd.Depth,
                jsonFormatting: JsonFormatting.Pretty,
                maxPages: _maxPages,
                maxCommits: _maxCommits,
                formatPaths: true,
                parallelDownloads: 1,
                logger: _logger);
        }

        private void AssertRequestCount()
        {
            Assert.Equal(_expectedRequestCount, _paths.Count);
            _expectedRequestCount = 0;
            _paths.Clear();
        }

        private void AssertDownload(DownloadDepth depth, string testDataDir, string requestPath, string filePath = null)
        {
            if (depth <= _dd.Depth)
            {
                Assert.Contains('/' + requestPath, _paths);
                _expectedRequestCount++;

                _dd.AssertTestData(testDataDir, requestPath, filePath);
            }
        }

        private void CopyFilesToWebRoot(string testDataDir)
        {
            var srcDir = Path.GetFullPath(testDataDir);
            foreach (var srcFile in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                var destFile = Path.Combine(_webRoot, srcFile.Substring(srcDir.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(srcFile, destFile, overwrite: true);
            }
        }
    }
}
