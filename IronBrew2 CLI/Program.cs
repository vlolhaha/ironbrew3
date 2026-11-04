using System;
using System.IO;
using System.Text;
using IronBrew2;
using IronBrew2.Obfuscator;

namespace IronBrew2_CLI
{
	class Program
	{
		static int Main(string[] args)
		{
			if (args.Length != 1)
			{
				Console.Error.WriteLine("Usage: IronBrew2 <input.lua>");
				return 2;
			}

			string tempDirectory = Path.Combine(Path.GetTempPath(), "IronBrew2-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tempDirectory);
			try
			{
				if (!IB2.Obfuscate(tempDirectory, Path.GetFullPath(args[0]), new ObfuscationSettings(), out string err))
				{
					Console.Error.WriteLine("ERR: " + err);
					return 1;
				}

				File.Delete("out.lua");
				File.Move(Path.Combine(tempDirectory, "out.lua"), "out.lua");
				Console.WriteLine("Done!");
				return 0;
			}
			finally
			{
				Directory.Delete(tempDirectory, true);
			}
		}
	}
}