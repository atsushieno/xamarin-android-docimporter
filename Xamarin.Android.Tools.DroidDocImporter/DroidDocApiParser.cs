using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Xamarin.Android.Tools.DroidDocImporter
{
	public class DroidDocApiParser
	{
		public DroidDocApiParser()
		{
		}

		static readonly Regex rxSimpleParams = new Regex("\\((?<p>.*?)\\)", RegexOptions.Compiled | RegexOptions.Singleline);

		public IEnumerable<ApiObjectInfo> Run (string[] htmlFiles, string[] packageNameFilters, string packageNameDocPrefix = null)
		{
			Log.Info("Started Parsing {0} files...", htmlFiles.Length);

			var objects = new List<ApiObjectInfo>();

			//Parallel.ForEach(htmlFiles, (file) =>
			foreach (var file in htmlFiles)
			{
				//if (!file.EndsWith("Fragment.html"))
				//	continue; //return;
				
				HtmlDocument html = new HtmlDocument();
				html.Load(file);

				// Get all the <code> nodes where our method and parameter docs live
				// They will be inside <table> elements with class names containing 'constructors' or 'methods'
				// Skip this file if no code nodes are found
				var codeNodes = html.DocumentNode.SelectNodes("//table[contains(@id, 'pubctors') or contains(@id, 'proctors') or contains(@id, 'pubmethods') or contains(@id, 'pubctors')]//code");
				if (codeNodes == null || !codeNodes.Any())
					continue;//return;

				// The canonical url from the docs will give us the full java package name path + class/interface name
				// If not found, skip this file
				var canonical = html.DocumentNode.SelectSingleNode("//link[@rel='canonical']")?.GetAttributeValue("href", null);
				if (string.IsNullOrEmpty(canonical))
					continue; //return;

				// Made it this far, let's start setting up our info storage object
				var objectInfo = new ApiObjectInfo();
				objectInfo.IsInterface = (html.DocumentNode.SelectSingleNode("//code[@class='api-signature']")?.InnerText ?? "").Contains("interface");

				// We need to get only the path part of the url which contains the java package path + class/interface name
				// It also has a .html suffix which we need to deal with
				var objTypeInfo = GetPackageAndTypeName(canonical, packageNameDocPrefix);
				objectInfo.PackageName = objTypeInfo.Item1;
				objectInfo.TypeName = objTypeInfo.Item2;

				// If we have package name filters, make sure this package name starts with at least one of the filters
				if (packageNameFilters != null 
				    && packageNameFilters.Any() 
				    && !packageNameFilters.Any(pf => 
						objectInfo.PackageName.StartsWith(pf, StringComparison.OrdinalIgnoreCase)))
					continue; //return;

				// Inspect all of our code nodes
				foreach (HtmlNode n in codeNodes)
				{
					var methodInfo = new ApiMethodInfo();

					HtmlNode code = n;

					// The first <a> will have an href containing a # in the url if it's actually a method description
					var methodUrl = code.SelectSingleNode("a[contains(@href, '#')]");
					// We only care about methods, otherwise skip
					if (methodUrl == null)
						continue;

					var methodUrlHref = methodUrl.GetAttributeValue("href", "");

					string methodName = null;
					// Get the parameter types from the href of the method url
					// This gets us the full package name and type name of each parameter
					// but doesn't give us the name of the parameter
					var paramTypes = GetParameterTypes(methodUrlHref, out methodName);

					methodInfo.Name = methodName;

					// Next, match the code's inner text to get text of the method declaration
					// which does include the parameter names
					var simpleParamsMatch = rxSimpleParams.Match(code.InnerText);
					if (simpleParamsMatch != null && simpleParamsMatch.Success)
					{
						// Get the rx group which has the comma separated list of short type and param names
						var simpleParams = simpleParamsMatch?.Groups?["p"]?.Value;

						// Split up the list by commas, this will give us pairs of simple type names and param names
						var pairs = simpleParams.Split(',');

						if (pairs != null && pairs.Any())
						{
							// Adds all the parameters to our method info object
							// This will match up the complex parameter type name from the list we found earlier
							// to the actual name of the parameter and combine it into a new object
							methodInfo.Parameters.AddRange(pairs.Where (pair => !string.IsNullOrWhiteSpace (pair) && pair.Trim ().Contains (' ')).Select((pair, index) =>
							{
								var typeName = paramTypes?[index];
								var name = pair?.Trim()?.Split(' ')?[1];

								return new ApiParameterInfo
								{
									TypeName = typeName,
									Name = name
								};
							}));
						}
					}

					// Only add the method if it has a name and parameters, otherwise we aren't interested
					if (!string.IsNullOrEmpty(methodInfo.Name) && methodInfo.Parameters.Any())
						objectInfo.Methods.Add(methodInfo);

				}

				if (!string.IsNullOrEmpty(objectInfo.PackageName)
					&& objectInfo.Methods.Any()
					&& !string.IsNullOrEmpty(objectInfo.TypeName))
				{
					lock (objects)
						objects.Add(objectInfo);
				}

				html = null;
			}//);

			Log.Info("Done Parsing {0} files...", htmlFiles.Length);

			return objects;
		}

		static readonly Regex rxParams = new Regex("\\#(?<n>.*?)\\((?<p>.*?)\\)", RegexOptions.Compiled | RegexOptions.Singleline);

		List<string> GetParameterTypes(string url, out string name)
		{
			name = null;
			var paramList = rxParams.Match(url);

			if (paramList != null)
			{
				name = paramList.Groups?["n"]?.Value;

				var p = paramList.Groups?["p"]?.Value;
				if (!string.IsNullOrEmpty(p))
				{
					var items = p.Split(',')?.Select ((i) => i.Trim ());

					return items?.ToList() ?? new List<string>();
				}
			}

			return new List<string>();
		}

		Tuple<string, string> GetPackageAndTypeName(string url, string packageNameDocPrefix)
		{
			try
			{
				var uri = new Uri(url);
				var name = uri.AbsolutePath;

				if (!string.IsNullOrEmpty(packageNameDocPrefix) && name.StartsWith(packageNameDocPrefix, StringComparison.OrdinalIgnoreCase))
					name = name.Substring(packageNameDocPrefix.Length);

				if (name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
					name = name.Substring(0, name.Length - 5);

				var lastPart = name.LastIndexOf('/');
				var pkgName = name.Substring(0, lastPart).Replace("/", ".");
				var typeName = name.Substring(lastPart + 1).Replace("/", ".");

				return Tuple.Create(pkgName, typeName);
			}
			catch (Exception ex)
			{

			}

			return null;
		}
	}
}
