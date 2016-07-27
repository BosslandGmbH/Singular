using System;
using System.IO;
using System.Reflection;

namespace FileCheckListGenerator
{
	class Program
	{
		static void Main(string[] args)
		{
			string singularFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "..",
				"..", "Singular");
			FileCheckList fileCheckList = new FileCheckList();

			fileCheckList.Generate(singularFolder);

			fileCheckList.Save(Path.Combine(singularFolder, "Singular.xml"));
		}
	}
}
