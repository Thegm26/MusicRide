using UnityEngine;

namespace MusicRoad
{
    public sealed class MusicRoadBootstrap : MonoBehaviour
    {
        public const int VehicleRenderLayer = 30;
        [SerializeField] private Shader runtimeShader;
        [SerializeField] private GameObject[] vehiclePrefabs;
        [SerializeField] private GameObject[] environmentPrefabs;
        private bool gameStarted;

        public void SetRuntimeShader(Shader shader)
        {
            runtimeShader = shader;
        }

        public void SetImportedAssets(GameObject[] vehicles, GameObject[] environment)
        {
            vehiclePrefabs = vehicles;
            environmentPrefabs = environment;
        }

        private void Awake()
        {
            if (FindAnyObjectByType<ArcadeCarController>() != null)
            {
                return;
            }

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Physics.gravity = new Vector3(0f, -18f, 0f);
            VehicleSelectionMenu.Show(VehicleCatalog.All, vehiclePrefabs, StartGame);
        }

        private void StartGame(int selectedIndex)
        {
            if (gameStarted)
            {
                return;
            }

            gameStarted = true;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, VehicleCatalog.All.Length - 1);
            VehicleSpec vehicle = VehicleCatalog.All[selectedIndex];
            Shader shader = runtimeShader != null ? runtimeShader : Shader.Find("MusicRoad/Reactive");
            Material roadMaterial = CreateMaterial(shader, "Road", new Color(0.09f, 0.1f, 0.14f), 0.25f);
            Material shoulderMaterial = CreateMaterial(shader, "Shoulder", new Color(0.19f, 0.35f, 0.27f), 0f);
            Material edgeMaterial = CreateMaterial(shader, "Music Lane Strips", Color.white, 0.4f, true);
            Material carMaterial = CreateMaterial(shader, "Toy Car", new Color(1f, 0.18f, 0.16f), 0.65f);
            Material darkMaterial = CreateMaterial(shader, "Toy Dark", new Color(0.025f, 0.03f, 0.045f), 0.35f);
            Material glassMaterial = CreateMaterial(shader, "Toy Glass", new Color(0.18f, 0.65f, 0.9f), 0.8f);
            Material flameMaterial = CreateMaterial(shader, "Nitro Flame", new Color(1f, 0.58f, 0.04f), 0f, true);
            Material windMaterial = CreateMaterial(shader, "Peak Wind", new Color(0.88f, 0.96f, 1f), 0f, true);
            Material trunkMaterial = CreateMaterial(shader, "Environment Trunks", new Color(0.24f, 0.12f, 0.055f), 0f);
            Material foliageMaterial = CreateMaterial(shader, "Environment Foliage", new Color(0.08f, 0.34f, 0.14f), 0f, true);
            Material rockMaterial = CreateMaterial(shader, "Environment Rocks", new Color(0.3f, 0.34f, 0.38f), 0.05f);

            AudioCaptureService capture = new GameObject("AudioCaptureService").AddComponent<AudioCaptureService>();
            Light sun = CreateLighting();
            MusicWorldController world = new GameObject("Music World Controller").AddComponent<MusicWorldController>();
            world.Initialize(capture, sun, edgeMaterial, foliageMaterial);

            GameObject selectedPrefab = vehiclePrefabs != null && selectedIndex < vehiclePrefabs.Length
                ? vehiclePrefabs[selectedIndex]
                : null;
            GameObject carObject = CreatePlayerCar(selectedPrefab, vehicle, carMaterial, darkMaterial, glassMaterial, flameMaterial);
            ArcadeCarController car = carObject.GetComponent<ArcadeCarController>();

            RoadGenerator road = new GameObject("Procedural Music Road").AddComponent<RoadGenerator>();
            road.Initialize(
                world,
                carObject.transform,
                roadMaterial,
                shoulderMaterial,
                edgeMaterial,
                trunkMaterial,
                foliageMaterial,
                rockMaterial,
                environmentPrefabs);
            car.Initialize(road);
            car.PlaceAtStart();

            CreateCamera(carObject.transform, world);
            MusicPeakWind wind = new GameObject("Maximum Music Wind").AddComponent<MusicPeakWind>();
            wind.Initialize(world, carObject.transform, windMaterial);

            RunManager run = new GameObject("Run Manager").AddComponent<RunManager>();
            run.Initialize(capture, car);
        }

