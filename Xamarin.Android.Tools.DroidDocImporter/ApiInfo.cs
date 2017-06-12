using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tools.DroidDocImporter
{
	public class ApiObjectInfo
	{
		public ApiObjectInfo()
		{
			Methods = new List<ApiMethodInfo>();
		}

		public bool IsInterface { get; set; }
		public string PackageName { get; set; }
		public string TypeName { get; set; }

		public List<ApiMethodInfo> Methods { get; set; }
	}

	public class ApiMethodInfo
	{
		public ApiMethodInfo()
		{
			Parameters = new List<ApiParameterInfo>();
		}

		public string Name { get; set; }
		public List<ApiParameterInfo> Parameters { get; set; }
		public bool IsConstructor { get; set; }

		public override string ToString()
		{
			var pstr = string.Join(", ", Parameters);
			return string.Format("{0} ({1})", Name, pstr);
		}
	}

	public class ApiParameterInfo
	{
		public ApiParameterInfo()
		{
		}

		public string Name { get; set; }
		public string PackageName { get; set; }
		public string TypeName { get; set; }

		public override string ToString()
		{
			return string.Format("{0}/{1} {2}", PackageName, TypeName, Name);
		}
	}
}
