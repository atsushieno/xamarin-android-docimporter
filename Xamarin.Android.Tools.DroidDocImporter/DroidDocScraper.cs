using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools.DroidDocImporter
{
	public class DroidDocScraper
	{
		static HttpClient http = new HttpClient();

		public DroidDocScraper(System.IO.DirectoryInfo outputPath, Uri docsUrlBase, string[] packageFilters = null, string packageListFileOrUrl = null)
		{
			OutputPath = outputPath;
			DocsUrlBase = docsUrlBase;
			PackageListFileOrUrl = packageListFileOrUrl ?? docsUrlBase.AbsoluteUri.TrimEnd ('/') + "/package-list";
			PackageFilters = packageFilters ?? new string[] { };
		}

		public DirectoryInfo OutputPath { get; private set; }
		public Uri DocsUrlBase { get; private set; }
		public string PackageListFileOrUrl { get; private set; }
		public string[] PackageFilters { get; private set; }

		System.Collections.Concurrent.BlockingCollection<LinkToDownload> linksToDownload = new System.Collections.Concurrent.BlockingCollection<LinkToDownload>();

		int linksFound = 0;
		int linksDownloaded = 0;

		List<string> alreadyDownloaded = new List<string>();

		public async Task Scrape(int threads = 1)
		{
			Log.Info("Started scraping {0} ({1} threads)", DocsUrlBase, threads);

			var downloadTasks = new List<Task>();
			for (int i = 0; i < threads; i++)
			{
				downloadTasks.Add(Task.Run(async () =>
				{
					foreach (var link in linksToDownload.GetConsumingEnumerable())
					{
						await DownloadLink(link.Url, link.LocalFile);
						linksDownloaded++;

						if (linksDownloaded % 100 == 0)
							Log.Info("{0}/{1} Links Downloaded", linksDownloaded, linksFound);
					}
				}));
			}

			var packages = await GetPackageListAsync();

			//Parallel.ForEach (packages, async (package) => {
			foreach (var package in packages)
			{
				// if filters were specified, make sure our package matches at least one of them
				if (PackageFilters.Any() && !PackageFilters.Any(pf => 
				                                                package.Replace ("/", ".").StartsWith (pf.Replace ("/", "."), StringComparison.OrdinalIgnoreCase) ||
				                                                package.StartsWith(pf, StringComparison.OrdinalIgnoreCase)))
					continue;

				var packageLinkStart = DocsUrlBase.AbsoluteUri.TrimEnd('/') + "/" + package;

				var summaryFile = await DownloadPackageSummary(package);

				var links = ParseLinks(summaryFile);

				var packageLinks = links.Where(l => l.StartsWith(packageLinkStart, StringComparison.InvariantCultureIgnoreCase));

				foreach (var packageLink in packageLinks)
				{

					lock (alreadyDownloaded)
					{
						if (alreadyDownloaded.Any (ad => ad.Equals (packageLink, StringComparison.OrdinalIgnoreCase)))
							continue;

						alreadyDownloaded.Add(packageLink);
					}

					var pkgUri = new Uri(packageLink);

					var outputFile = new FileInfo(OutputPath.FullName.TrimEnd(Path.DirectorySeparatorChar)
												   + Path.DirectorySeparatorChar
												   + pkgUri.AbsolutePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));

					if (outputFile.Exists)
						continue;

					if (!Directory.Exists(outputFile.FullName))
						Directory.CreateDirectory(outputFile.Directory.FullName);

					linksToDownload.Add(new LinkToDownload { Url = packageLink, LocalFile = outputFile });
					linksFound++;
					//await DownloadLink (packageLink, outputFile);
				}
			}//);

			linksToDownload.CompleteAdding();

			Log.Info("Done Discovering. {0} Links to Download", linksFound);

			await Task.WhenAll(downloadTasks);

			Log.Info("Done Scraping.");
		}

		async Task DownloadLink(string url, FileInfo localFile)
		{
			try
			{
				using (var urlStream = await http.GetStreamAsync(url))
				using (var sw = File.Open(localFile.FullName, FileMode.Create))
				{
					await urlStream.CopyToAsync(sw);
				}
			}
			catch { }
		}

		async Task<FileInfo> DownloadPackageSummary(string package)
		{
			const string PACKAGE_SUMMARY_FILE = "package-summary.html";

			var url = DocsUrlBase.AbsoluteUri.TrimEnd('/') + "/" + package + "/" + PACKAGE_SUMMARY_FILE;
			var localFile = new FileInfo(Path.Combine(OutputPath.FullName, "reference", package, PACKAGE_SUMMARY_FILE));

			if (!Directory.Exists(localFile.FullName))
				Directory.CreateDirectory(localFile.Directory.FullName);

			await DownloadLink(url, localFile);

			return localFile;
		}

		Regex rxLinks = new Regex("href=\"(?<href>.*?)\"", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

		IEnumerable<string> ParseLinks(FileInfo file)
		{
			if (!File.Exists(file.FullName))
				yield break;
			var html = File.ReadAllText(file.FullName);

			var matches = rxLinks.Matches(html);

			foreach (Match match in matches)
			{

				var link = match?.Groups["href"]?.Value;

				if (!string.IsNullOrEmpty(link))
					yield return link;
			}
		}

		async Task<List<string>> GetPackageListAsync()
		{
			var packages = new List<string>();
			string line = null;

			Uri uriResult;
			var isUri = Uri.TryCreate(PackageListFileOrUrl, UriKind.Absolute, out uriResult);
			
			if (isUri)
			{
				using (var urlStream = await http.GetStreamAsync(uriResult))
				using (var sr = new System.IO.StreamReader(urlStream))
				{
					while (!string.IsNullOrEmpty(line = await sr.ReadLineAsync()))
					{
						packages.Add(line.Replace('.', '/'));
					}
				}
			}
			else
			{
				using (var sr = new StreamReader(File.OpenRead(PackageListFileOrUrl)))
				{
					while (!string.IsNullOrEmpty(line = await sr.ReadLineAsync()))
					{
						packages.Add(line.Replace('.', '/'));
					}
				}
			}

			return packages;
		}

		struct LinkToDownload
		{
			public string Url;
			public FileInfo LocalFile;
		}
	}

}