        private static Material CreateMaterial(Shader shader, string name, Color color, float smoothness, bool emissive = false)
        {
            if (shader == null)
            {
                throw new System.InvalidOperationException("MusicRoad/Reactive shader is missing. Refresh the main scene from the Music Road editor menu.");
            }

            Material material = new Material(shader)
            {
                name = name,
                color = color
            };

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.5f);
            }

            return material;
        }

        private static Light CreateLighting()
        {
            GameObject lightObject = new GameObject("Music Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.intensity = 1f;
            light.cullingMask &= ~(1 << VehicleRenderLayer);
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = light;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            return light;
        }

        private static GameObject CreatePlayerCar(
            GameObject visualPrefab,
            VehicleSpec vehicle,
            Material bodyMaterial,
            Material darkMaterial,
            Material glassMaterial,
            Material flameMaterial)
        {
            GameObject root = new GameObject($"Player {vehicle.DisplayName}");
            root.transform.position = new Vector3(0f, 1f, 4f);
            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = vehicle.ColliderSize;
            collider.center = vehicle.ColliderCenter;
            ArcadeCarController controller = root.AddComponent<ArcadeCarController>();
            controller.ConfigureVehicle(vehicle);

            if (visualPrefab != null)
            {
                GameObject visual = Instantiate(visualPrefab, root.transform);
                visual.name = $"{vehicle.DisplayName} Visual";
                visual.transform.localPosition = vehicle.VisualOffset;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                StripVisualPhysics(visual);
                StabilizeVehicleMaterials(visual);
            }
            else
            {
                CreateFallbackToyVisual(root.transform, bodyMaterial, darkMaterial, glassMaterial);
            }

            if (vehicle.CanNitro)
            {
                float rear = -vehicle.ColliderSize.z * 0.5f - 0.15f;
                float side = Mathf.Min(0.58f, vehicle.ColliderSize.x * 0.27f);
                Transform[] nitroFlames =
                {
                    CreateNitroFlame(root.transform, "Left Nitro Flame", new Vector3(-side, 0f, rear), flameMaterial),
                    CreateNitroFlame(root.transform, "Right Nitro Flame", new Vector3(side, 0f, rear), flameMaterial)
                };
                controller.ConfigureNitroFlames(nitroFlames);
            }

            SetLayerRecursively(root, VehicleRenderLayer);
            CreateVehicleLighting(root.transform, vehicle.ColliderSize);
            rigidbody.centerOfMass = new Vector3(0f, -0.45f, 0f);
            return root;
        }

        private static void CreateVehicleLighting(Transform car, Vector3 carSize)
        {
            CreateVehicleLight(
                car,
                "Neutral Car Key Light",
                new Vector3(-carSize.x * 0.65f, carSize.y + 2.1f, -0.8f),
                2.15f,
                7.5f);
            CreateVehicleLight(
                car,
                "Neutral Car Fill Light",
                new Vector3(carSize.x * 0.75f, carSize.y + 0.9f, 1.4f),
                1.1f,
                6f);
        }

        private static void CreateVehicleLight(
            Transform parent,
            string lightName,
            Vector3 localPosition,
            float intensity,
            float range)
        {
            GameObject lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = localPosition;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.white;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << VehicleRenderLayer;
        }

        private static void StabilizeVehicleMaterials(GameObject visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].materials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null || !material.HasProperty("_EmissionColor"))
                    {
                        continue;
                    }

                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.white * 0.22f);
                    if (material.mainTexture != null && material.HasProperty("_EmissionMap"))
                    {
                        material.SetTexture("_EmissionMap", material.mainTexture);
                    }
                }
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void StripVisualPhysics(GameObject visual)
        {
            Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }

            Rigidbody[] rigidbodies = visual.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Destroy(rigidbodies[i]);
            }
        }

        private static void CreateFallbackToyVisual(Transform root, Material bodyMaterial, Material darkMaterial, Material glassMaterial)
        {
            AddPrimitive(root, "Body", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(1.85f, 0.55f, 3.25f), bodyMaterial);
            AddPrimitive(root, "Cabin", PrimitiveType.Cube, new Vector3(0f, 0.67f, -0.28f), new Vector3(1.45f, 0.72f, 1.55f), glassMaterial);
            AddPrimitive(root, "Front Bumper", PrimitiveType.Cube, new Vector3(0f, -0.02f, 1.72f), new Vector3(1.95f, 0.18f, 0.22f), darkMaterial);
            AddPrimitive(root, "Rear Bumper", PrimitiveType.Cube, new Vector3(0f, -0.02f, -1.72f), new Vector3(1.95f, 0.18f, 0.22f), darkMaterial);

            Vector3[] wheelPositions =
            {
                new Vector3(-1f, -0.18f, 1.08f),
                new Vector3(1f, -0.18f, 1.08f),
                new Vector3(-1f, -0.18f, -1.08f),
                new Vector3(1f, -0.18f, -1.08f)
            };
            for (int i = 0; i < wheelPositions.Length; i++)
            {
                GameObject wheel = AddPrimitive(root, $"Wheel {i + 1}", PrimitiveType.Cylinder, wheelPositions[i], new Vector3(0.52f, 0.28f, 0.52f), darkMaterial);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private static Transform CreateNitroFlame(Transform parent, string name, Vector3 position, Material material)
        {
            GameObject flame = new GameObject(name);
            flame.transform.SetParent(parent, false);
            flame.transform.localPosition = position;

            Mesh mesh = new Mesh { name = $"{name} Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.2f, -0.2f, 0f),
                new Vector3(0.2f, -0.2f, 0f),
                new Vector3(0.2f, 0.2f, 0f),
                new Vector3(-0.2f, 0.2f, 0f),
                new Vector3(0f, 0f, -1.65f)
            };
            mesh.triangles = new[]
            {
                0, 1, 4,
                1, 2, 4,
                2, 3, 4,
                3, 0, 4,
                0, 3, 2,
                0, 2, 1
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            flame.AddComponent<MeshFilter>().sharedMesh = mesh;
            flame.AddComponent<MeshRenderer>().sharedMaterial = material;
            return flame.transform;
        }

        private static GameObject AddPrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Collider primitiveCollider = primitive.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                Destroy(primitiveCollider);
            }

            return primitive;
        }

        private static void CreateCamera(Transform car, MusicWorldController world)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 230f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.22f, 0.34f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ChaseCamera>().Initialize(car, world);
        }

    }
}
