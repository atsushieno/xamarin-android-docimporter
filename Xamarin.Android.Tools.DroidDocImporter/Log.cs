using System;
namespace Xamarin.Android.Tools.DroidDocImporter
{
	public static class Log
	{
		public static void Info(string format, params object[] args)
		{
			Console.WriteLine(format, args);
		}
	}
}
