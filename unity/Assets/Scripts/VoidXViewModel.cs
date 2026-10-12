using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidX
{
    // Metre-scale, connected silhouettes. Sleeves taper into wrists; fingers curl around the actual grips.
    public static class VoidXViewModel
    {
        static Material fabric, leather, rubber, alloy, polymer, edge;
        public static Material Fabric { get { Materials(); return fabric; } }
        public static Material Leather { get { Materials(); return leather; } }

        static void Materials()
        {
            if (fabric) return;
            fabric = Surface("Tactical twill", .36f, 0, .22f, true);
            leather = Surface("Glove leather", .30f, 0, .38f, false);
            rubber = Surface("Rubber reinforcement", .14f, 0, .21f, false);
            alloy = Surface("Satin receiver alloy", .39f, .65f, .48f, false);
            polymer = Surface("Grip polymer", .14f, .05f, .32f, false);
            edge = Surface("Machined edge", .62f, .7f, .55f, false);
        }

        static Material Surface(string name, float shade, float metal, float gloss, bool woven)
        {
            return VoidXInk.Material(name, 0, true);
        }

        // Elliptical cross-sections along a smooth Hermite path, with true length rather than stretched primitives.
        public static GameObject Loft(Transform parent, string name, Vector3[] path, Vector2[] radii, Material material, Vector3 up, int sides = 16, int steps = 5, float folds = 0)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            float travelled = 0; Vector3 previous = path[0]; int rings = (path.Length - 1) * steps + 1;
            for (int ring = 0; ring < rings; ring++)
            {
                int segment = Mathf.Min(ring / steps, path.Length - 2); float t = (ring - segment * steps) / (float)steps;
                Vector3 a = path[segment], b = path[segment + 1];
                Vector3 da = (b - path[Mathf.Max(0, segment - 1)]) * .5f, db = (path[Mathf.Min(path.Length - 1, segment + 2)] - a) * .5f;
                float t2 = t * t, t3 = t2 * t;
                Vector3 centre = (2 * t3 - 3 * t2 + 1) * a + (t3 - 2 * t2 + t) * da + (-2 * t3 + 3 * t2) * b + (t3 - t2) * db;
                Vector3 tangent = ((6 * t2 - 6 * t) * a + (3 * t2 - 4 * t + 1) * da + (-6 * t2 + 6 * t) * b + (3 * t2 - 2 * t) * db).normalized;
                Vector3 axis = Vector3.Cross(up, tangent).normalized; if (axis.sqrMagnitude < .1f) axis = Vector3.Cross(Vector3.right, tangent).normalized;
                Vector3 second = Vector3.Cross(tangent, axis).normalized; Vector2 radius = Vector2.Lerp(radii[segment], radii[segment + 1], t);
                travelled += Vector3.Distance(previous, centre); previous = centre;
                for (int j = 0; j <= sides; j++)
                {
                    float angle = j / (float)sides * Mathf.PI * 2;
                    float wrinkle = 1 + folds * Mathf.Sin(travelled * 103 + Mathf.Sin(angle * 3) * 1.4f) * Mathf.Sin(angle * 2 + travelled * 21);
                    vertices.Add(centre + (axis * Mathf.Cos(angle) * radius.x + second * Mathf.Sin(angle) * radius.y) * wrinkle);
                    uv.Add(new Vector2(j / (float)sides, travelled * 6));
                    if (ring > 0 && j < sides)
                    {
                        int current = ring * (sides + 1) + j, last = current - sides - 1;
                        triangles.Add(last); triangles.Add(last + 1); triangles.Add(current);
                        triangles.Add(current); triangles.Add(last + 1); triangles.Add(current + 1);
                    }
                }
            }
            for (int end = 0; end < 2; end++)
            {
                int cap = vertices.Count, start = end == 0 ? 0 : (rings - 1) * (sides + 1);
                vertices.Add(path[end == 0 ? 0 : path.Length - 1]); uv.Add(Vector2.one * .5f);
                for (int j = 0; j < sides; j++) { triangles.Add(cap); triangles.Add(start + (end == 0 ? j + 1 : j)); triangles.Add(start + (end == 0 ? j : j + 1)); }
            }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            // Weld shading at the UV seam, without a visible line down the forearm.
            var normals = mesh.normals; for (int r = 0; r < rings; r++) { int i = r * (sides + 1); var n = (normals[i] + normals[i + sides]).normalized; normals[i] = normals[i + sides] = n; }
            mesh.normals = normals; mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = material; obj.AddComponent<VoidXRuntimeMesh>().Owned = mesh; return obj;
        }

        static void Finger(Transform parent, string name, Vector3[] path, float radius)
        {
            var sizes = new Vector2[path.Length]; for (int i = 0; i < sizes.Length; i++) sizes[i] = Vector2.one * radius * Mathf.Lerp(1, .74f, i / (float)(sizes.Length - 1));
            sizes[^1] *= .5f; Loft(parent, name, path, sizes, leather, Vector3.up, 12, 4);
        }

        static void Ellipsoid(Transform parent, string name, Vector3 p, Vector3 size, Material mat, Vector3 rotation = default)
        {
            var o = VoidXWorld.Part(parent, name, p, size, mat, PrimitiveType.Sphere); o.transform.localRotation = Quaternion.Euler(rotation);
        }

        static void Stitch(Transform parent, Vector3 a, Vector3 b)
        {
            float length = Vector3.Distance(a, b); var o = VoidXWorld.Part(parent, "Double stitched seam", (a + b) * .5f, new Vector3(.0015f, .0015f, length), edge); o.transform.localRotation = Quaternion.LookRotation(b - a);
        }

        static Transform Arms(Transform root, bool shotgun)
        {
            // Restore the compact, bent support arm and padded gloves from the 0.2 viewmodel.
            var support = new GameObject("Classic support arm").transform; support.SetParent(root, false);
            support.localPosition = new Vector3(0, 0, shotgun ? .025f : 0);
            Loft(support, "Bent support sleeve", new[] {new Vector3(-.13f,-.38f,-.30f),new Vector3(-.17f,-.29f,.05f),new Vector3(-.10f,-.18f,.21f),new Vector3(-.047f,-.105f,.305f)},
                new[] {new Vector2(.100f,.100f),new Vector2(.089f,.087f),new Vector2(.079f,.077f),new Vector2(.061f,.060f)},fabric,Vector3.up,24,7,.035f);
            Loft(support,"Wide support cuff",new[] {new Vector3(-.064f,-.133f,.270f),new Vector3(-.046f,-.105f,.305f),new Vector3(-.038f,-.085f,.326f)},
                new[] {new Vector2(.065f,.062f),new Vector2(.060f,.058f),new Vector2(.052f,.050f)},rubber,Vector3.up,24,4);
            Ellipsoid(support,"Classic padded support glove",new Vector3(-.036f,-.067f,.342f),new Vector3(.126f,.102f,.151f),leather,new Vector3(-16,0,-8));
            Ellipsoid(support,"Support glove back pad",new Vector3(-.087f,-.058f,.337f),new Vector3(.034f,.069f,.098f),rubber,new Vector3(-12,0,-8));
            for(int f=0;f<4;f++)
            {
                float z=.288f+f*.028f;
                Ellipsoid(support,"Wrapped support finger",new Vector3(.005f,-.073f,z),new Vector3(.102f,.038f,.026f),leather,new Vector3(0,0,-12));
            }
            Ellipsoid(support,"Support thumb",new Vector3(-.071f,-.003f,.316f),new Vector3(.031f,.047f,.093f),leather,new Vector3(-20,-20,-20));
            VoidXWorld.Combine(support);
            var firing=new GameObject("Classic firing arm").transform; firing.SetParent(root,false);
            Loft(firing,"Short firing sleeve",new[] {new Vector3(.16f,-.36f,-.31f),new Vector3(.104f,-.25f,-.215f),new Vector3(.043f,-.169f,-.118f)},
                new[] {new Vector2(.085f,.086f),new Vector2(.077f,.075f),new Vector2(.060f,.061f)},fabric,Vector3.up,24,6,.035f);
            Loft(firing,"Firing cuff",new[] {new Vector3(.063f,-.194f,-.147f),new Vector3(.041f,-.17f,-.113f)},new[] {new Vector2(.064f,.063f),new Vector2(.057f,.057f)},rubber,Vector3.up,24,3);
            Ellipsoid(firing,"Classic firing glove",new Vector3(.034f,-.145f,-.071f),new Vector3(.118f,.158f,.125f),leather,new Vector3(-14,0,-8));
            Ellipsoid(firing,"Firing knuckle padding",new Vector3(.085f,-.132f,-.060f),new Vector3(.029f,.104f,.073f),rubber,new Vector3(-14,0,-8));
            for(int f=0;f<3;f++) Ellipsoid(firing,"Firing grip finger",new Vector3(.021f,-.116f-f*.027f,-.014f),new Vector3(.105f,.025f,.043f),leather,new Vector3(-14,0,0));
            Ellipsoid(firing,"Trigger finger",new Vector3(.039f,-.074f,-.018f),new Vector3(.027f,.027f,.084f),leather,new Vector3(0,-24,0));
            Ellipsoid(firing,"Firing thumb",new Vector3(-.019f,-.070f,-.080f),new Vector3(.028f,.039f,.093f),leather,new Vector3(-22,16,0));
            VoidXWorld.Combine(firing); return support;
        }
        // Convex side profile with bevels, separate face normals and clean machined highlights.
        public static GameObject Shell(Transform parent, string name, Vector3 position, Vector2[] profile, float width, float bevel, Material material)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var normals = new List<Vector3>(); var indices = new List<int>();
            Vector2 centre=Vector2.zero; foreach(var p in profile)centre+=p; centre/=profile.Length;
            Vector3 Point(int ring,int i){var p=profile[i];if(ring==0||ring==3)p=Vector2.MoveTowards(p,centre,bevel);return new Vector3(ring==0?-width*.5f:ring==1?-width*.5f+bevel:ring==2?width*.5f-bevel:width*.5f,p.x,p.y);}
            void Face(Vector3 normal,params Vector3[] points)
            {
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0)Array.Reverse(points);
                int first=vertices.Count;foreach(var p in points){vertices.Add(p);uv.Add(new Vector2(p.z*3,p.y*3));normals.Add(normal.normalized);}for(int i=1;i<points.Length-1;i++){indices.Add(first);indices.Add(first+i);indices.Add(first+i+1);}
            }
            for(int r=0;r<3;r++)for(int i=0;i<profile.Length;i++){int j=(i+1)%profile.Length;Vector3 middle=(Point(r,i)+Point(r,j)+Point(r+1,i)+Point(r+1,j))*.25f;var n=middle-new Vector3(0,centre.x,centre.y);if(r==0)n.x=-bevel;if(r==2)n.x=bevel;Face(n,Point(r,i),Point(r,j),Point(r+1,j),Point(r+1,i));}
            foreach(int r in new[]{0,3}){var face=new Vector3[profile.Length];for(int i=0;i<face.Length;i++)face[i]=Point(r,i);Face(r==0?Vector3.left:Vector3.right,face);}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateTangents();mesh.RecalculateBounds();
            var o=new GameObject(name);o.transform.SetParent(parent,false);o.transform.localPosition=position;o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=material;o.AddComponent<VoidXRuntimeMesh>().Owned=mesh;return o;
        }

        static void Block(Transform root,string name,Vector3 at,Vector3 size,Material m,Vector3 rotation=default){var o=VoidXWorld.Part(root,name,at,size,m);o.transform.localRotation=Quaternion.Euler(rotation);}
        static void Tube(Transform root,string name,float z,float y,float length,float radius,Material m)
        {
            Loft(root,name,new[]{new Vector3(0,y,z-length*.5f),new Vector3(0,y,z+length*.5f)},new[]{Vector2.one*radius,Vector2.one*radius},m,Vector3.up,24,1);
        }

        public static Transform Build(Transform parent,int type)
        {
            Materials();bool shotgun=type==1;var root=new GameObject(shotgun?"VX—08 Shotgun":"VX—01 Assault Rifle").transform;root.SetParent(parent,false); Transform pump=null;
            Shell(root,"Forged receiver",Vector3.zero,new[]{new Vector2(-.049f,-.123f),new Vector2(.035f,-.139f),new Vector2(.059f,-.095f),new Vector2(.059f,.143f),new Vector2(.028f,.186f),new Vector2(-.046f,.155f)},.100f,.008f,alloy);
            Block(root,"Upper receiver seam",new Vector3(0,.058f,.024f),new Vector3(.084f,.012f,.300f),polymer);
            Tube(root,"Free floating barrel",.45f,.013f,.48f,shotgun?.0185f:.0125f,alloy);
            if(shotgun)
            {
                Tube(root,"Tubular shell magazine",.390f,-.028f,.53f,.018f,polymer);
                pump=new GameObject("VX—08 Pump assembly").transform;
                Shell(pump,"Pump fore-end",new Vector3(0,-.018f,.326f),new[]{new Vector2(-.039f,-.139f),new Vector2(.022f,-.139f),new Vector2(.039f,-.10f),new Vector2(.039f,.10f),new Vector2(.022f,.139f),new Vector2(-.039f,.139f)},.093f,.012f,polymer);
                for(int i=0;i<11;i++)Block(pump,"Pump grip rib",new Vector3(0,-.021f,.215f+i*.021f),new Vector3(.097f,.067f,.007f),rubber);
                Block(root,"Shell loading port",new Vector3(0,-.050f,.069f),new Vector3(.048f,.003f,.072f),rubber);
            }
            else
            {
                Shell(root,"Octagonal handguard",new Vector3(0,0,.320f),new[]{new Vector2(-.042f,-.135f),new Vector2(.025f,-.150f),new Vector2(.045f,-.125f),new Vector2(.045f,.120f),new Vector2(.024f,.150f),new Vector2(-.042f,.132f)},.092f,.009f,alloy);
                for(int i=0;i<7;i++)foreach(int side in new[]{-1,1})Block(root,"Recessed M-LOK slot",new Vector3(side*.047f,.012f,.216f+i*.031f),new Vector3(.002f,.019f,.021f),rubber);
                Shell(root,"Curved thirty round magazine",new Vector3(0,-.047f,.061f),new[]{new Vector2(0,-.042f),new Vector2(-.061f,-.045f),new Vector2(-.210f,-.015f),new Vector2(-.224f,.072f),new Vector2(-.170f,.066f),new Vector2(0,.043f)},.065f,.006f,alloy);
                for(int i=0;i<4;i++)foreach(int side in new[]{-1,1})Block(root,"Magazine pressed channel",new Vector3(side*.033f,-.150f,.030f+i*.018f),new Vector3(.002f,.125f,.004f),polymer,new Vector3(-10,0,0));
                Block(root,"Magazine floorplate",new Vector3(0,-.264f,.095f),new Vector3(.072f,.014f,.079f),polymer,new Vector3(-12,0,0));
            }
            Tube(root,"Ported muzzle brake",.710f,.013f,.057f,shotgun?.027f:.024f,alloy);Tube(root,"Deep black bore",.740f,.013f,.002f,shotgun?.017f:.010f,rubber);
            for(int i=0;i<3;i++)foreach(int side in new[]{-1,1})Block(root,"Muzzle brake port",new Vector3(side*.0238f,.013f,.694f+i*.012f),new Vector3(.002f,.014f,.006f),rubber);
            // First-person stock extends towards the shoulder behind the camera, keeping the firing hand visible.
            Tube(root,"Visible stock collar",-.195f,.006f,.18f,.020f,polymer);
            Shell(root,"Adjustable shoulder stock",new Vector3(0,0,-.660f),new[]{new Vector2(.032f,-.123f),new Vector2(-.100f,-.123f),new Vector2(-.088f,-.071f),new Vector2(-.042f,.095f),new Vector2(.032f,.095f)},.070f,.008f,polymer);
            Block(root,"Rubber butt plate",new Vector3(0,-.035f,-.790f),new Vector3(.080f,.145f,.020f),rubber);
            Shell(root,"Ergonomic pistol grip",new Vector3(0,-.043f,-.041f),new[]{new Vector2(0,-.030f),new Vector2(-.145f,-.062f),new Vector2(-.151f,.010f),new Vector2(-.024f,.042f),new Vector2(0,.028f)},.050f,.008f,polymer);
            // An open trigger bow, with a visible trigger rather than a solid block.
            Block(root,"Trigger bow front",new Vector3(0,-.074f,.036f),new Vector3(.014f,.043f,.010f),alloy,new Vector3(-8,0,0));
            Block(root,"Trigger bow lower",new Vector3(0,-.096f,.008f),new Vector3(.014f,.009f,.060f),alloy);
            Block(root,"Curved trigger",new Vector3(0,-.071f,.004f),new Vector3(.008f,.025f,.008f),edge,new Vector3(-18,0,0));
            Block(root,"Ejection recess",new Vector3(.0504f,.017f,.068f),new Vector3(.002f,.027f,.078f),rubber);
            Block(root,"Bolt visible in port",new Vector3(.0518f,.021f,.068f),new Vector3(.002f,.012f,.058f),edge);
            Block(root,"Charging handle",new Vector3(.060f,.027f,.095f),new Vector3(.031f,.012f,.015f),polymer);
            Ellipsoid(root,"Selector pivot",new Vector3(-.051f,-.008f,-.062f),new Vector3(.005f,.019f,.019f),edge);
            Block(root,"Selector lever",new Vector3(-.055f,-.011f,-.071f),new Vector3(.006f,.009f,.028f),polymer,new Vector3(-20,0,0));
            for(int i=0;i<13;i++)Block(root,"Picatinny tooth",new Vector3(0,.070f,-.075f+i*.029f),new Vector3(.081f,.009f,.011f),polymer);
            Block(root,"Reflex base",new Vector3(0,.088f,.017f),new Vector3(.063f,.028f,.085f),polymer);
            foreach(int side in new[]{-1,1})Block(root,"Open reflex frame",new Vector3(side*.031f,.140f,.029f),new Vector3(.012f,.071f,.023f),alloy,new Vector3(0,0,side*-8));
            Block(root,"Reflex roof",new Vector3(0,.177f,.029f),new Vector3(.059f,.012f,.025f),alloy);
            Block(root,"Smoked reflex lens",new Vector3(0,.142f,.035f),new Vector3(.048f,.054f,.002f),VoidXWorld.Glass);
            for(int i=0;i<4;i++)foreach(int side in new[]{-1,1})Ellipsoid(root,"Flush receiver fastener",new Vector3(side*.0507f,-.019f,-.090f+i*.056f),new Vector3(.004f,.007f,.007f),edge);
            VoidXWorld.Combine(root); if(pump){VoidXWorld.Combine(pump);pump.SetParent(root,false);}
            var support=Arms(root,shotgun);root.gameObject.AddComponent<VoidXWeaponPose>().Init(support,pump);
            foreach(var r in root.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            return root;
        }
    }

    public sealed class VoidXWeaponPose : MonoBehaviour
    {
        Transform support, pump; Vector3 rest;
        public void Init(Transform hand,Transform foreEnd){support=hand;pump=foreEnd;rest=hand.localPosition;}
        public void Animate(float reloadProgress,float pumpAmount)
        {
            float reach=Mathf.Pow(Mathf.Sin(reloadProgress*Mathf.PI),2);
            support.localPosition=rest+new Vector3(.030f,-.025f,pump?-.24f:-.18f)*reach-Vector3.forward*pumpAmount*.065f;
            support.localRotation=Quaternion.Euler(8*reach,12*reach,-16*reach);
            if(pump)pump.localPosition=Vector3.back*pumpAmount*.065f;
        }
    }

    public sealed class VoidXRuntimeMesh : MonoBehaviour
    {
        public Mesh Owned;
        void OnDestroy(){if(Owned)UnityEngine.Object.Destroy(Owned);}
    }
}
