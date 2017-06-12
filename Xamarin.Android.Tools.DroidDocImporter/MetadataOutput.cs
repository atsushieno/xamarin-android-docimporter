using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.CSharp;

namespace Xamarin.Android.Tools.DroidDocImporter
{
	public class MetadataOutput
	{
		public MetadataOutput()
		{
		}

		CSharpCodeProvider codeProvider = new CSharpCodeProvider();

		string SafeParameterName(string paramName)
		{
			if (!codeProvider.IsValidIdentifier(paramName))
				return "@" + paramName;
			return paramName;
		}

		public void ToTransformsMetadataXml(string outputFile, IEnumerable<ApiObjectInfo> objects)
		{
			using (var file = File.Open(outputFile, FileMode.Create))
			using (var sw = new StreamWriter(file))
			{
				sw.WriteLine ("<metadata>");

				foreach (var item in objects)
				{
					foreach (var method in item.Methods)
					{
						var pIndex = 0;

						//<attr path="/api/package[@name='com.google.android.gms.common.api']/class[@name='ResolvingResultCallbacks']/method[@name='onSuccess' and count(parameter)=1 and parameter[1][@type='R']]" name="visibility">
						var pstr = string.Join(" and ", method.Parameters.Select((p, index) => {
							var pkgName = SecurityElement.Escape(p.PackageName);
							var typeName = SecurityElement.Escape(p.TypeName);
							if (string.IsNullOrEmpty(p.PackageName))
								return $"parameter[{index + 1}][@type='{typeName}']";
							else
								return $"parameter[{index + 1}][@type='{pkgName}.{typeName}']";
						}));

						if (!string.IsNullOrEmpty(pstr))
							pstr = " and " + pstr;

						foreach (var param in method.Parameters)
						{
							pIndex++;
							var cOrI = item.IsInterface ? "interface" : "class";
							var pCount = method.Parameters.Count.ToString();

							var pkgName = SecurityElement.Escape(item.PackageName);
							var typeName = SecurityElement.Escape(item.TypeName);
							var methodName = SecurityElement.Escape(method.Name);
							var paramName = SafeParameterName (SecurityElement.Escape(param.Name));

							var xpath = $"/api/package[@name='{pkgName}']/{cOrI}[@name='{typeName}']/method[@name='{methodName}' and count(parameter)={pCount}{pstr}]/parameter[{pIndex}]";

							var md = $"  <attr path=\"{xpath}\" name=\"managedName\">{paramName}</attr>";

							sw.WriteLine(md);
						}

						pIndex++;
					}
				}

				sw.WriteLine ("</metadata>");
			}
		}


		public void ToDescriptiveXml(string outputFile, IEnumerable<ApiObjectInfo> objects)
		{
			using (var xml = System.Xml.XmlWriter.Create(outputFile))
			{
				xml.WriteStartDocument(true);
				xml.WriteStartElement("api");

				var packages = objects.GroupBy((item) => item.PackageName);

				foreach (var pkg in packages)
				{
					xml.WriteStartElement("package");
					xml.WriteAttributeString("name", pkg.Key);

					foreach (var type in pkg)
					{
						xml.WriteStartElement(type.IsInterface ? "interface" : "class");
						xml.WriteAttributeString("name", type.TypeName);

						foreach (var method in type.Methods)
						{
							var isCtor = method.Name.Equals(type.TypeName, StringComparison.Ordinal);

							xml.WriteStartElement(isCtor ? "constructor" : "method");
							xml.WriteAttributeString("name", method.Name);

							foreach (var param in method.Parameters)
							{
								xml.WriteStartElement("parameter");

								xml.WriteAttributeString("name", SafeParameterName (param.Name));

								var typeName = string.Empty;
								if (!string.IsNullOrEmpty(param.PackageName))
									typeName += param.PackageName + ".";
								typeName += param.TypeName;

								xml.WriteAttributeString("type", typeName);

								xml.WriteEndElement(); // parameter
							}

							xml.WriteEndElement(); // method
						}

						xml.WriteEndElement(); // type

					}

					xml.WriteEndElement(); //package

				}

				xml.WriteEndElement(); // packages

				xml.WriteEndDocument();

			}
		}


	}
}
