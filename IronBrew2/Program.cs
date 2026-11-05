using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using IronBrew2.Bytecode_Library.Bytecode;
using IronBrew2.Bytecode_Library.IR;
using IronBrew2.Obfuscator;
using IronBrew2.Obfuscator.Control_Flow;
using IronBrew2.Obfuscator.Encryption;
using IronBrew2.Obfuscator.VM_Generation;

namespace IronBrew2
{
	public static class IB2
	{
		public static Random Random = new Random();
		private static Encoding _fuckingLua = Encoding.GetEncoding(28591);

		private static string GetApplicationDirectory()
		{
			string entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
			if (!string.IsNullOrEmpty(entryAssemblyPath))
			{
				string assemblyDirectory = Path.GetDirectoryName(entryAssemblyPath);
				if (!string.IsNullOrEmpty(assemblyDirectory) &&
				    File.Exists(Path.Combine(assemblyDirectory, "Lua", "Minifier", "luasrcdiet.lua")))
					return assemblyDirectory;
			}

			using (Process process = Process.GetCurrentProcess())
			{
				string processPath = process.MainModule?.FileName;
				if (!string.IsNullOrEmpty(processPath))
				{
					string processDirectory = Path.GetDirectoryName(processPath);
					if (!string.IsNullOrEmpty(processDirectory) &&
					    File.Exists(Path.Combine(processDirectory, "Lua", "Minifier", "luasrcdiet.lua")))
						return processDirectory;
				}
			}

			return AppContext.BaseDirectory;
		}

		private static string FindLuaExecutable(string applicationDirectory, params string[] names)
		{
			string extension = Environment.OSVersion.Platform == PlatformID.Win32NT ? ".exe" : "";
			string[] directories = (Environment.GetEnvironmentVariable("PATH") ?? "")
				.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

			foreach (string name in names)
			{
				string executable = Path.HasExtension(name) ? name : name + extension;
				string localPath = Path.Combine(applicationDirectory, executable);
				if (File.Exists(localPath))
					return localPath;

				foreach (string directory in directories)
				{
					string path = Path.Combine(directory, executable);
					if (File.Exists(path))
						return path;
				}
			}

			throw new FileNotFoundException("Required Lua executable not found: " + string.Join(" or ", names));
		}

		private static string ReadProcessOutput(Process process)
		{
			Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
			Task<string> standardError = process.StandardError.ReadToEndAsync();
			process.WaitForExit();
			Task.WaitAll(standardOutput, standardError);

			return standardOutput.Result + standardError.Result;
		}

