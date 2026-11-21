using System.Runtime.Serialization;

namespace IM800Emu.Core.IM800Debug;

[DataContract]
internal class DebugSymbol
{
	/// <summary>
	/// Index into the SourceFileName list of the file that this symbol was
	/// declared in
	/// </summary>
	[DataMember]
	public int SourceFileNameIndex { get; set; }
	/// <summary>
	/// 1-indexed line from the source file that this symbol was declared in
	/// </summary>
	[DataMember]
	public int Line { get; set; }
	/// <summary>
	/// Fully-qualified name of the symbol
	/// </summary>
	[DataMember]
	public required string Name { get; set; }
	/// <summary>
	/// Symbol type
	/// </summary>
	[DataMember]
	public Constants.SymbolType SymbolType { get; set; }
	/// <summary>
	/// Symbol value
	/// </summary>
	[DataMember]
	public long Value { get; set; }
}
