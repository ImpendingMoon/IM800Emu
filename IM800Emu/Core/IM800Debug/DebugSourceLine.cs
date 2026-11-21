using System.Runtime.Serialization;

namespace IM800Emu.Core.IM800Debug;

[DataContract]
internal class DebugSourceLine
{
	/// <summary>
	/// Index into the SourceFileName list
	/// </summary>
	[DataMember]
	public int SourceFileNameIndex { get; set; }
	/// <summary>
	/// 1-indexed line from the source file
	/// </summary>
	[DataMember]
	public int Line { get; set; }
	/// <summary>
	/// Base address of this line in the emitted program
	/// </summary>
	[DataMember]
	public long BaseAddress { get; set; }
	/// <summary>
	/// Number of bytes emitted from this line
	/// </summary>
	[DataMember]
	public int OutputLength { get; set; }
	/// <summary>
	/// Source text
	/// </summary>
	[DataMember]
	public required string Text { get; set; }
}
