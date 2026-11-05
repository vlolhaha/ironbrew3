using System;
using System.Linq;
using IronBrew2.Bytecode_Library.Bytecode;
using IronBrew2.Bytecode_Library.IR;

namespace IronBrew2.Obfuscator.Control_Flow
{
	public class CFGenerator
	{
		public Random Random = new Random();

		public Instruction NextJMP(Chunk lc, Instruction Reference) =>
			new Instruction(lc, Opcode.Jmp, Reference);

		public Instruction BelievableRandom(Chunk lc)
		{
			Instruction ins = new Instruction(lc, (Opcode)Random.Next(0, 37));

			ins.A = Random.Next(0, 128);
			ins.B = Random.Next(0, 128);
			ins.C = Random.Next(0, 128);

			while (true)
			{
				switch (ins.OpCode)
				{
					case Opcode.LoadConst:
					case Opcode.GetGlobal:
					case Opcode.SetGlobal:
					case Opcode.Jmp:
					case Opcode.ForLoop:
					case Opcode.TForLoop:
					case Opcode.ForPrep:
					case Opcode.Closure:
					case Opcode.GetTable:
					case Opcode.SetTable:
					case Opcode.Add:
					case Opcode.Sub:
					case Opcode.Mul:
					case Opcode.Div:
					case Opcode.Mod:
					case Opcode.Pow:
					case Opcode.Test:
					case Opcode.TestSet:
					case Opcode.Eq:
					case Opcode.Lt:
					case Opcode.Le:
					case Opcode.Self:
						ins.OpCode = (Opcode) Random.Next(0, 37);
						continue;

					default:
						return ins;
				}
			}
		}
		
		// Opcode nào không bao giờ ném lỗi runtime bất kể giá trị hiện tại
		// của register (kể cả khi register đó đang là nil) - an toàn tuyệt
		// đối để làm junk trên scratch register chưa khởi tạo.
		private static readonly Opcode[] SafeJunkOpcodes =
		{
			Opcode.Move,
			Opcode.LoadNil,
			Opcode.LoadBool,
			Opcode.Not
		};

		// Junk bị khóa cứng vào scratchA/scratchB - không bao giờ đọc/ghi
		// register nào khác, nên không thể đè lên biến thật của script gốc.
		public Instruction SafeJunk(Chunk lc, int scratchA, int scratchB)
		{
			Opcode op = SafeJunkOpcodes[Random.Next(SafeJunkOpcodes.Length)];
			Instruction ins = new Instruction(lc, op);

			switch (op)
			{
				case Opcode.Move:
				case Opcode.Not:
					ins.A = Random.Next(2) == 0 ? scratchA : scratchB;
					ins.B = Random.Next(2) == 0 ? scratchA : scratchB;
					break;

				case Opcode.LoadNil:
					ins.A = scratchA;
					ins.B = scratchB;
					break;

				case Opcode.LoadBool:
					ins.A = Random.Next(2) == 0 ? scratchA : scratchB;
					ins.B = Random.Next(2);
					ins.C = 0; // QUAN TRỌNG: C!=0 nghĩa là "skip lệnh kế tiếp"
					           // trong semantics Lua - junk phải luôn =0.
					break;
			}

			return ins;
		}

		public Constant GetOrAddConstant(Chunk chunk, ConstantType type, dynamic constant, out int constantIndex)
		{
			var current =
				chunk.Constants.FirstOrDefault(c => c.Type == type &&
				                                    c.Data == constant); // type checking to prevent errors i guess
			if (current != null)
			{
				constantIndex = chunk.Constants.IndexOf(current);
				return current;
			}

			Constant newConst = new Constant
			                    {
				                    Type = type,
				                    Data = constant
			                    };


			constantIndex = chunk.Constants.Count;

			chunk.Constants.Add(newConst);
			chunk.ConstantMap.Add(newConst, constantIndex);

			return newConst;
		}
	}
}