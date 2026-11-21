using System.Runtime.Serialization;

namespace IM800Emu.Core.IM800Debug;

[DataContract]
internal class DebugFile
{
	/// <summary>
	/// Name of the assembler that generated this file
	/// </summary>
	[DataMember]
	public required string Assembler { get; set; }
	/// <summary>
	/// File version. Constant 1.
	/// </summary>
	[DataMember]
	public int FileVersion { get; set; }
	/// <summary>
	/// File path of the corresponding output file
	/// </summary>
	[DataMember]
	public required string OutputFilePath { get; set; }
	/// <summary>
	/// Size in bytes of the corresponding output file
	/// </summary>
	[DataMember]
	public int OutputFileSize { get; set; }
	/// <summary>
	/// List of source file paths
	/// </summary>
	[DataMember]
	public required List<string> SourceFileNames { get; set; }
	/// <summary>
	/// List of symbols
	/// </summary>
	[DataMember]
	public required List<DebugSymbol> Symbols { get; set; }
	/// <summary>
	/// List of source lines
	/// </summary>
	[DataMember]
	public required List<DebugSourceLine> SourceLines { get; set; }
}
