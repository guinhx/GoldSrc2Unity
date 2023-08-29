using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoldSrc2Unity.Source.Extension;
using UnityEngine;

namespace GoldSrc2Unity.Source.IO
{
    public class MdlFile : IDisposable
    {
        private BinaryReader _reader;
        private readonly string _filePath;

        private MdlHeader _header;
        private readonly List<MdlTextureEntry> _textures = new();
        private readonly List<MdlBoneEntry> _bones = new();
        private readonly List<MdlSequence> _sequences = new();
        private readonly List<MdlBodyPartEntry> _bodyParts = new();

        public const string SupportedMagic = "IDS";
        public const int SupportedVersion = 10;

        public MdlFile(string filePath)
        {
            _filePath = filePath;
        }

        public bool Open()
        {
            try
            {
                _reader = new BinaryReader(new FileStream(_filePath, FileMode.Open));
                _header = _reader.ReadStruct<MdlHeader>();
                if (_header.Magic != SupportedMagic) throw new Exception($"Unsupported magic: {_header.Magic}");
                if (_header.Version != SupportedVersion) throw new Exception($"Unsupported version: {_header.Version}");

                Debug.Log($"Magic = {_header.Magic}, Version = {_header.Version}, Name = {_header.Name}, Size = {_header.Size}");
                Debug.Log($"NumSequences = {_header.NumSequences}, EyePosition = {_header.EyePosition}, Min = {_header.Min}, Max = {_header.Max}, BoundingBoxMin = {_header.BoundingBoxMin}, BoundingBoxMax = {_header.BoundingBoxMax}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
        }

        public bool Read()
        {
            if (_reader == null) return false;
            try
            {
                _reader.BaseStream.Seek(_header.TextureIndex, SeekOrigin.Begin);
                for (var i = 0; i < _header.NumTextures; i++)
                {
                    var entry = _reader.ReadStruct<MdlTextureEntry>();
                    Debug.Log($"Texture: {entry.Name}, Flags = {entry.Flags}, Width = {entry.Width}, Height = {entry.Height}, Index = {entry.Index}");
                    _textures.Add(entry);
                }

                _reader.BaseStream.Seek(_header.BoneIndex, SeekOrigin.Begin);
                for (var i = 0; i < _header.NumBones; i++)
                {
                    var entry = _reader.ReadStruct<MdlBoneEntry>();
                    Debug.Log($"Bone: {entry.Name}, Parent = {entry.Parent}, Flags = {entry.Flags}, Position = {entry.Position}, Rotation = {entry.Rotation}");
                    _bones.Add(entry);
                }

                _reader.BaseStream.Seek(_header.SequenceIndex, SeekOrigin.Begin);
                for (var i = 0; i < _header.NumSequences; i++)
                {
                    var entry = _reader.ReadStruct<MdlSequenceEntry>();
                    Debug.Log($"Sequence: {entry.Name}, FPS = {entry.FPS}, Flags = {entry.Flags}, Activity = {entry.Activity}, ActivityWeight = {entry.ActivityWeight}, NumEvents = {entry.NumEvents}, EventIndex = {entry.EventIndex}, NumFrames = {entry.NumFrames}, NumPivots = {entry.NumPivots}, PivotIndex = {entry.PivotIndex}, MotionType = {entry.MotionType}, MotionBone = {entry.MotionBone}, LinearMovement = {entry.LinearMovement}, AutoMovePosIndex = {entry.AutoMovePosIndex}, AutoMoveAngleIndex = {entry.AutoMoveAngleIndex}, BoundingBoxMin = {entry.BoundingBoxMin}, BoundingBoxMax = {entry.BoundingBoxMax}, NumBlends = {entry.NumBlends}, AnimIndex = {entry.AnimIndex}, BlendType0 = {entry.BlendType0}, BlendType1 = {entry.BlendType1}, BlendStart0 = {entry.BlendStart0}, BlendStart1 = {entry.BlendStart1}, BlendEnd0 = {entry.BlendEnd0}, BlendEnd1 = {entry.BlendEnd1}, BlendParent = {entry.BlendParent}, SeqGroup = {entry.SeqGroup}, EntryNode = {entry.EntryNode}, ExitNode = {entry.ExitNode}, NodeFlags = {entry.NodeFlags}, NextSeq = {entry.NextSeq}");
                    var blends = new List<List<MdlSequenceFrameEntry>>();
                    var position = _reader.BaseStream.Position;
                    for (var j = 0; j < entry.NumBlends; j++)
                    {
                        var blend = ParseBlend(entry);
                        blends.Add(blend);
                    }
                    _reader.BaseStream.Seek(position, SeekOrigin.Begin);
                    _sequences.Add(new MdlSequence
                    {
                        Entry = entry,
                        Blends = blends
                    });
                    foreach (var frame in _sequences.Last().Blends.SelectMany(blend => blend))
                    {
                        Debug.Log($"Frame = {frame.Index} ({frame.Position}, {frame.Rotation})");
                    }
                }
                
                // TODO: Blend Seq. to Bone Transform Anim

                _reader.BaseStream.Seek(_header.BodyPartIndex, SeekOrigin.Begin);
                for (var i = 0; i < _header.NumBodyParts; i++)
                {
                    var bodyPart = _reader.ReadStruct<MdlBodyPartEntry>();
                    Debug.Log($"BodyPart: {bodyPart.Name}, NumModels = {bodyPart.NumModels}, Base = {bodyPart.Base}, ModelIndex = {bodyPart.ModelIndex}");
                    _bodyParts.Add(bodyPart);
                    // TODO: Read Models from BodyPart
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
        }

        private List<MdlSequenceFrameEntry> ParseBlend(MdlSequenceEntry entry)
        {
            _reader.BaseStream.Seek(entry.AnimIndex, SeekOrigin.Begin);

            var blendOffsets = new List<short>();
            var blendLength = _header.NumBones * 6;
            for (var i = 0; i < entry.NumBlends * blendLength; i++)
            {
                blendOffsets.Add(_reader.ReadInt16());
            }
            
            var result = new List<MdlSequenceFrameEntry>();
            result.Fill( new MdlSequenceFrameEntry(), entry.NumFrames);

            for (var boneIdx = 0; boneIdx < _header.NumBones; boneIdx++)
            {
                var boneFrameData = new Dictionary<MdlAttribute, List<int>>();
                for (var i = 0; i < 6; i++)
                {
                    int offset = blendOffsets[boneIdx * 6 + i];

                    boneFrameData.Add((MdlAttribute)i, offset == 0 ? CreateEmptyData(entry.NumFrames) : ParseAnimData(entry.NumFrames));
                }
                
                for (var f = 0; f < entry.NumFrames; f++)
                {
                    var position = new Vector3
                    {
                        x = boneFrameData[MdlAttribute.PosX][f],
                        y = boneFrameData[MdlAttribute.PosY][f],
                        z = boneFrameData[MdlAttribute.PosZ][f]
                    };
                    
                    var rotation = new Vector3
                    {
                        x = boneFrameData[MdlAttribute.RotX][f],
                        y = boneFrameData[MdlAttribute.RotY][f],
                        z = boneFrameData[MdlAttribute.RotZ][f]
                    };
                    result.Add(new MdlSequenceFrameEntry
                    {
                        Index = f,
                        Position = position,
                        Rotation = rotation
                    });
                }
            }
            
            return result;
        }


        private List<int> CreateEmptyData(int numFrames)
        {
            return (List<int>)new List<int>().Fill(0, numFrames);
        }
        private List<int> ParseAnimData(int numFrames)
        {
            var result = CreateEmptyData(numFrames);
            var i = 0;
            while (i < numFrames)
            {
                var compressedSize = _reader.ReadByte();
                var uncompressedSize = _reader.ReadByte();
                var compressedData = _reader.ReadShortArray(compressedSize);
                var j = 0;
                while (j < uncompressedSize && i < numFrames)
                {
                    var index = Math.Min(compressedSize - 1, j);
                    result[i] = compressedData[index];
                    j++;
                    i++;
                }
            }
            return result;
        }

        public void Dispose() => _reader?.Close();

        public string ModelName => _filePath.WithoutFileExtension();
    }
}