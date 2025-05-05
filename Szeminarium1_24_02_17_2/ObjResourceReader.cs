using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Globalization;

namespace Szeminarium1_24_02_17_2
{
    internal class ObjResourceReader
    {
        public static unsafe GlObject LoadObjWithColor
        (
            GL      Gl,
            string  objFileName,
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
            List<uint>  glI = new();

            CreateGlArraysFromObjArrays(defaultFaceColor, objV, objN, objF, glV, glC, glI);

            // tenyleges OpenGl objektum
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            return CreateOpenGlObject(Gl, vao, glV, glC, glI);
        }

        
        
        
        private static unsafe GlObject CreateOpenGlObject(GL Gl, uint vao, List<float> glVertices, List<float> glColors,
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

        private static void CreateGlArraysFromObjArrays
(
    float[]                           faceColor,
    List<float[]>                     objVertices,
    List<float[]>                     objNormals,
    List<(int v,int n)[]>             objFaces,
    List<float>                       glVertices,
    List<float>                       glColors,
    List<uint>                        glIndices
)
{
    Dictionary<string, int> uniqueIndex = new();

    foreach (var face in objFaces)
    {
        // van e mindharom csucson normal index
        bool faceHasNormals =
            face[0].n > 0 && face[1].n > 0 && face[2].n > 0 &&
            objNormals.Count >= Math.Max(Math.Max(face[0].n, face[1].n), face[2].n);

        // ha nincs szamoljunk lapnormat
        Vector3D<float> fallbackNormal = default;
        if (!faceHasNormals)
        {
            Vector3D<float> a = new(objVertices[face[0].v - 1][0],
                                    objVertices[face[0].v - 1][1],
                                    objVertices[face[0].v - 1][2]);
            Vector3D<float> b = new(objVertices[face[1].v - 1][0],
                                    objVertices[face[1].v - 1][1],
                                    objVertices[face[1].v - 1][2]);
            Vector3D<float> c = new(objVertices[face[2].v - 1][0],
                                    objVertices[face[2].v - 1][1],
                                    objVertices[face[2].v - 1][2]);
            fallbackNormal = Vector3D.Normalize(Vector3D.Cross(b - a, c - a));
        }

        // a 3 csucs feldolgozasa
        for (int i = 0; i < 3; ++i)
        {
            float[] pos = objVertices[face[i].v - 1];

            float[] normArr;
            if (faceHasNormals)
                normArr = objNormals[face[i].n - 1];
            else
                normArr = new[] { fallbackNormal.X, fallbackNormal.Y, fallbackNormal.Z };

            // kulcs az egyediseghez
            string key = $"{pos[0]} {pos[1]} {pos[2]} {normArr[0]} {normArr[1]} {normArr[2]}";
            if (!uniqueIndex.ContainsKey(key))
            {
                glVertices.AddRange(pos);                       // v.x v.y v.z
                glVertices.AddRange(normArr);                   // n.x n.y n.z
                glColors  .AddRange(faceColor);
                uniqueIndex[key] = uniqueIndex.Count;
            }

            glIndices.Add((uint)uniqueIndex[key]);
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
                var line = sr.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

                var firstSpace = line.IndexOf(' ');
                var tag = firstSpace == -1 ? line : line[..firstSpace];
                var data = firstSpace == -1
                    ? Array.Empty<string>()
                    : line[(firstSpace + 1)..].Split(' ',
                        StringSplitOptions.RemoveEmptyEntries);

                switch (tag)
                {
                    case "v": // vertex
                        objVertices.Add(new float[]
                        {
                            float.Parse(data[0], CultureInfo.InvariantCulture),
                            float.Parse(data[1], CultureInfo.InvariantCulture),
                            float.Parse(data[2], CultureInfo.InvariantCulture)
                        });
                        break;

                    case "vn":                                      // normal
                        objNormals.Add(new float[]
                        {
                            float.Parse(data[0], CultureInfo.InvariantCulture),
                            float.Parse(data[1], CultureInfo.InvariantCulture),
                            float.Parse(data[2], CultureInfo.InvariantCulture)
                        });
                        break;

                    case "f":                                       // face (haromszognek feltetelezzuk)
                        var face = new (int v, int n)[3];
                        for (int i = 0; i < 3; ++i)
                        {
                            var parts = data[i].Split('/');
                            face[i].v = int.Parse(parts[0]);

                            //   v//n  => parts.Length==3 && parts[1]==""               --> normal a parts[2]-ben
                            //   v/n   => parts.Length==2                               --> normal a parts[1]-ben
                            //   v/t/n => parts.Length==3 && parts[2] != ""             --> normal a parts[2]-ben
                            
                            
                            
                            face[i].n = -1;
                            if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
                                face[i].n = int.Parse(parts[1]);
                            else if (parts.Length == 3 && !string.IsNullOrEmpty(parts[2]))
                                face[i].n = int.Parse(parts[2]);
                        }

                        objFaces.Add(face);
                        break;
                }
            }
        }
    }
}