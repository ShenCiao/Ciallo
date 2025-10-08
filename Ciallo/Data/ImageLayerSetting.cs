using System.Runtime.Serialization;
using Godot;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class ImageLayerSetting
{
    [DataMember] public ImageTexture Image = new();
    [DataMember] public Transform2D ImageTransforms = Transform2D.Identity;
}