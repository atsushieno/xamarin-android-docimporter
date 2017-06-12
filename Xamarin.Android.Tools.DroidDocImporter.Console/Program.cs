using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;

namespace Xamarin.Android.Tools.DroidDocImporter
{
	class MainClass
	{
		public static int Main(string[] args)
		{
			return CommandLine.Parser.Default.ParseArguments<ScrapeOptions, TransformOptions>(args)
				.MapResult(
					(ScrapeOptions opt) => {
						Scrape(opt);
						return 0;
					},
					(TransformOptions opt) => {
						Transform(opt);
						return 0;
					},
					(errs) => 1);
	  

			var supportUrl = "https://developer.android.com/reference/";
			var supportFilter = "android/support";
			var supportPackageList = "/Users/redth/Projects/AndroidDocScraper/AndroidDocScraper/bin/Debug/support-package-list";

			var gpsUrl = "https://developers.google.com/android/reference/";
			var gpsPackageList = "/Users/redth/Projects/AndroidDocScraper/AndroidDocScraper/bin/Debug/gps-package-list";
			var gpsFilter = string.Empty;

		}

		public static void Scrape(ScrapeOptions opt)
		{
			if (string.IsNullOrEmpty(opt.OutputDirectory))
				throw new DirectoryNotFoundException("Output Directory not specified");

			var outputDir = new DirectoryInfo(opt.OutputDirectory);
			if (!outputDir.Exists)
				outputDir.Create();

			var uri = new Uri(opt.Url);

			var scraper = new DroidDocScraper(outputDir, uri, opt.PackageNameFilters?.ToArray() ?? new string[] { }, opt.PackageListSource);

			scraper.Scrape(opt.Threads).Wait();
		}

		static void Transform(TransformOptions opt)
		{
			var dirInfo = new DirectoryInfo(opt.InputDirectory);
			if (!dirInfo.Exists)
				throw new DirectoryNotFoundException(opt.InputDirectory);

			var files = Directory.GetFiles(dirInfo.FullName, "*.html", SearchOption.AllDirectories);

			var parser = new DroidDocApiParser();

			var apiInfos = parser.Run(
				files, 
				opt.PackageNameFilters?.ToArray() ?? new string[] { },
				opt.UrlPackageNamePrefix);

			var metadata = new MetadataOutput();

			if (opt.OutputType == ParseOutputType.Metadata)
			{
				metadata.ToTransformsMetadataXml(opt.OutputFile, apiInfos);
			}
			else if (opt.OutputType == ParseOutputType.Xml)
			{
				metadata.ToDescriptiveXml(opt.OutputFile, apiInfos);
			}

		}
	}

	[Verb ("scrape", HelpText = "Scrape Android Docs from a website")]
	class ScrapeOptions
	{
		[Option ('u', "url", Required = true, HelpText = "Url to scrape docs from")]
		public string Url { get; set; }

		[Option('o', "out", Required = true, HelpText = "Local directory to save the scraped docs to")]
		public string OutputDirectory { get; set; }

		[Option('s', "package-list-source", HelpText = "An alternate file or url to load package-list contents from")]
		public string PackageListSource { get; set; }

		[Option ('f', "package-filter", HelpText = "Only process package names that start with one of these filters")]
		public IEnumerable<string> PackageNameFilters { get; set; }

		[Option ('t', "threads", Default = 1, HelpText = "Number of threads to use concurrently")]
		public int Threads { get; set; }
	}

	[Verb("transform", HelpText = "Transforms Android Docs API Parameter info into an alternate format")]
	class TransformOptions
	{
		[Option('d', "dir", Required = true, HelpText = "Directory with Android Docs to Parse")]
		public string InputDirectory { get; set; }

		[Option('o', "out", Required = true, HelpText = "Local directory to save the scraped docs to")]
		public string OutputFile { get; set; }
		
		[Option('t', "type", Required = true, HelpText = "Type of file to parse to")]
		public ParseOutputType OutputType { get; set; }

		[Option('p', "prefix", Required = true, HelpText = "Prefix in the doc url for a given package name which is after the base url, but should be stripped out since it's not part of the package name itself, eg: /reference/")]
		public string UrlPackageNamePrefix { get; set; }

		[Option('f', "package-filter", HelpText = "Only process package names that start with one of these filters")]
		public IEnumerable<string> PackageNameFilters { get; set; }
	}

	public enum ParseOutputType
	{
		Metadata,
		Xml
	}
}
