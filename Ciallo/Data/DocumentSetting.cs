using System;
using System.Runtime.Serialization;
using Godot;
using R3;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class DocumentSetting
{
    [DataMember, ProjectField] public ReactiveProperty<Guid> DocumentId = new(Guid.NewGuid());
    [DataMember, ProjectField] public ReactiveProperty<string> Name = new();
    // Reference size is used for background size, default export size, import image size, etc.
    [DataMember, ProjectField] public ReactiveProperty<Vector2> ReferenceSize = new(new(1920, 1080));
    [DataMember, ProjectField] public ReactiveProperty<Color> BackgroundColor = new(Colors.White);

    public ReactiveProperty<string> FilePath = new(OS.GetSystemDir(OS.SystemDir.Documents));
}
