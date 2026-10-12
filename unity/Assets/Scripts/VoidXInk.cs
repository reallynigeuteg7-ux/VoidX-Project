using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidX
{
    public static class VoidXInk
    {
        static Texture2D hatch;
        public static Material Material(string name, float strokes=0, bool soft=false)
        {
            var m=new Material(Resources.Load<Material>("VoidXInk")) { name=name };
            // Smooth helmets, pipes and pads need silhouettes too; the shader suppresses rims on flat faces.
            m.SetFloat("_Hatch",strokes); m.SetFloat("_Rim",1);
            m.SetFloat("_LineWidth",soft?1.15f:.85f);
            m.SetTexture("_HatchTex",Hatching()); return m;
        }
        static Texture2D Hatching()
        {
            if(hatch)return hatch;
            const int n=512; var p=new Color[n*n]; for(int i=0;i<p.Length;i++)p[i]=Color.black;
            var rng=new System.Random(719);
            for(int i=0;i<450;i++)
            {
                int x=rng.Next(n),y=rng.Next(n),length=rng.Next(5,21); float grey=(float)rng.NextDouble()*.4f+.6f;
                for(int k=0;k<length;k++)p[y*n+(x+k)%n]=new Color(grey,grey,grey,1);
                if(i%4==0)for(int k=0;k<4;k++)p[((y+k)%n)*n+x]=new Color(grey,grey,grey,1);
            }
            hatch=new Texture2D(n,n,TextureFormat.RGBA32,true,true){name="White etched strokes",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            hatch.SetPixels(p);hatch.Apply(true,true);return hatch;
        }
        struct Edge { public Vector3 normal; public bool visible; }
        // Mark real creases, never the diagonals of a flat polygon. Barycentrics keep lines a constant pixel width.
        public static Mesh Contours(Mesh source, bool soft)
        {
            var v=source.vertices;var normal=source.normals;var uv=source.uv;var tris=source.triangles;
            var weld=new Dictionary<Vector3Int,int>();var ids=new int[v.Length];int next=0;
            for(int i=0;i<v.Length;i++)
            {
                var p=v[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));
                if(!weld.TryGetValue(key,out int id)){id=next++;weld.Add(key,id);}ids[i]=id;
            }
            var edges=new Dictionary<(int,int),Edge>();
            (int,int) Key(int a,int b) => ids[a]<ids[b]?(ids[a],ids[b]):(ids[b],ids[a]);
            for(int t=0;t<tris.Length;t+=3)
            {
                int a=tris[t],b=tris[t+1],c=tris[t+2];var n=Vector3.Cross(v[b]-v[a],v[c]-v[a]).normalized;
                foreach(var k in new[]{Key(a,b),Key(b,c),Key(c,a)})
                {
                    if(edges.TryGetValue(k,out var e)) {e.visible|=Vector3.Dot(e.normal,n)<(soft?.55f:.94f);edges[k]=e;}
                    else edges.Add(k,new Edge {normal=n});
                }
            }
            var points=new Vector3[tris.Length];var norms=new Vector3[tris.Length];var tex=new Vector2[tris.Length];
            var bary=new List<Vector3>(tris.Length);var masks=new List<Vector3>(tris.Length);var indices=new int[tris.Length];
            for(int t=0;t<tris.Length;t+=3)
            {
                int a=tris[t],b=tris[t+1],c=tris[t+2];
                var mask=new Vector3(edges[Key(b,c)].visible?1:0,edges[Key(c,a)].visible?1:0,edges[Key(a,b)].visible?1:0);
                for(int k=0;k<3;k++) {int j=t+k,old=tris[j];points[j]=v[old];norms[j]=normal[old];tex[j]=uv.Length==v.Length?uv[old]:Vector2.zero;indices[j]=j;bary.Add(k==0?Vector3.right:k==1?Vector3.up:Vector3.forward);masks.Add(mask);}
            }
            var mesh=new Mesh {name=source.name+" contours",indexFormat=IndexFormat.UInt32};mesh.vertices=points;mesh.normals=norms;mesh.uv=tex;mesh.SetUVs(1,bary);mesh.SetUVs(2,masks);mesh.triangles=indices;mesh.RecalculateBounds();return mesh;
        }
    }
}
