using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class PolygonFillCoverageTests
{
    [TestCase]
    public void GpuCoverageMatchesClippingForThinAlignedReversedAndLargeTriangles()
    {
        List<Vector2[]> triangles = [
            [new(0, 0), new(1, 0), new(0, 1)],
            [new(0, 0), new(1, 1), new(0, 1)],
            [new(-2, -2), new(4, -2), new(-2, 4)],
            [new(0, 0), new(0, 0), new(1, 1)],
            [new(-10, -10), new(10, 10), new(0, 0)],
            [new(.25f, .25f), new(.75f, .25f), new(.5f, .25001f)],
            [new(.5f, -10), new(.50001f, 10), new(.5f, 10)],
            [new(-10, .5f), new(10, .50001f), new(10, .5f)],
            [new(0, -10000), new(0, 10000), new(.00001f, 0)],
            [new(0, 0), new(0, 10000), new(10000, 0)],
            [new(1, 0), new(1, 10000), new(10000, 0)],
        ];
        var random = new Random(74123);
        float[] scales = [.001f, 1, 10, 100, 10000];
        for (int i = 0; i < 3000; i++)
        {
            float scale = scales[i % scales.Length];
            Vector2 Point() => new(.5f + (random.NextSingle() - .5f) * scale,
                .5f + (random.NextSingle() - .5f) * scale);
            triangles.Add([Point(), Point(), Point()]);
        }
        for (int i = 0; i < 200; i++)
        {
            float angle = random.NextSingle() * Mathf.Tau;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var center = new Vector2(.5f, .5f);
            float length = 1 + random.NextSingle() * 10000;
            triangles.Add([center - direction * length, center + direction * length,
                center + new Vector2(-direction.Y, direction.X) * (random.NextSingle() * .001f)]);
        }
        int[][] permutations = [[0, 1, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0], [1, 0, 2], [0, 2, 1]];
        var cases = triangles.SelectMany(t => permutations.Select(p => p.Select(i => t[i]).ToArray())).ToArray();
        var packed = cases.SelectMany(t => new[] { t[0].X, t[0].Y, t[1].X, t[1].Y, t[2].X, t[2].Y, 0f, 0f }).ToArray();

        // Execute the production functions on the GPU, with an independent double
        // precision clipping reference. No copied implementation of edge integration.
        string include = FileAccess.GetFileAsString("res://Rendering/PolygonFillAA.gdshaderinc");
        string functions = include[include.IndexOf("float aa_edge_area", StringComparison.Ordinal)..
            include.IndexOf("float polygon_aa_alpha", StringComparison.Ordinal)];
        string cross = include[include.IndexOf("float aa_cross", StringComparison.Ordinal)..
            include.IndexOf("vec2 aa_vertex_offset", StringComparison.Ordinal)];
        using var device = RenderingServer.CreateLocalRenderingDevice()
            ?? throw new InvalidOperationException("Polygon coverage tests require a GPU RenderingDevice.");
        using var source = new RDShaderSource
        {
            SourceCompute = """
                #version 450
                layout(local_size_x=64) in;
                layout(set=0,binding=0,std430) readonly buffer Input { vec4 points[]; } data;
                layout(set=0,binding=1,std430) writeonly buffer Output { float values[]; } result;
                vec2 aa_triangle_a, aa_triangle_b, aa_triangle_c;
                """ + cross + functions + """
                void main() {
                    uint i=gl_GlobalInvocationID.x;
                    if(i>=result.values.length()) return;
                    aa_triangle_a=data.points[i*2].xy;
                    aa_triangle_b=data.points[i*2].zw;
                    aa_triangle_c=data.points[i*2+1].xy;
                    result.values[i]=polygon_aa_coverage(vec2(0.5));
                }
                """,
        };
        using var spirv = device.ShaderCompileSpirVFromSource(source);
        AssertThat(spirv.CompileErrorCompute).IsEmpty();
        var shader = device.ShaderCreateFromSpirV(spirv);
        var input = device.StorageBufferCreate((uint)(packed.Length * sizeof(float)), MemoryMarshal.AsBytes(packed.AsSpan()).ToArray());
        var output = device.StorageBufferCreate((uint)(cases.Length * sizeof(float)));
        using var inputUniform = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 0 };
        using var outputUniform = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 1 };
        inputUniform.AddId(input);
        outputUniform.AddId(output);
        var uniforms = device.UniformSetCreate([inputUniform, outputUniform], shader, 0);
        var pipeline = device.ComputePipelineCreate(shader);
        try
        {
            long list = device.ComputeListBegin();
            device.ComputeListBindComputePipeline(list, pipeline);
            device.ComputeListBindUniformSet(list, uniforms, 0);
            device.ComputeListDispatch(list, (uint)((cases.Length + 63) / 64), 1, 1);
            device.ComputeListEnd();
            device.Submit();
            device.Sync();
            var actual = MemoryMarshal.Cast<byte, float>(device.BufferGetData(output)).ToArray();
            double maximumError = 0;
            for (int i = 0; i < cases.Length; i++)
            {
                AssertThat(float.IsFinite(actual[i])).IsTrue();
                maximumError = Math.Max(maximumError, Math.Abs(actual[i] - ClippedArea(cases[i])));
            }
            GD.Print($"Polygon coverage: {cases.Length} cases, maximum area error {maximumError:G6}");
            AssertThat(maximumError).IsLess(.0005);
        }
        finally
        {
            device.FreeRid(pipeline);
            device.FreeRid(uniforms);
            device.FreeRid(input);
            device.FreeRid(output);
            device.FreeRid(shader);
        }
    }

    private static double ClippedArea(Vector2[] triangle)
    {
        var polygon = triangle.Select(p => ((double)p.X, (double)p.Y)).ToList();
        foreach (var (axis, boundary, direction) in new[] { (0, 0, 1), (0, 1, -1), (1, 0, 1), (1, 1, -1) })
        {
            if (polygon.Count == 0) return 0;
            List<(double X, double Y)> output = [];
            var previous = polygon[^1];
            double Distance((double X, double Y) p) => ((axis == 0 ? p.X : p.Y) - boundary) * direction;
            double pd = Distance(previous);
            foreach (var current in polygon)
            {
                double d = Distance(current);
                if ((d < 0 && pd > 0) || (d > 0 && pd < 0))
                {
                    double t = pd / (pd - d);
                    output.Add((previous.Item1 + t * (current.Item1 - previous.Item1),
                        previous.Item2 + t * (current.Item2 - previous.Item2)));
                }
                if (d >= 0) output.Add(current);
                previous = current;
                pd = d;
            }
            polygon = output;
        }
        double twiceArea = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p = polygon[i];
            var q = polygon[(i + 1) % polygon.Count];
            twiceArea += p.Item1 * q.Item2 - p.Item2 * q.Item1;
        }
        return Math.Abs(twiceArea) * .5;
    }
}
