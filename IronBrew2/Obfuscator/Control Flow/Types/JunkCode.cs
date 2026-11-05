using System;
using System.Collections.Generic;
using IronBrew2.Bytecode_Library.Bytecode;
using IronBrew2.Bytecode_Library.IR;

namespace IronBrew2.Obfuscator.Control_Flow.Types
{
	public static class JunkCode
	{
		public static Random      Random      = new Random();
		public static CFGenerator CFGenerator = new CFGenerator();

		public static void DoChunk(Chunk chunk, int min, int max)
		{
			if (chunk.Instructions.Count > 0)
			{
				// Những vị trí KHÔNG được chèn junk vào trước đó, vì sẽ tách
				// rời 1 cặp lệnh bắt buộc liền kề nhau theo semantics Lua
				// (copy lại đúng logic skip[] mà Generator.cs dùng cho
				// super-operator, để không bị vỡ 2 nơi theo 2 kiểu khác nhau).
				bool[] noInsertBefore = new bool[chunk.Instructions.Count + 1];

				for (int i = 0; i < chunk.Instructions.Count; i++)
				{
					Instruction ins = chunk.Instructions[i];

					switch (ins.OpCode)
					{
						case Opcode.Closure:
						{
							Chunk proto = (Chunk) ins.RefOperands[0];
							for (int j = 1; j <= proto.UpvalueCount; j++)
								if (i + j < noInsertBefore.Length)
									noInsertBefore[i + j] = true;
							break;
						}

						case Opcode.Eq:
						case Opcode.Lt:
						case Opcode.Le:
						case Opcode.Test:
						case Opcode.TestSet:
						case Opcode.TForLoop:
						case Opcode.SetList:
						case Opcode.LoadBool when ins.C != 0:
							if (i + 1 < noInsertBefore.Length)
								noInsertBefore[i + 1] = true;
							break;
					}
				}

				chunk.Rebase(2);

				int scratchA = chunk.ParameterCount;
				int scratchB = chunk.ParameterCount + 1;

				int count = Random.Next(min, max + 1);

				for (int i = 0; i < count; i++)
				{
					List<int> valid = new List<int>();
					for (int p = 1; p < noInsertBefore.Length; p++)
						if (!noInsertBefore[p])
							valid.Add(p);

					if (valid.Count == 0)
						break;

					int pos = valid[Random.Next(valid.Count)];

					Instruction junk = CFGenerator.SafeJunk(chunk, scratchA, scratchB);
					chunk.Instructions.Insert(pos, junk);

					bool[] shifted = new bool[noInsertBefore.Length + 1];
					Array.Copy(noInsertBefore, 0, shifted, 0, pos);
					Array.Copy(noInsertBefore, pos, shifted, pos + 1, noInsertBefore.Length - pos);
					noInsertBefore = shifted;
				}

				chunk.UpdateMappings();
			}

			foreach (Chunk sub in chunk.Functions)
				DoChunk(sub, min, max);
		}
	}
}
