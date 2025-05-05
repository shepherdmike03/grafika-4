using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Globalization;

namespace Szeminarium1_24_02_17_2
{
    internal class ObjResourceReader
    {
        public static unsafe GlObject LoadObjWithColor
        (
            GL Gl,
            string objFileName,
            float[] defaultFaceColor
        )
        {
            // obj beolvas
            ReadObjFile(objFileName,
                out var objV,
                out var objN,
                out var objF);

            // cpu oldali lista OPENGL hez
            List<float> glV = new();
            List<float> glC = new();
            List<uint> glI = new();

            CreateGlArraysFromObjArrays(defaultFaceColor, objV, objN, objF, glV, glC, glI);

            // tenyleges OpenGl objektum
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            return CreateOpenGlObject(Gl, vao, glV, glC, glI);
        }


        internal static unsafe GlObject CreateOpenGlObject(GL Gl, uint vao, List<float> glVertices,
            List<float> glColors,
            List<uint> glIndices)
        {
            uint offsetPos = 0;
            uint offsetNormal = offsetPos + (3 * sizeof(float));
            uint vertexSize = offsetNormal + (3 * sizeof(float));

            uint vertices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glVertices.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);

            Gl.EnableVertexAttribArray(2);
            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetNormal);

            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glColors.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)glIndices.ToArray().AsSpan(),
                GLEnum.StaticDraw);

            // release array buffer
            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            uint indexArrayLength = (uint)glIndices.Count;

            return new GlObject(vao, vertices, colors, indices, indexArrayLength, Gl);
        }

        internal static void CreateGlArraysFromObjArrays
        (
            float[] faceColor,
            List<float[]> objV,
            List<float[]> objN,
            List<(int v, int n)[]> objFaces,
            List<float> glV,
            List<float> glC,
            List<uint> glI
        )
        {
            Dictionary<string, int> uniq = new();

            foreach (var face in objFaces)
            {
                int vc = face.Length;                // face‑en beluli csucspontok szsma

                //ellenorizzuk, hogy minden csucshoz van e normal‑index
                bool hasN = true;
                for (int k = 0; k < vc && hasN; ++k)
                    hasN &= face[k].n > 0 && face[k].n <= objN.Count;

                //ha nincs szamolunk egy lapnormalt az elso 3 csucsbol
                Vector3D<float> flatN = default;
                if (!hasN)
                {
                    var pA = objV[face[0].v - 1];
                    var pB = objV[face[1].v - 1];
                    var pC = objV[face[2].v - 1];

                    Vector3D<float> a = new(pA[0], pA[1], pA[2]);
                    Vector3D<float> b = new(pB[0], pB[1], pB[2]);
                    Vector3D<float> c = new(pC[0], pC[1], pC[2]);

                    flatN = Vector3D.Normalize(Vector3D.Cross(b - a, c - a));
                }


                for (int i = 1; i < vc - 1; ++i)
                {
                    int[] tri = { 0, i, i + 1 };

                    foreach (int idx in tri)
                    {
                        float[] posArr = objV[face[idx].v - 1];
                        float[] nrmArr = hasN
                            ? objN[face[idx].n - 1]
                            : new[] { flatN.X, flatN.Y, flatN.Z };

                        string key = $"{posArr[0]} {posArr[1]} {posArr[2]} " +
                                     $"{nrmArr[0]} {nrmArr[1]} {nrmArr[2]}";

                        if (!uniq.TryGetValue(key, out int glIndex))
                        {
                            glIndex = uniq.Count;
                            uniq[key] = glIndex;

                            glV.AddRange(posArr); // v.x v.y v.z
                            glV.AddRange(nrmArr); // n.x n.y n.z
                            glC.AddRange(faceColor);
                        }

                        glI.Add((uint)glIndex);
                    }
                }
            }
        }

        private static void ReadObjFile
        (
            string fileName,
            out List<float[]> objVertices,
            out List<float[]> objNormals,
            out List<(int v, int n)[]> objFaces
        )
        {
            objVertices = new();
            objNormals = new();
            objFaces = new();

            using StreamReader sr = new StreamReader(fileName);
            while (!sr.EndOfStream)
            {
                string? line = sr.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                int firstSpace = line.IndexOf(' ');
                string tag = firstSpace == -1 ? line : line[..firstSpace];
                string[] data = firstSpace == -1
                    ? Array.Empty<string>()
                    : line[(firstSpace + 1)..]
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

                switch (tag)
                {
                    case "v": // csucspont
                        objVertices.Add(new[]
                        {
                            float.Parse(data[0], CultureInfo.InvariantCulture),
                            float.Parse(data[1], CultureInfo.InvariantCulture),
                            float.Parse(data[2], CultureInfo.InvariantCulture)
                        });
                        break;

                    case "vn": // normal
                        objNormals.Add(new[]
                        {
                            float.Parse(data[0], CultureInfo.InvariantCulture),
                            float.Parse(data[1], CultureInfo.InvariantCulture),
                            float.Parse(data[2], CultureInfo.InvariantCulture)
                        });
                        break;

                    case "f": // oldal hossz
                        var verts = new (int v, int n)[data.Length];
                        for (int i = 0; i < data.Length; ++i)
                        {
                            string[] p = data[i].Split('/');
                            verts[i].v = int.Parse(p[0]); // mindig van

                            verts[i].n = -1; // alap||||   nincs normal‑idx
                            if (p.Length == 2 && p[1] != "")
                                verts[i].n = int.Parse(p[1]); // „v/n”
                            else if (p.Length == 3 && p[2] != "")
                                verts[i].n = int.Parse(p[2]); // „v//n” vagy „v/t/n”
                        }

                        objFaces.Add(verts);
                        break;
                }
            }
        }
    }
}