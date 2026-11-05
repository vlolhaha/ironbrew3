namespace IronBrew2.Obfuscator
{
	public class ObfuscationSettings
	{
		public bool EncryptStrings;
		public bool EncryptImportantStrings;
		public bool ControlFlow;
		public bool BytecodeCompress;
		public int DecryptTableLen;
		public bool PreserveLineInfo;
		public bool Mutate;
		public bool SuperOperators;
		public int MaxMiniSuperOperators;
		public int MaxMegaSuperOperators;
		public int MaxMutations;

		public bool JunkCode;
		public int MinJunkPerChunk;
		public int MaxJunkPerChunk;

		public int EncryptionLayers;

		public bool IntegrityCheck;
		
		public ObfuscationSettings()
		{
			EncryptStrings = false;
			EncryptImportantStrings = false;
			ControlFlow = true;
			BytecodeCompress = true;
			DecryptTableLen = 500;
			PreserveLineInfo = false;
			Mutate = true;
			SuperOperators = true;
			MaxMegaSuperOperators = 120;
			MaxMiniSuperOperators = 120;
			MaxMutations = 200;

			// Chưa build-test kỹ trong môi trường thật -> để false mặc định,
			// tự bật sau khi test qua Tests/*.lua.
			JunkCode = false;
			MinJunkPerChunk = 3;
			MaxJunkPerChunk = 8;

			EncryptionLayers = 3;

			IntegrityCheck = true;
		}
	}
}