using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Szeminarium1_24_02_17_2
{
   
    internal static class ColladaResourceReader
    {

        

        
        public static unsafe GlObject LoadColladaWithColor
        (
            GL      gl,
            string  daePath,
            float[] defaultColor
        )
        {
            // COLLADA -> CPU‑listak
            ReadColladaFile(daePath,
                out var positions,
                out var normals,
                out var faces);

            
            
            // ugyanazzal a segedfuggvennyel GL‑hez formazzuk
            List<float> glV = new();
            List<float> glC = new();
            List<uint>  glI = new();

            
            
            ObjResourceReader.CreateGlArraysFromObjArrays(
                defaultColor,
                positions,
                normals,
                faces,
                glV, glC, glI);

            
            
            //tenyleges GL‑objektum
            uint vao = gl.GenVertexArray();
            gl.BindVertexArray(vao);

            return ObjResourceReader.CreateOpenGlObject(gl, vao, glV, glC, glI);
        }

        

        
        
        
        
       

        
        private readonly struct InputDef
        {
            public readonly string Semantic;
            public readonly int    Offset;
            public readonly string SourceId;
            public InputDef(string sem, int off, string src)
                => (Semantic, Offset, SourceId) = (sem, off, src);
        }

        
        private static IEnumerable<float[]> Chunk3(this float[] arr)
        {
            for (int i = 0; i < arr.Length; i += 3)
                yield return new[] { arr[i], arr[i + 1], arr[i + 2] };
        }

      

        
        
        private static void ReadColladaFile
        (
            string                                daePath,
            out List<float[]>                     posList,
            out List<float[]>                     nrmList,
            out List<(int v, int n)[]>            faces                     // OBJ‑formatumhoz illo tuple
        )
        {
            posList = new();
            nrmList = new();
            faces   = new();

            //XML betoltese
            XmlDocument doc = new();
            doc.Load(daePath);

            
            
            XmlNamespaceManager ns = new(doc.NameTable);
            ns.AddNamespace("c", "http://www.collada.org/2005/11/COLLADASchema");

            

            //float array kigyujtese
            var flatArrays = new Dictionary<string, float[]>();

            foreach (XmlNode src in doc.SelectNodes("//c:source", ns)!)
            {
                string srcId = src.Attributes!["id"]!.Value;

                XmlNode? fa = src.SelectSingleNode("./c:float_array", ns);
                if (fa is null) continue;                                                           // pl bool_array

                float[] raw = Array.ConvertAll(
                    fa.InnerText.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries),
                    s => float.Parse(s, CultureInfo.InvariantCulture));

                
                
                
                //csoportmeret az accessor stride‑bol
                XmlNode acc   = src.SelectSingleNode("./c:technique_common/c:accessor", ns)!;
                int comps     = int.Parse(acc.Attributes!["stride"]?.Value ?? "3");

                
                
                // 3‑asaval lapositva taroljuk||||||| OBJ‑loader igy is kezeli
                flatArrays[srcId] = raw;
            }

            
            XmlNode mesh = doc.SelectSingleNode("//c:geometry/c:mesh", ns)
                        ?? throw new Exception("A fajl nem tartalmaz <mesh>-t.");

            
            
            //vertices id  -->  POSITION source id
            var vertToPosSrc = new Dictionary<string, string>();
            foreach (XmlNode verts in mesh.SelectNodes("./c:vertices", ns)!)
            {
                string vid   = verts.Attributes!["id"]!.Value;
                string pSrc  = verts.SelectSingleNode("./c:input[@semantic='POSITION']", ns)!
                                .Attributes!["source"]!.Value.TrimStart('#');
                vertToPosSrc[vid] = pSrc;
            }

            
            
            
            //triangles | polylist
            XmlNode prim = mesh.SelectSingleNode("./c:triangles|./c:polylist", ns)
                        ?? throw new Exception("Csak <triangles> / <polylist> tamogatott.");

            
            
            //INPUT‑lista
            List<InputDef> inputs = new();
            foreach (XmlNode inp in prim.SelectNodes("./c:input", ns)!)
            {
                inputs.Add(new InputDef(
                    inp.Attributes!["semantic"]!.Value,
                    int.Parse(inp.Attributes!["offset"]!.Value),
                    inp.Attributes!["source"]!.Value.TrimStart('#')));
            }

            
            
            int stride = inputs.Max(i => i.Offset) + 1;

            
            
            //VERTEX → POSITION feloldasa
            InputDef vInp   = inputs.First(i => i.Semantic == "VERTEX");
            string posSrcId = vertToPosSrc[vInp.SourceId];
            posList = flatArrays[posSrcId].Chunk3().Select(a => a.ToArray()).ToList();

            
            
            // NORMAL IF exists
            int nOff = -1;
            var nInp = inputs.FirstOrDefault(i => i.Semantic == "NORMAL");
            if (nInp.Semantic != null)
            {
                nOff    = nInp.Offset;
                nrmList = flatArrays[nInp.SourceId].Chunk3().Select(a => a.ToArray()).ToList();
            }

            
            
            //indexlista <p> feldolgozasa
            int[] idx = Array.ConvertAll(
                prim.SelectSingleNode("./c:p", ns)!.InnerText
                    .Split((char[])null!, StringSplitOptions.RemoveEmptyEntries),
                int.Parse);

            // feltetelezzuk hogy minden poligon haromszog
            int triangleCount;
            if (prim.Name == "triangles")
                triangleCount = idx.Length / (stride * 3);
            else
            {
                //vcount mindenhol 3
                string[] vcount = prim.SelectSingleNode("./c:vcount", ns)!
                                      .InnerText.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
                triangleCount = vcount.Length;
            }

            
            
            for (int t = 0; t < triangleCount; ++t)
            {
                (int v, int n)[] tri = new (int, int)[3];

                for (int v = 0; v < 3; ++v)
                {
                    int baseIdx = (t * 3 + v) * stride;
                    tri[v].v = idx[baseIdx + vInp.Offset] + 1;                            // OBJ‑szeru 1‑based
                    tri[v].n = nOff >= 0 ? idx[baseIdx + nOff] + 1 : -1;
                }
                faces.Add(tri);
            }
        }

        
    }
}
