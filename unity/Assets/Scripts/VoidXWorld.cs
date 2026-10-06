using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidX
{
    // Shared meshes/materials keep this entirely offline industrial arena inexpensive on mobile.
    public static class VoidXWorld
    {
        public struct Cover { public Vector2 centre, size; }
        public static readonly List<Cover> Covers = new();
        public static Material Concrete, Steel, Black, White, Floor, Glass, Glow;
        static readonly Dictionary<PrimitiveType, Mesh> meshes = new();
        static Mesh gunBox;
        static System.Random random;
        public static float R(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
        static Color Grey(float g) => new(g, g, g, 1);

        static Texture2D Surface(int kind, bool normal = false)
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, normal) { name = normal ? "Micro normal" : "Industrial surface", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var colors = new Color[n * n];
            float Height(float x, float y)
            {
                float broad = Mathf.PerlinNoise(x * .048f + kind * 10, y * .048f);
                float grain = Mathf.PerlinNoise(x * .51f + 8, y * .51f);
                float g = .52f + (broad - .5f) * .22f + (grain - .5f) * .16f;
                if (kind == 1) g -= Mathf.Pow(Mathf.Abs(Mathf.Sin(x * .31f + y * .012f)), 28) * .07f;
                if (kind == 2) { g -= Mathf.Pow(Mathf.Abs(Mathf.Sin(x * .025f)), 60) * .16f; g -= Mathf.Pow(Mathf.Abs(Mathf.Sin(y * .025f)), 60) * .16f; }
                return g;
            }
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                if (!normal) colors[y * n + x] = Grey(Height(x, y));
                else { var v = new Vector3((Height(x - 1, y) - Height(x + 1, y)) * 2, (Height(x, y - 1) - Height(x, y + 1)) * 2, 1).normalized; colors[y * n + x] = new Color(v.x * .5f + .5f, v.y * .5f + .5f, v.z * .5f + .5f, v.x * .5f + .5f); }
            }
            tex.SetPixels(colors); tex.Apply(true, true); return tex;
        }
        static Material Mat(string name, float shade, float metal, float smooth, int kind = 0)
        {
            var template = Resources.Load<Material>("VoidXLit");
            var mat = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.name = name; mat.enableInstancing = true;
            mat.SetColor("_BaseColor", Grey(shade)); mat.SetFloat("_Metallic", metal); mat.SetFloat("_Smoothness", smooth);
            mat.SetTexture("_BaseMap", Surface(kind)); mat.SetTexture("_BumpMap", Surface(kind, true)); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", .4f);
            return mat;
        }
        public static GameObject Part(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, PrimitiveType type = PrimitiveType.Cube, bool collider = false)
        {
            if (!meshes.TryGetValue(type, out var mesh))
            {
                var sample = GameObject.CreatePrimitive(type); mesh = sample.GetComponent<MeshFilter>().sharedMesh; meshes[type] = mesh;
                if (Application.isPlaying) UnityEngine.Object.Destroy(sample); else UnityEngine.Object.DestroyImmediate(sample);
            }
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = pos; obj.transform.localScale = size;
            if (type == PrimitiveType.Cube && parent && parent.name.StartsWith("VX—")) { if (!gunBox) gunBox = BevelledBox(); mesh = gunBox; }
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            if (collider) obj.AddComponent<BoxCollider>();
            return obj;
        }
        static Mesh BevelledBox()
        {
            const float h = .5f, k = .455f;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            void Face(Vector3 normal, params Vector3[] points)
            {
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) < 0) Array.Reverse(points);
                int start = vertices.Count; foreach (var p in points) { vertices.Add(p); normals.Add(normal.normalized); uv.Add(Mathf.Abs(normal.y) > .7f ? new Vector2(p.x, p.z) : new Vector2(p.z, p.y)); }
                for (int i = 1; i < points.Length - 1; i++) { indices.Add(start); indices.Add(start + i); indices.Add(start + i + 1); }
            }
            foreach (int s in new[] { -1, 1 })
            {
                Face(new Vector3(s,0,0),new(s*h,-k,-k),new(s*h,k,-k),new(s*h,k,k),new(s*h,-k,k));
                Face(new Vector3(0,s,0),new(-k,s*h,-k),new(k,s*h,-k),new(k,s*h,k),new(-k,s*h,k));
                Face(new Vector3(0,0,s),new(-k,-k,s*h),new(k,-k,s*h),new(k,k,s*h),new(-k,k,s*h));
            }
            foreach (int a in new[] { -1, 1 }) foreach (int b in new[] { -1, 1 })
            {
                Face(new Vector3(a,b,0),new(a*h,b*k,-k),new(a*k,b*h,-k),new(a*k,b*h,k),new(a*h,b*k,k));
                Face(new Vector3(a,0,b),new(a*h,-k,b*k),new(a*k,-k,b*h),new(a*k,k,b*h),new(a*h,k,b*k));
                Face(new Vector3(0,a,b),new(-k,a*h,b*k),new(-k,a*k,b*h),new(k,a*k,b*h),new(k,a*h,b*k));
                foreach (int c in new[] { -1, 1 }) Face(new Vector3(a,b,c),new(a*h,b*k,c*k),new(a*k,b*h,c*k),new(a*k,b*k,c*h));
            }
            var result = new Mesh { name = "Machined bevels" }; result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0,uv); result.SetTriangles(indices,0); result.RecalculateTangents(); result.RecalculateBounds(); return result;
        }
        static void Box(Transform p, Vector3 pos, Vector3 size, Material mat, bool solid = false)
        {
            Part(p, "Architecture", pos, size, mat, PrimitiveType.Cube, solid);
            if (solid) Covers.Add(new Cover { centre = new Vector2(pos.x, pos.z), size = new Vector2(size.x, size.z) });
        }
        static void Pipe(Transform p, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var o = Part(p, "Pipe", (a + b) * .5f, new Vector3(radius * 2, Vector3.Distance(a, b) * .5f, radius * 2), mat, PrimitiveType.Cylinder);
            o.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }
        static void AddCover(Transform p, Vector3 pos, Vector3 size)
        {
            Box(p, pos, size, Concrete, true);
            Box(p, pos + Vector3.up * (size.y * .5f), new Vector3(size.x + .12f, .12f, size.z + .12f), Steel);
            for (float x = -size.x * .5f + .25f; x < size.x * .5f; x += .6f)
                Box(p, pos + new Vector3(x, 0, size.z * .5f + .015f), new Vector3(.065f, size.y * .75f, .035f), Black);
        }
        public static Transform Build()
        {
            random = new System.Random(1942); Covers.Clear();
            Concrete = Mat("Weathered concrete", .82f, .02f, .12f);
            Steel = Mat("Brushed gunmetal", .46f, .78f, .58f, 1);
            Black = Mat("Carbon / oxidised steel", .12f, .25f, .24f, 1);
            White = Mat("Ivory paint", 1.45f, .25f, .3f);
            Floor = Mat("Cracked industrial floor", .59f, .12f, .37f, 2);
            Glass = Mat("Dark glazing", .095f, .85f, .93f);
            Glow = new Material(Resources.Load<Material>("VoidXGlow")); Glow.name = "Strip light"; Glow.SetColor("_EmissionColor", Grey(5)); Glow.EnableKeyword("_EMISSION");
            var root = new GameObject("SECTOR 07 · INDUSTRIAL COURTYARD").transform;
            Part(root, "Ground collision", new Vector3(0, -.2f, 0), new Vector3(60, .4f, 60), Floor, PrimitiveType.Cube, true);
            foreach (int side in new[] { -1, 1 })
            {
                Box(root, new Vector3(side * 25, 5, 0), new Vector3(2, 10, 52), Concrete, true);
                for (int z = -20; z <= 20; z += 8)
                {
                    Box(root, new Vector3(side * 23.92f, 6.2f, z), new Vector3(.13f, 2.8f, 4.4f), Glass);
                    Box(root, new Vector3(side * 23.76f, 6.2f, z), new Vector3(.16f, 2.85f, .085f), Steel);
                    Box(root, new Vector3(side * 23.76f, 6.2f, z), new Vector3(.16f, .1f, 4.5f), Steel);
                    Box(root, new Vector3(side * 24, 5, z + 3.65f), new Vector3(.65f, 10, .65f), Black);
                    Box(root, new Vector3(side * 23.6f, 3, z), new Vector3(.25f, .1f, 5.4f), Steel);
                }
                Pipe(root, new Vector3(side * 23.4f, 2, -23), new Vector3(side * 23.4f, 2, 23), .14f, Steel);
                for (int z = -20; z <= 20; z += 3) Box(root, new Vector3(side * 23.4f, 2, z), new Vector3(.4f, .45f, .1f), Black);
            }
            Box(root, new Vector3(0, 5, -25), new Vector3(50, 10, 2), Concrete, true);
            Box(root, new Vector3(0, 3.5f, 25), new Vector3(50, 7, 2), Concrete, true);
            for (int x = -20; x <= 20; x += 8)
            {
                Box(root, new Vector3(x, 7, -23.91f), new Vector3(4, 2.4f, .12f), Glass);
                Box(root, new Vector3(x, 7, -23.8f), new Vector3(.1f, 2.5f, .15f), Steel);
                Box(root, new Vector3(x, 7, -23.8f), new Vector3(4.1f, .12f, .15f), Steel);
                Box(root, new Vector3(x, 9.75f, -23.7f), new Vector3(5.5f, .22f, 1.8f), Black);
            }
            // Recessed roll-up door with slats, rivets and emissive overhead light.
            Box(root, new Vector3(0, 2.4f, -23.83f), new Vector3(7, 4.8f, .2f), Black);
            for (float y = .25f; y < 4.6f; y += .23f) Box(root, new Vector3(0, y, -23.66f), new Vector3(6.6f, .15f, .09f), Steel);
            foreach (int side in new[] { -1, 1 }) Box(root, new Vector3(side * 3.5f, 2.4f, -23.55f), new Vector3(.23f, 5.1f, .3f), Steel);
            Box(root, new Vector3(0, 5.1f, -23.3f), new Vector3(3.8f, .07f, .18f), Glow);
            foreach (var v in new[] { new Vector3(-11, 1.1f, -9), new Vector3(10, 1.1f, -10), new Vector3(-10, 1.1f, 8), new Vector3(10, 1.1f, 9) }) AddCover(root, v, new Vector3(4.5f, 2.2f, 3.2f));
            foreach (int side in new[] { -1, 1 })
            {
                AddCover(root, new Vector3(side * 18, 1.2f, 0), new Vector3(3, 2.4f, 6));
                // Corrugated containers and supply crates.
                Box(root, new Vector3(side * 16, 1.45f, -18), new Vector3(5.6f, 2.9f, 3), Black, true);
                for (float x = -2.7f; x < 2.8f; x += .22f) Box(root, new Vector3(side * 16 + x, 1.45f, -16.47f), new Vector3(.065f, 2.65f, .075f), Steel);
                for (int i = 0; i < 3; i++)
                {
                    var centre = new Vector3(side * 20, .6f, 16 + i * 1.4f);
                    Box(root, centre, new Vector3(1.25f, 1.2f, 1.1f), Concrete, true);
                    Box(root, centre + new Vector3(0, 0, .57f), new Vector3(1.3f, .12f, .07f), Steel);
                }
                Pipe(root, new Vector3(side * 21, 0, -12), new Vector3(side * 21, 8, -12), .085f, Steel);
                Pipe(root, new Vector3(side * 21, 8, -12), new Vector3(side * 21, 8, -8), .085f, Steel);
                Box(root, new Vector3(side * 21, 7.9f, -8), new Vector3(1, .12f, .55f), Glow);
            }
            // Gantry, suspended conduits and distant skyline add depth above the combat space.
            foreach (int z in new[] { -19, 19 })
            {
                Pipe(root, new Vector3(-23, 9, z), new Vector3(23, 9, z), .17f, Black);
                for (int x = -22; x < 23; x += 4) { Box(root, new Vector3(x, 8.7f, z), new Vector3(.13f, .65f, .13f), Steel); Pipe(root, new Vector3(x, 8.5f, z), new Vector3(x + 4, 9, z), .025f, Steel); }
            }
            for (int i = 0; i < 42; i++)
            {
                float a = i / 42f * Mathf.PI * 2, r = R(35, 47), h = R(12, 30);
                var v = new Vector3(Mathf.Sin(a) * r, h * .5f, Mathf.Cos(a) * r);
                Box(root, v, new Vector3(R(3, 6), h, R(3, 6)), Concrete);
                if (i % 3 == 0) Pipe(root, v + Vector3.up * h * .5f, v + Vector3.up * (h * .5f + 8), .12f, Black);
            }
            for (int z = -20; z <= 20; z += 5) foreach (int side in new[] { -1, 1 }) Box(root, new Vector3(side * 2.8f, .012f, z), new Vector3(.075f, .025f, 2), White);
            for (int i = 0; i < 140; i++)
            {
                float x = R(-23, 23), z = R(-22, 22); if (Mathf.Abs(x) < 4) continue;
                var rubble = Part(root, "Rubble", new Vector3(x, .06f, z), new Vector3(R(.07f, .3f), R(.04f, .13f), R(.08f, .4f)), i % 2 == 0 ? Concrete : Steel);
                rubble.transform.localRotation = Quaternion.Euler(R(0, 14), R(0, 360), R(0, 14));
            }
            Sign(root, "07", new Vector3(0, 6.4f, -23.6f), 2.3f);
            Sign(root, "VOID SECTOR", new Vector3(-12, 3.7f, -23.7f), .55f);
            Combine(root);
            SetupLight();
            return root;
        }
        static void Sign(Transform p, string text, Vector3 pos, float size)
        {
            var o = new GameObject("Sector signage"); o.transform.SetParent(p); o.transform.localPosition = pos; o.transform.rotation = Quaternion.Euler(0, 180, 0);
            var t = o.AddComponent<TextMesh>(); t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); o.GetComponent<MeshRenderer>().sharedMaterial = t.font.material; t.text = text; t.fontSize = 90; t.characterSize = size / 9; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = Grey(.8f);
        }
        public static void Combine(Transform root)
        {
            bool world = root.name.StartsWith("SECTOR");
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var f in root.GetComponentsInChildren<MeshFilter>())
            {
                if (f.GetComponent<TextMesh>()) continue;
                var r = f.GetComponent<MeshRenderer>(); if (!r) continue;
                if (!groups.TryGetValue(r.sharedMaterial, out var list)) groups[r.sharedMaterial] = list = new();
                var geometry = f.sharedMesh;
                if (world)
                {
                    geometry = UnityEngine.Object.Instantiate(geometry); var positions = geometry.vertices; var normals = geometry.normals; var uv = new Vector2[positions.Length];
                    for (int i = 0; i < uv.Length; i++) { var p = f.transform.TransformPoint(positions[i]); var n = f.transform.TransformDirection(normals[i]); if (Mathf.Abs(n.y) >= Mathf.Abs(n.x) && Mathf.Abs(n.y) >= Mathf.Abs(n.z)) uv[i] = new Vector2(p.x,p.z) * .4f; else if (Mathf.Abs(n.x) > Mathf.Abs(n.z)) uv[i] = new Vector2(p.z,p.y) * .4f; else uv[i] = new Vector2(p.x,p.y) * .4f; }
                    geometry.uv = uv; geometry.RecalculateTangents();
                }
                list.Add(new CombineInstance { mesh = geometry, transform = root.worldToLocalMatrix * f.transform.localToWorldMatrix }); r.enabled = false;
            }
            foreach (var pair in groups)
            {
                var o = new GameObject("Batched " + pair.Key.name); o.transform.SetParent(root, false);
                var m = new Mesh { name = o.name, indexFormat = IndexFormat.UInt32 }; m.CombineMeshes(pair.Value.ToArray(), true, true);
                o.AddComponent<MeshFilter>().sharedMesh = m; o.AddComponent<MeshRenderer>().sharedMaterial = pair.Key; o.isStatic = true;
                if (world) foreach (var item in pair.Value) UnityEngine.Object.Destroy(item.mesh);
            }
        }
        static void SetupLight()
        {
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .012f; RenderSettings.fogColor = Grey(.5f);
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = Grey(.68f); RenderSettings.ambientEquatorColor = Grey(.35f); RenderSettings.ambientGroundColor = Grey(.12f); RenderSettings.ambientIntensity = 1;
            var sun = new GameObject("Cloud-filtered sunlight").AddComponent<Light>(); sun.type = LightType.Directional; sun.color = Color.white; sun.intensity = 1.8f; sun.transform.rotation = Quaternion.Euler(35, -32, 0); sun.shadows = LightShadows.Soft; sun.shadowBias = .035f; sun.shadowNormalBias = .28f;
            var light = new GameObject("Door light").AddComponent<Light>(); light.type = LightType.Point; light.color = Color.white; light.intensity = 3; light.range = 10; light.transform.position = new Vector3(0, 4.8f, -22);
            var sky = new Material(Shader.Find("Skybox/Procedural")); sky.SetFloat("_AtmosphereThickness", .7f); sky.SetColor("_SkyTint", Grey(.47f)); sky.SetColor("_GroundColor", Grey(.17f)); sky.SetFloat("_Exposure", 1.1f); RenderSettings.skybox = sky;
            var reflection = new GameObject("Courtyard reflections").AddComponent<ReflectionProbe>(); reflection.transform.position = new Vector3(0, 3, 0); reflection.mode = ReflectionProbeMode.Realtime; reflection.refreshMode = ReflectionProbeRefreshMode.OnAwake; reflection.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce; reflection.resolution = 128; reflection.size = new Vector3(60, 20, 60); reflection.boxProjection = true; reflection.intensity = .8f;
            var dust = new GameObject("Drifting dust").AddComponent<ParticleSystem>(); dust.transform.position = new Vector3(0, 6, 0);
            var main = dust.main; main.startLifetime = 18; main.startSpeed = .035f; main.startSize = .026f; main.startColor = new Color(.8f, .8f, .8f, .28f); main.maxParticles = 180; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = dust.emission; emission.rateOverTime = 8;
            var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(45, 10, 45);
            dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = Resources.Load<Material>("VoidXParticles");
        }
        public static Transform Weapon(Transform parent, int type)
        {
            var root = new GameObject(type == 0 ? "VX—01 Assault Rifle" : "VX—08 Shotgun").transform; root.SetParent(parent, false);
            Part(root, "Receiver", new Vector3(0, 0, .03f), new Vector3(.105f, .115f, .31f), Black);
            Part(root, "Upper receiver", new Vector3(0, .063f, .07f), new Vector3(.09f, .027f, .36f), Steel);
            Part(root, "Handguard", new Vector3(0, -.005f, .31f), new Vector3(.09f, .09f, type == 0 ? .28f : .35f), Steel);
            for (int i = 0; i < 9; i++)
            {
                Part(root, "Picatinny rail", new Vector3(0, .073f, .04f + i * .042f), new Vector3(.106f, .015f, .023f), Black);
                foreach (int s in new[] { -1, 1 }) Part(root, "Cooling vent", new Vector3(s * .047f, .015f, .2f + i * .027f), new Vector3(.004f, .024f, .013f), Black);
            }
            var barrel = Part(root, "Barrel", new Vector3(0, .016f, .54f), new Vector3(.025f, .17f, .025f), Black, PrimitiveType.Cylinder); barrel.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var muzzle = Part(root, "Muzzle brake", new Vector3(0, .016f, .7f), new Vector3(.04f, .035f, .04f), Steel, PrimitiveType.Cylinder); muzzle.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Part(root, "Bore", new Vector3(0, .016f, .737f), new Vector3(.018f, .018f, .002f), Black);
            Part(root, "Stock", new Vector3(0, -.015f, -.23f), new Vector3(.07f, .08f, .26f), Black);
            Part(root, "Butt pad", new Vector3(0, -.035f, -.36f), new Vector3(.09f, .16f, .025f), Steel);
            var grip = Part(root, "Pistol grip", new Vector3(0, -.115f, -.045f), new Vector3(.058f, .15f, .072f), Black); grip.transform.localRotation = Quaternion.Euler(-14, 0, 0);
            var magazine = Part(root, "Magazine", new Vector3(0, -.13f, .08f), new Vector3(.065f, type == 0 ? .22f : .095f, .105f), Steel); magazine.transform.localRotation = Quaternion.Euler(-9, 0, 0);
            Part(root, "Trigger guard", new Vector3(0, -.082f, -.02f), new Vector3(.025f, .013f, .09f), Steel);
            Part(root, "Optic mount", new Vector3(0, .105f, .04f), new Vector3(.09f, .055f, .105f), Black);
            var optic = Part(root, "Reflex sight", new Vector3(0, .154f, .04f), new Vector3(.065f, .042f, .065f), Steel, PrimitiveType.Cylinder); optic.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Part(root, "Optic glass", new Vector3(0, .154f, -.003f), new Vector3(.047f, .047f, .002f), Glass, PrimitiveType.Sphere);
            for (int i = 0; i < 4; i++) foreach (int s in new[] { -1, 1 }) Part(root, "Receiver screws", new Vector3(s * .054f, .025f, -.065f + i * .06f), new Vector3(.006f, .011f, .011f), White, PrimitiveType.Sphere);
            // Arms with segmented gloves, sleeve seam and wrist cuffs.
            foreach (int side in new[] { -1, 1 })
            {
                var arm = Part(root, "Sleeve", new Vector3(side * .1f, -.155f, side == -1 ? .28f : -.075f), new Vector3(.09f, .28f, .1f), Concrete, PrimitiveType.Capsule); arm.transform.localRotation = Quaternion.Euler(48, side * 12, side * -20);
                Part(root, "Glove", new Vector3(side * .054f, -.06f, side == -1 ? .28f : -.075f), new Vector3(.065f, .075f, .11f), Black, PrimitiveType.Capsule);
                for (int f = 0; f < 4; f++) Part(root, "Finger", new Vector3(side * .057f, -.08f + f * .018f, side == -1 ? .27f : -.063f), new Vector3(.075f, .015f, .028f), Steel, PrimitiveType.Capsule);
            }
            Combine(root); foreach (var r in root.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            return root;
        }
        public static Transform Operative(Transform parent, out Transform[] legs)
        {
            var root = new GameObject("Void operative").transform; root.SetParent(parent, false);
            Part(root, "Armour torso", new Vector3(0, 1.08f, 0), new Vector3(.5f, .51f, .25f), Black, PrimitiveType.Capsule);
            Part(root, "Plate carrier", new Vector3(0, 1.17f, .15f), new Vector3(.38f, .32f, .08f), Steel);
            for (int i = -1; i <= 1; i++) Part(root, "Magazine pouch", new Vector3(i * .105f, 1.02f, .2f), new Vector3(.085f, .16f, .065f), Concrete);
            Part(root, "Radio", new Vector3(.22f, 1.35f, .09f), new Vector3(.06f, .12f, .06f), Steel);
            Part(root, "Backpack", new Vector3(0, 1.2f, -.21f), new Vector3(.33f, .36f, .13f), Concrete);
            Part(root, "Helmet", new Vector3(0, 1.61f, 0), new Vector3(.31f, .29f, .3f), Concrete, PrimitiveType.Sphere);
            Part(root, "Face mask", new Vector3(0, 1.51f, .11f), new Vector3(.22f, .14f, .16f), Black, PrimitiveType.Sphere);
            Part(root, "Visor", new Vector3(0, 1.64f, .139f), new Vector3(.24f, .077f, .075f), Glass);
            Part(root, "Helmet stripe", new Vector3(0, 1.76f, .04f), new Vector3(.05f, .045f, .16f), White);
            foreach (int side in new[] { -1, 1 })
            {
                Part(root, "Arm", new Vector3(side * .29f, 1.18f, .095f), new Vector3(.14f, .34f, .16f), Concrete, PrimitiveType.Capsule).transform.localRotation = Quaternion.Euler(-50, 0, side * 15);
                Part(root, "Gloved hand", new Vector3(side * .16f, 1.03f, .27f), new Vector3(.1f, .09f, .12f), Black, PrimitiveType.Sphere);
            }
            Part(root, "Enemy rifle", new Vector3(.05f, 1.1f, .37f), new Vector3(.075f, .09f, .49f), Black);
            Part(root, "Enemy rail", new Vector3(.05f, 1.16f, .4f), new Vector3(.06f, .025f, .36f), Steel);
            legs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var leg = new GameObject("Animated leg").transform; leg.SetParent(root, false); leg.localPosition = new Vector3(i == 0 ? -.13f : .13f, .81f, 0); legs[i] = leg;
                Part(leg, "Trousers", new Vector3(0, -.31f, 0), new Vector3(.18f, .64f, .2f), Concrete, PrimitiveType.Capsule);
                Part(leg, "Knee pad", new Vector3(0, -.32f, .1f), new Vector3(.14f, .15f, .07f), Steel);
                Part(leg, "Boot", new Vector3(0, -.72f, .035f), new Vector3(.19f, .18f, .31f), Black);
            }
            return root;
        }
    }
}