		public static bool Obfuscate(string path, string input, ObfuscationSettings settings, out string error)
		{
			try
			{
				error = "";
				Directory.CreateDirectory(path);

				string applicationDirectory = GetApplicationDirectory();
				string luac = FindLuaExecutable(applicationDirectory, "luac", "luac5.1");
				string luajit = FindLuaExecutable(applicationDirectory, "luajit");
				string minifier = Path.Combine(applicationDirectory, "Lua", "Minifier", "luasrcdiet.lua");

				if (!File.Exists(minifier))
					throw new FileNotFoundException("Lua minifier script not found.", minifier);

				string l = Path.Combine(path, "luac.out");

				if (!File.Exists(input))
					throw new Exception("Invalid input file.");

				Console.WriteLine("Checking file...");
				if (File.Exists(l))
					File.Delete(l);
				
				Process proc = new Process
				       {
					       StartInfo =
					       {
						       FileName  = luac,
						       Arguments = "-o \"" + l + "\" \"" + input + "\"",
						       UseShellExecute = false,
						       RedirectStandardError = true,
						       RedirectStandardOutput = true
					       }
				       };

				string err = "";
				
				proc.Start();
				err = ReadProcessOutput(proc);

				if (proc.ExitCode != 0 || !File.Exists(l))
				{
					error = string.IsNullOrWhiteSpace(err) ? "Lua compiler exited with code " + proc.ExitCode + "." : err;
					return false;
				}
				
				File.Delete(l);
				string t0 = Path.Combine(path, "t0.lua");
				
				Console.WriteLine("Stripping comments...");
				if (File.Exists(t0))
					File.Delete(t0);
				err = "";

				proc = new Process
				       {
					       StartInfo =
					       {
						       FileName = luajit,
						       Arguments =
							       "\"" + minifier + "\" --noopt-whitespace --noopt-emptylines --noopt-numbers --noopt-locals --noopt-strings --opt-comments \"" +
							       input                                                       +
							       "\" -o \""                                                  + t0 + "\"",
						       UseShellExecute        = false,
						       RedirectStandardError  = true,
						       RedirectStandardOutput = true
					       }
				       };

				proc.Start();
				err = ReadProcessOutput(proc);

				if (proc.ExitCode != 0 || !File.Exists(t0))
				{
					error = string.IsNullOrWhiteSpace(err) ? "Lua minifier exited with code " + proc.ExitCode + "." : err;
					return false;
				}

				string t1 = Path.Combine(path, "t1.lua");
				
				Console.WriteLine("Compiling...");

				File.WriteAllText(t1, new ConstantEncryption(settings, File.ReadAllText(t0, _fuckingLua)).EncryptStrings());
				if (File.Exists(l))
					File.Delete(l);
				err = "";
				proc = new Process
				       {
					       StartInfo =
					       {
						       FileName  = luac,
						       Arguments = "-o \"" + l + "\" \"" + t1 + "\"",
						       UseShellExecute = false,
						       RedirectStandardError = true,
						       RedirectStandardOutput = true
					       }
				       };

				proc.Start();
				err = ReadProcessOutput(proc);

				if (proc.ExitCode != 0 || !File.Exists(l))
				{
					error = string.IsNullOrWhiteSpace(err) ? "Lua compiler exited with code " + proc.ExitCode + "." : err;
					return false;
				}
				
				Console.WriteLine("Obfuscating...");

				Deserializer des    = new Deserializer(File.ReadAllBytes(l));
				Chunk lChunk = des.DecodeFile();

				if (settings.ControlFlow)
				{
					CFContext cf = new CFContext(lChunk);
					cf.DoChunks();
				}

				Console.WriteLine("Serializing...");
				
				//shuffle stuff
				//lChunk.Constants.Shuffle();
				//lChunk.Functions.Shuffle();

				ObfuscationContext context = new ObfuscationContext(lChunk);

				string t2 = Path.Combine(path, "t2.lua");
				string c = new Generator(context).GenerateVM(settings);

				//string byteLocal = c.Substring(null, "\n");
				//string rest = c.Substring("\n");

				File.WriteAllText(t2, c, _fuckingLua);

				string t3 = Path.Combine(path, "t3.lua");
				
				Console.WriteLine("Minifying...");
				if (File.Exists(t3))
					File.Delete(t3);
				
				proc = new Process
				       {
					       StartInfo =
					       {
						       FileName = luajit,
						       Arguments =
							       "\"" + minifier + "\" --maximum --opt-entropy --opt-emptylines --opt-eols --opt-numbers --opt-whitespace --opt-locals --noopt-strings \"" +
							       t2                                                                                                                                                +
							       "\" -o \"" + 
							        t3 + 
							       "\""
								,
					       }
				       };

				proc.Start();
				proc.WaitForExit();

				if (proc.ExitCode != 0 || !File.Exists(t3))
				{
					error = "Lua minifier exited with code " + proc.ExitCode + ".";
					return false;
				}
				
				Console.WriteLine("Watermark...");
				
				File.WriteAllText(Path.Combine(path, "out.lua"), @"--[[
IronBrew3:tm: obfuscation; Version 3.0.2

........................................................................................................................................................................................................
........................................................................................................................................................................................................
.....,,...,.............................................................................................................................................................................................
.... MMMMM,.............................................................................................................................................................................................
....MMMMMMM,............................................................................................................................................................................................
....MMMMMMM,............................................................................................................................................................................................
....,MMMMMO.............................................................................................................................................................................................
......,.................................................................................................................................................................................................
..................................................,,,,,,............................................Z$$.................................................................................................
...................................................:::::............................................MMMO................................................................................................
.....:???? ,.......:????....,.8MMMMM,.......,,,MMMMI???INMMM.,................,.?ZMMMMDI:,,.........MMM$................................................................................................
.....MMMMM?,.......MMMMM,,.OMMMMMMMM......, 7MM+?+++++++++?+DM$ .............MMMMMMMMMMMMMM ,,......MMM$................................................................................................
.....MMMMM?,.......MMMMM..NMMMMMMMMM.,...,$M7++++++++++++++++++M$ .........MMMMMMMMMMMMMMMMMN .,....MMM$................................................................................................
.....MMMMM?,.......MMMMMMMMMMM8..,,,.,..,MM?++++++++++++++++++++MM,,......MMMMMMMM~,.+MMMMMMMM......MMM$................................................................................................
.....MMMMM?,.......MMMMMMMMZ ,,.......MMMMMMMMMMMMMDZZZZMMMMMMMMMMMMM ...MMMMMM,,,....., MMMMMM.....MMM$................................,.,,............................................................
.....MMMMM?,.......MMMMMMM:............MMMMMMMMMMMMMMMMMMMMMMMMMMMMM....MMMMMD,...........MMMMMM.,..MMM$...:MMMMMMMM:,........8MMM:.,DMMMMM,......?MMMMMMMMI.........MMMM......... MMM,.........MMMI....
.....MMMMM?,.......MMMMMM+............,M?+MMMMMMMMMM++?DMMMMMMMMM?+M,...MMMMM,.............MMMMM,,..MMM$,NMMMMMMMMMMMM8,.,....MMMM,NMMMMMMM,..,,MMMMMMMMMMMMMM.,.....MMMM.........7MMM7.........MMM$....
.....MMMMM?,.......MMMMMM,............,M?++MMMMMMMM7++++MMMMMMMM$??MM,,+MMMMM,.............MMMMM=...MMM$,MMMZ...,?MMMMMM,.....MMMMMMMMM,......DMMMMM:,....MMMMMN,....MMMM.........7MMM7.........MMM$....
.....MMMMM?,.......MMMMMM.............MM+??+MMMMMMM?++++MMMMMMMD??+$M,.MMMMM?.............,MMMMM?...MMM$,M,.,...,,,,MMMMM,....MMMMMM,,,,....,MMMMM,..,....,.MDNN$....MMMM.........7MMM7.........MMM$....
.....MMMMM?,.......MMMMM?,............MM??++???????++++++?????+++++7M..$MMMM,.............,?MMMM.,..MMM$.............OMMMM....MMMMM.........$MMMM,....... MMMMMM.,...MMMM.........7MMM7.........MMM$....
.....MMMMM?,.......MMMMM=,............NM?+++++++++++++++++++++++++?$M..MMMMM+,............,+MMMM+,..NMN$..............MMMM+,..MMMMM.........MMMM......,?MMMMM?.,.....MMMM.........7MMM7.........MMM$....
.....MMMMM?,.......MMMMM,,............,M+?+++++++?++++++++?+?++++++M7,,DMMMM:...............MMMM:,..MMMN.,............$MMM7...MMMM=.........MMMM....,DMMMMM..........MMMM.........7MMM7.........MMM$....
.....MMMMM?,.......MMMMM,,............:M$?++++?MM+++++++++DM?+++++?M,,,DMMMM+,..............MMMM+,..MMMM.,............?NMM?,..ZMMM,,........MMMM.,.MMMMMM,,..........MMMM,........7MMM7.........MMM$....
.....MMMMM?,.......MMMMM,,.............,M=++++++DMD++?++DMM+++++++M:...$MMMM.,..............MMMM ,..MMMM..............OMMM,,..OMMM,,........MMMM.,MMMM?,,......MNZ,,,MMMM.........IMMM?,........MMM?....
.....MMMMM?,.......MMMMM,,...............M+?+++++?+ZMMMN+++?+++++M7,...$MMMM................MMMM.,..=MMMN,..........,,MNMM.,..OMMM,.........?MMMI.,M..........,MMM,.,NMMM,........IMMMI.........MMM?....
.....MMMMM?,.......MMMMM,,................M7+?+++++++++++++++++IM,,....$MMMM,...............MMMM,....MMMMN.,......,,.MMMM,....OMMM,,........,MMMMN..........,+MMM,...,MMMN,.....,,MMMMM,,,.....MMMM.....
.....MMMMM?,.......MMMMM,,................,MM++++?++++++++????MM.......$MMMM,...............MMMM,.....MNMMM$,......MMMMM .....OMMM,,..........MMMMM~......,,MMMM ,....MMMMM,,,..~MMMMMMM~,,,.,MMMMM.....
.....MMMMM?,.......MMMMM,,.................,,MMD+++++++++++$MM,.,......$MMMM,...............MMMM,.....,+NMMMMMMMMMMMMMM..,....OMMM,,.......... +NMMMMMMMMMMMMMM,.......MMMMMMMMMMMMN,NMMMMMMMMMMMN,.....
.....MMMMM?........MMMMM,,.....................::MMMMMMMMM$.,.........,ZMMMM,,..............MMMM,,......, MMMMMMMMMM:.,,......+MMM................MMMMMMMMMM7,,,......,.,MMMMMMMMN.:...MMMMMMMMM,,......
..........,.......,,.....,.........................,,,,.,...................................................,.,,.,,,...........,,,..................,,..,,,,..............,,..,,,.......,,.,,,,.........
........................................................................................................................................................................................................
]]

" + File.ReadAllText(t3, _fuckingLua).Replace("\n", " "), _fuckingLua);
				return true;
			}
			catch (Exception e)
			{
				Console.WriteLine("ERROR");
				Console.WriteLine(e);

				error = e.ToString();
				return false;
			}
		}
	}
}