using System.Collections.Generic;

namespace GoldSrc2Unity.Source.IO;

public class MdlSequence
{
    public MdlSequenceEntry Entry;
    public List<List<MdlSequenceFrameEntry>> Blends = new();
}