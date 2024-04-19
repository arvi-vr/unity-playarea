using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using UnityEngine.Events;

#if ARVI_PROVIDER_OPENXR
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
#endif

#if ARVI_PROVIDER_OPENVR
using UnityEngine.Rendering;
using Valve.VR;
#endif

using ARVI.SDK;

namespace ARVI.PlayArea
{
    public class PlayArea : MonoBehaviour
    {
        private const int PLAY_AREA_MAJOR_VERSION = 1;
        private const int PLAY_AREA_MINOR_VERSION = 0;

        [Header("Tracking Components")]

        [Tooltip("Left controller transform")]
        public Transform leftController;

        [Tooltip("Right controller transform")]
        public Transform rightController;

        [Tooltip("Camera transform. Usually matches the VR camera transform")]
        public new Transform camera;

        [Tooltip("Camera rig transform. Used to synchronize the position and rotation of play area")]
        public Transform cameraRig;

        [Header("Play Area Settings")]

        [SerializeField]
        [Tooltip("Should to check the boundaries of play area")]
        private PlayAreaCheckingMode checkingMode = PlayAreaCheckingMode.Auto;

        [SerializeField]
        [Tooltip("Height of the play area grid")]
#pragma warning disable CS0414
        private float playAreaHeight = 3f;
#pragma warning restore CS0414

        [SerializeField]
        [Tooltip("An array of headset models that have their own play area. For these headsets, the play area will not be checked")]
        private string[] headsetsWithOwnPlayArea;

        [Header("Bounds Detection Settings")]

        [SerializeField]
        [Tooltip("The distance from which the grid for the left controller will start to appear")]
        private float leftControllerDetectionDistance = 0.3f;

        [SerializeField]
        [Tooltip("The distance from which the grid for the right controller will start to appear")]
        private float rightControllerDetectionDistance = 0.3f;

        [SerializeField]
        [Tooltip("Play area size in standing mode")]
#pragma warning disable CS0414
        private float standingModePlayAreaSize = 1.6f;
#pragma warning restore CS0414

        [SerializeField]
        private float minDistanceToStartShowCell = 0.4f;
        
        [SerializeField]
        private float minDistanceToStartShowTextureInsideCell = 0.05f;
        
        [SerializeField]
        private float offsetDistance = 0.25f;

        [Header("Out Of Bounds Area Settings")]

        [SerializeField]
        [Tooltip("Allows to activate or deactivate play area out of bounds mode")]
        private PlayAreaOutOfBoundsMode outOfBoundsMode = PlayAreaOutOfBoundsMode.Auto;

        [SerializeField]
        [Tooltip("A GameObject that activates when a player moves outside the play area")]
        private GameObject outOfBoundsArea;

        [SerializeField]
        [Tooltip("Size of the square area within which the OutOfBounds area is deactivated")]
        private float playAreaCenterThreshold = 0.5f;

        [SerializeField]
        [Tooltip("Render Play Area bounds when OutOfBounds area displayed")]
        private bool renderPlayAreaWhenOutOfBounds = false;

        [SerializeField]
        [Tooltip("Apply PostProcessing when rendering the OutOfBounds area")]
        private bool renderPostProcessingWhenOutOfBounds = false;

        [Header("Controllers Velocity Settings")]

        [SerializeField]
        [Tooltip("The width of the safe zone within which the controllers velocity does not affect the play area grid")]
        private float safeZoneWidth = 0.7f;

        [SerializeField]
        [Tooltip("The depth of the safe zone within which the controllers velocity does not affect the play area grid")]
        private float safeZoneDepth = 0.7f;

        [SerializeField]
        [Tooltip("Minimum controllers velocity that affects the play area grid")]
        private float minControllersVelocityToAffectTheGrid = 0.2f;

        [SerializeField]
        [Tooltip("Maximum additional detection distance for controllers when their velocity exceeds the maximum")]
        private float maxControllersVelocityDetectionDistance = 0.2f;

        [Header("Debug")]

        [SerializeField]
        [Tooltip("Do not require ARVI SDK initialization. Useful for debugging in the Editor")]
#pragma warning disable CS0414
        private bool skipSDKInitialization = false;
#pragma warning restore CS0414

        [Header("Events")]

        [Tooltip("The event is raised when the Play Area mesh is created and ready to use")]
        public UnityEvent OnPlayAreaReady;

        [Tooltip("The event is raised when a player leaves to the play area boundaries")]
        public UnityEvent OnPlayerOutOfBounds;

        [Tooltip("The event is raised when a player returns to the play area boundaries")]
        public UnityEvent OnPlayerInBounds;

        public const string PLAY_AREA_LAYER_NAME = "ARVI Play Area";

        private const string LEFT_CONTROLLER_POSITION_NAME = "_LeftControllerPosition";
        private const string RIGHT_CONTROLLER_POSITION_NAME = "_RightControllerPosition";
        private const string HEADSET_POSITION_NAME = "_HeadsetPosition";
        private const string LEFT_CONTROLLER_DETECTION_DISTANCE_NAME = "_LeftControllerSphereRadius";
        private const string RIGHT_CONTROLLER_DETECTION_DISTANCE_NAME = "_RightControllerSphereRadius";
        private const string CLOSEST_SIDE_DIRECTION = "_ClosestSideDirection";
        private const string HEADSET_DIRECTION = "_HeadsetDirection";
        private const string CELL_TEXTURE_SIZE = "_CellTextureSize";
        private const string INSIDE_CELL_TEXTURE_ALPHA = "_InsideCellTextureAlpha";

        private const string PLAY_AREA_MATERIAL_NAME = "ARVI Play Area Material";

        private int leftControllerPositionID;
        private int rightControllerPositionID;
        private int headsetPositionID;
        private int leftControllerDetectionDistanceID;
        private int rightControllerDetectionDistanceID;
        private int closestSideDirectionID;
        private int headsetDirectionID;
        private int cellTextureSizeID;
        private int insideCellTextureAlphaID;

        private InputDevice hmdDevice;
        private Plane[] playAreaSides;
        private Vector2[] playAreaLocalPoints;
        private bool playAreaInitialized;
        private Material playAreaMaterial;

        private float leftControllerVelocity;
        private Vector3 previousLeftControllerPosition;
        private float calculatedLeftControllerDetectionDistance;

        private float rightControllerVelocity;
        private Vector3 previousRightControllerPosition;
        private float calculatedRightControllerDetectionDistance;

        private CameraSettings originalCameraSettings = null;

        private bool shouldCheckPlayArea;
        private bool shouldUseOfBoundsArea;

        private class CameraSettings
        {
            public int cullingMask;
            public CameraClearFlags clearFlags;
            public Color backgroundColor;
            public bool renderPostProcessing;

            public CameraSettings(int cullingMask, CameraClearFlags clearFlags, Color backgroundColor, bool renderPostProcessing)
            {
                this.cullingMask = cullingMask;
                this.clearFlags = clearFlags;
                this.backgroundColor = backgroundColor;
                this.renderPostProcessing = renderPostProcessing;
            }

            public CameraSettings(Camera camera)
            {
                if (camera)
                {
                    cullingMask = camera.cullingMask;
                    clearFlags = camera.clearFlags;
                    backgroundColor = camera.backgroundColor;
                    var cameraData = camera.GetUniversalAdditionalCameraData();
                    renderPostProcessing = cameraData.renderPostProcessing;
                }
            }
        }

        public static PlayArea Instance { get; private set; }

        public InputDevice HMDDevice
        {
            get
            {
                if (hmdDevice.isValid)
                    return hmdDevice;
                hmdDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                return hmdDevice;
            }
        }

        public static string Version
        {
            get
            {
                return string.Format("{0}.{1}", PLAY_AREA_MAJOR_VERSION, PLAY_AREA_MINOR_VERSION);
            }
        }

        protected virtual void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(this);
                return;
            }

            leftControllerPositionID = Shader.PropertyToID(LEFT_CONTROLLER_POSITION_NAME);
            rightControllerPositionID = Shader.PropertyToID(RIGHT_CONTROLLER_POSITION_NAME);
            headsetPositionID = Shader.PropertyToID(HEADSET_POSITION_NAME);
            leftControllerDetectionDistanceID = Shader.PropertyToID(LEFT_CONTROLLER_DETECTION_DISTANCE_NAME);
            rightControllerDetectionDistanceID = Shader.PropertyToID(RIGHT_CONTROLLER_DETECTION_DISTANCE_NAME);
            closestSideDirectionID = Shader.PropertyToID(CLOSEST_SIDE_DIRECTION);
            headsetDirectionID = Shader.PropertyToID(HEADSET_DIRECTION);
            cellTextureSizeID = Shader.PropertyToID(CELL_TEXTURE_SIZE);
            insideCellTextureAlphaID = Shader.PropertyToID(INSIDE_CELL_TEXTURE_ALPHA);

            playAreaMaterial = Resources.Load<Material>(PLAY_AREA_MATERIAL_NAME);

            if (outOfBoundsArea)
                SetPlayAreaLayerRecursive(outOfBoundsArea.transform);

            ShowOutOfBoundsArea(false);
        }

        protected virtual void Start()
        {
            originalCameraSettings = CreateCameraSettings();

#if ARVI_PROVIDER_OPENVR || ARVI_PROVIDER_OPENXR
            if (!skipSDKInitialization)
            {
                // Initialize SDK
                if (!Integration.Initialize())
                {
                    Debug.LogWarning("Failed to start ARVI Play Area because of ARVI SDK is not initialized");
                    enabled = false;
                    return;
                }
            }

            // Wait for headset
            StartCoroutine(WaitForHeadsetRoutine(() =>
            {
                if (TryGetPlayAreaRect(out playAreaLocalPoints))
                {
                    shouldCheckPlayArea = ShouldCheckPlayArea();
                    shouldUseOfBoundsArea = ShouldUseOutOfBoundsArea();

                    if (shouldCheckPlayArea)
                    {
                        var playAreaMesh = CreatePlayAreaMesh(playAreaLocalPoints, playAreaHeight);
                        // Play area MeshFilter
                        var playAreaMeshMeshFilter = gameObject.AddComponent<MeshFilter>();
                        playAreaMeshMeshFilter.mesh = playAreaMesh;
                        // Play area MeshRenderer
                        var playAreaRenderer = gameObject.AddComponent<MeshRenderer>();
                        playAreaRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        playAreaRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        playAreaRenderer.receiveShadows = false;
                        playAreaRenderer.lightProbeUsage = LightProbeUsage.Off;
                        playAreaRenderer.material = playAreaMaterial;
                        // Should render Play Area when out of bounds?
                        if (renderPlayAreaWhenOutOfBounds)
                            gameObject.layer = LayerMask.NameToLayer(PLAY_AREA_LAYER_NAME);
                    }

                    playAreaInitialized = true;

                    OnPlayAreaReady?.Invoke();

                    // If nothing to check - disable component
                    if (!shouldCheckPlayArea && !shouldUseOfBoundsArea)
                        enabled = false;
                }
                else
                {
                    Debug.LogWarning("Failed to get play area rect");
                    enabled = false;
                }
            }));
#else
            Debug.LogError("VR provider is not set. Select your provider in \"Provider\" field");
            enabled = false;
#endif
        }

        protected virtual void Update()
        {
            if (playAreaInitialized)
            {
                // Synchronize play area position and rotation with camera rig
                if (cameraRig)
                    transform.SetPositionAndRotation(cameraRig.position, cameraRig.rotation);
                // Update visibility of play area bounds
                if (shouldCheckPlayArea)
                    CheckPlayAreaBounds();
                // Check if player moves out of play area bounds
                if (shouldUseOfBoundsArea)
                    CheckPlayAreaOutOfBounds();
            }
        }

        protected virtual void CheckPlayAreaBounds()
        {
            // Check if headset is inside the safe zone
            var isHeadsetInSafeZone = IsHeadsetInSafeZone();
            // Calculate controllers detection distance taking into account controllers velocity
            calculatedLeftControllerDetectionDistance = Mathf.Lerp(calculatedLeftControllerDetectionDistance, CalculateLeftControllerDetectionDistance(isHeadsetInSafeZone), 2f * Time.unscaledDeltaTime);
            calculatedRightControllerDetectionDistance = Mathf.Lerp(calculatedRightControllerDetectionDistance, CalculateRightControllerDetectionDistance(isHeadsetInSafeZone), 2f * Time.unscaledDeltaTime);
            // Update play area material properties
            if (playAreaMaterial)
            {
                // Update controllers detection distance
                playAreaMaterial.SetFloat(leftControllerDetectionDistanceID, calculatedLeftControllerDetectionDistance);
                playAreaMaterial.SetFloat(rightControllerDetectionDistanceID, calculatedRightControllerDetectionDistance);
                // Update left controller position
                playAreaMaterial.SetVector(leftControllerPositionID, leftController ? leftController.position : Vector3.zero);
                // Update right controller position
                playAreaMaterial.SetVector(rightControllerPositionID, rightController ? rightController.position : Vector3.zero);
                // Update headset position and direction
                playAreaMaterial.SetVector(headsetPositionID, camera ? camera.position : Vector3.zero);
                playAreaMaterial.SetVector(headsetDirectionID, camera ? transform.InverseTransformDirection(camera.forward) : Vector3.zero);
                // Find closest side
                if (TryGetClosestSide(transform.InverseTransformPoint(camera.position), out float distanceToSide, out Plane side))
                {
                    distanceToSide -= offsetDistance;
                    playAreaMaterial.SetVector(closestSideDirectionID, side.normal);
                    playAreaMaterial.SetFloat(cellTextureSizeID, 1f - Mathf.Clamp01(distanceToSide / (minDistanceToStartShowCell - offsetDistance)));
                    playAreaMaterial.SetFloat(insideCellTextureAlphaID, Mathf.Clamp01(distanceToSide / (minDistanceToStartShowTextureInsideCell - offsetDistance)));
                }
            }
        }

        protected virtual void CheckPlayAreaOutOfBounds()
        {
            // Check if the player inside the play area
            var headsetPosition = transform.InverseTransformPoint(camera ? camera.position : Vector3.zero);
            if (IsPointInGameZone(headsetPosition))
            {
                // Check if the player near the center of play area
                if (IsHeadsetNearGameZoneCenter(headsetPosition))
                {
                    // Dectivate OutOfBounds area
                    ShowOutOfBoundsArea(false);
                }
            }
            else
            {
                // Activate OutOfBounds area
                ShowOutOfBoundsArea(true);
            }
        }

        private CameraSettings CreateCameraSettings()
        {
            if (!this.camera)
                return null;
            if (this.camera.TryGetComponent<Camera>(out var camera))
                return new CameraSettings(camera);
            else
                return null;
        }

        private bool SetPlayAreaCulling(bool culled)
        {
            if (originalCameraSettings == null)
                return false;
            if (!this.camera)
                return false;
            if (!this.camera.TryGetComponent<Camera>(out var camera))
                return false;

            camera.backgroundColor = culled ? Color.black : originalCameraSettings.backgroundColor;
            camera.clearFlags = culled ? CameraClearFlags.Color : originalCameraSettings.clearFlags;
            camera.cullingMask = culled ? LayerMask.GetMask(PLAY_AREA_LAYER_NAME) : originalCameraSettings.cullingMask;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            if (culled && !renderPostProcessingWhenOutOfBounds)
                cameraData.renderPostProcessing = false;
            else
                cameraData.renderPostProcessing = originalCameraSettings.renderPostProcessing;
            return true;
        }

        private bool IsHeadsetNearGameZoneCenter(Vector3 headsetPosition)
        {
            return (Mathf.Abs(headsetPosition.x) < playAreaCenterThreshold) && (Mathf.Abs(headsetPosition.z) < playAreaCenterThreshold);
        }

        private bool PolygonContainsPoint(ref Vector2[] polygonPoints, Vector2 point)
        {
            int j = polygonPoints.Length - 1;
            bool inside = false;
            Vector2 pi;
            Vector2 pj;
            for (int i = 0; i < polygonPoints.Length; j = i++)
            {
                pi = polygonPoints[i];
                pj = polygonPoints[j];
                if (((pi.y <= point.y && point.y < pj.y) || (pj.y <= point.y && point.y < pi.y)) &&
                    (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x))
                    inside = !inside;
            }
            return inside;
        }

        protected virtual void ShowOutOfBoundsArea(bool show)
        {
            if (outOfBoundsArea)
            {
                // Apply play area culling
                SetPlayAreaCulling(show);
                // Activate/deactivate OutOfBounds area object
                outOfBoundsArea.SetActive(show);
            }

            if (show)
                OnPlayerOutOfBounds?.Invoke();
            else
                OnPlayerInBounds?.Invoke();
        }

        protected bool IsPointInGameZone(Vector3 point)
        {
            // входит ли позиция шлема в игровую зону
            return PolygonContainsPoint(ref playAreaLocalPoints, new Vector2(point.x, point.z));
        }

        protected virtual void UpdateLeftControllerVelocity()
        {
            if (leftController)
            {
                leftControllerVelocity = (leftController.position - previousLeftControllerPosition).magnitude / Time.unscaledDeltaTime;
                previousLeftControllerPosition = leftController.position;
            }
            else
                leftControllerVelocity = 0f;
        }

        protected virtual void UpdateRightControllerVelocity()
        {
            if (rightController)
            {
                rightControllerVelocity = (rightController.position - previousRightControllerPosition).magnitude / Time.unscaledDeltaTime;
                previousRightControllerPosition = rightController.position;
            }
            else
                rightControllerVelocity = 0f;
        }

        protected virtual float CalculateLeftControllerDetectionDistance(bool isHeadsetInSafeZone)
        {
            UpdateLeftControllerVelocity();
            // When the headset is outside the safe zone - add extra controller detection distance
            if (leftControllerVelocity > minControllersVelocityToAffectTheGrid && !isHeadsetInSafeZone)
                return leftControllerDetectionDistance + Mathf.Lerp(0f, maxControllersVelocityDetectionDistance, Mathf.Clamp01(leftControllerVelocity));
            else
                return leftControllerDetectionDistance;
        }

        protected virtual float CalculateRightControllerDetectionDistance(bool isHeadsetInSafeZone)
        {
            UpdateRightControllerVelocity();
            // When the headset is outside the safe zone - add extra controller detection distance
            if (rightControllerVelocity > minControllersVelocityToAffectTheGrid && !isHeadsetInSafeZone)
                return rightControllerDetectionDistance + Mathf.Lerp(0f, maxControllersVelocityDetectionDistance, Mathf.Clamp01(rightControllerVelocity));
            else
                return rightControllerDetectionDistance;
        }

        protected virtual bool ShouldCheckPlayArea()
        {
            // Get play area checking mode from SDK
            var mode = Integration.PlayAreaCheckingMode;
            switch (mode)
            {
                case PlayAreaCheckingMode.Enabled:
                    return true;
                case PlayAreaCheckingMode.Disabled:
                    return false;
                default:
                    {
                        // If SDK has not set the checking mode - use checkingMode value
                        switch (checkingMode)
                        {
                            case PlayAreaCheckingMode.Enabled:
                                return true;
                            case PlayAreaCheckingMode.Disabled:
                                return false;
                            default:
                                // Check whether headset has its own play area
                                return !HasOwnPlayArea(HMDDevice);
                        }
                    }
            }
        }

        protected virtual bool ShouldUseOutOfBoundsArea()
        {
            // Get play area out of bounds mode from SDK
            var mode = Integration.PlayAreaOutOfBoundsMode;
            switch (mode)
            {
                case PlayAreaOutOfBoundsMode.Block:
                    return true;
                case PlayAreaOutOfBoundsMode.Ignore:
                    return false;
                default:
                    {
                        // If SDK has not set out of bounds mode - use outOfBoundsMode value
                        switch (outOfBoundsMode)
                        {
                            case PlayAreaOutOfBoundsMode.Block:
                                return true;
                            case PlayAreaOutOfBoundsMode.Ignore:
                                return false;
                            default:
                                // Check whether headset has its own play area
                                return !HasOwnPlayArea(HMDDevice);
                        }
                    }
            }
        }

        protected virtual bool HasOwnPlayArea(InputDevice headset)
        {
            foreach (string modelName in headsetsWithOwnPlayArea)
                if (headset.name.Contains(modelName, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            return false;
        }

        private float GetTotalLength(ref Vector2[] playAreaPoints)
        {
            float totalLength = 0f;

            for (int i = 0; i < playAreaPoints.Length; i++)
            {
                Vector2 startPoint = playAreaPoints[i];
                Vector2 endPoint = playAreaPoints[(i + 1) % playAreaPoints.Length];

                totalLength += Vector2.Distance(startPoint, endPoint);
            }

            return totalLength;
        }

        protected virtual Mesh CreatePlayAreaMesh(Vector2[] playAreaPoints, float height)
        {
            int pointsCount = playAreaPoints.Length;
            if (pointsCount < 2) return null;

            float totalLength = GetTotalLength(ref playAreaPoints);
            float accumulatedLength = 0f;

            List<Vector3> verticesList = new List<Vector3>();
            List<int> trianglesList = new List<int>();
            List<Vector2> uvsList = new List<Vector2>();
            List<Vector2> uvs2List = new List<Vector2>();

            for (int i = 0; i < pointsCount; i++)
            {
                Vector2 startPoint = playAreaPoints[i];
                Vector2 endPoint = playAreaPoints[(i + 1) % pointsCount];

                Vector3[] vertices = new Vector3[4];
                vertices[0] = new Vector3(startPoint.x, 0, startPoint.y);
                vertices[1] = new Vector3(startPoint.x, height, startPoint.y);
                vertices[2] = new Vector3(endPoint.x, height, endPoint.y);
                vertices[3] = new Vector3(endPoint.x, 0, endPoint.y);

                int[] triangles = new int[6];
                int baseIndex = verticesList.Count;
                triangles[0] = baseIndex;
                triangles[1] = baseIndex + 1;
                triangles[2] = baseIndex + 2;
                triangles[3] = baseIndex + 2;
                triangles[4] = baseIndex + 3;
                triangles[5] = baseIndex;

                float startUV = accumulatedLength / totalLength;
                float endUV = (accumulatedLength + Vector2.Distance(startPoint, endPoint)) / totalLength;
                Vector2[] uvs = new Vector2[4];
                uvs[0] = new Vector2(startUV, 0);
                uvs[1] = new Vector2(startUV, height / totalLength);
                uvs[2] = new Vector2(endUV, height / totalLength);
                uvs[3] = new Vector2(endUV, 0);
                
                Vector2[] uvs2 = new Vector2[4];
                uvs2[0] = new Vector2(0, 0); 
                uvs2[1] = new Vector2(1, 1); 
                uvs2[2] = new Vector2(1, 1);
                uvs2[3] = new Vector2(0, 0); 

                uvs2List.AddRange(uvs2); 

                verticesList.AddRange(vertices);
                trianglesList.AddRange(triangles);
                uvsList.AddRange(uvs);

                accumulatedLength += Vector2.Distance(startPoint, endPoint);
            }

            playAreaSides = CreatePlanes(playAreaPoints, CalculateCenter(playAreaPoints));
            
            Mesh mesh = new Mesh();
            mesh.SetVertices(verticesList);
            mesh.SetTriangles(trianglesList, 0);
            mesh.SetUVs(0, uvsList);
            mesh.SetUVs(1, uvs2List);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        
        private Vector2 CalculateCenter(Vector2[] points)
        {
            Vector2 center = Vector2.zero;
            foreach (var point in points)
            {
                center += point;
            }
            center /= points.Length;
            return center;
        }
        
        private Plane[] CreatePlanes(Vector2[] points, Vector3 center)
        {
            int pointsCount = points.Length;
            Plane[] planes = new Plane[pointsCount];

            for (int i = 0; i < pointsCount; i++)
            {
                Vector2 startPoint = points[i];
                Vector2 endPoint = points[(i + 1) % pointsCount];

                Vector3 start3D = new Vector3(startPoint.x, 0, startPoint.y);
                Vector3 end3D = new Vector3(endPoint.x, 0, endPoint.y);

                Vector3 dir = (end3D - start3D).normalized;
                Vector3 normal = Vector3.Cross(dir, Vector3.up);

                Vector3 middlePoint = (start3D + end3D) * 0.5f;
                Vector3 directionToCenter = center - middlePoint;
                if (Vector3.Dot(normal, directionToCenter) < 0f)
                    normal = -normal;

                planes[i] = new Plane(normal, start3D);
            }

            return planes;
        }

        private bool TryGetClosestSide(Vector3 position, out float closestDistance, out Plane side)
        {
            side = default;
            closestDistance = float.MaxValue;
            bool sideFounded = false;

            for (int i = 0; i < playAreaSides.Length; i++)
            {
                float distance = Mathf.Abs(playAreaSides[i].GetDistanceToPoint(position));
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    side = playAreaSides[i];
                    sideFounded = true;
                }
            }

            return sideFounded;
        }

        protected virtual IEnumerator WaitForHeadsetRoutine(Action onComplete)
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (!HMDDevice.isValid)
                yield return wait;
            onComplete?.Invoke();
        }

        private bool IsHeadsetInSafeZone()
        {
            var headsetPosition = transform.InverseTransformPoint(camera ? camera.position : Vector3.zero);
            return (headsetPosition.x < safeZoneWidth && headsetPosition.z < safeZoneDepth);
        }

        protected virtual bool TryGetPlayAreaRect(out Vector2[] points)
        {
#if ARVI_PROVIDER_OPENVR
            return TryGetPlayAreaRectOpenVR(out points);
#elif ARVI_PROVIDER_OPENXR
            return TryGetPlayAreaRectOpenXR(out points);
#else
            points = default;
            return false;
#endif
        }

#if ARVI_PROVIDER_OPENVR
        protected virtual bool TryGetPlayAreaRectOpenVR(out Vector2[] points)
        {
            points = default;
            var chaperone = OpenVR.Chaperone;
            if (chaperone != null)
            {
                HmdQuad_t playAreaRect = default;
                if (chaperone.GetPlayAreaRect(ref playAreaRect))
                {
                    var playAreaWidth = playAreaRect.vCorners0.v0 - playAreaRect.vCorners1.v0;
                    var playAreaDepth = playAreaRect.vCorners2.v2 - playAreaRect.vCorners1.v2;
                    bool isStandingMode = (playAreaWidth <= 1.01f) && (playAreaDepth <= 1.01f);

                    points = new Vector2[4];

                    if (isStandingMode)
                    {
                        playAreaWidth = Mathf.Max(playAreaWidth, standingModePlayAreaSize);
                        playAreaDepth = Mathf.Max(playAreaDepth, standingModePlayAreaSize);

                        points[0] = new Vector2(-playAreaWidth / 2f, playAreaDepth / 2f);
                        points[1] = new Vector2(playAreaWidth / 2f, playAreaDepth / 2f);
                        points[2] = new Vector2(playAreaWidth / 2f, -playAreaDepth / 2f);
                        points[3] = new Vector2(-playAreaWidth / 2f, -playAreaDepth / 2f);
                    }
                    else
                    {
                        points[0] = new Vector2(playAreaRect.vCorners0.v0, playAreaRect.vCorners0.v2);
                        points[1] = new Vector2(playAreaRect.vCorners1.v0, playAreaRect.vCorners1.v2);
                        points[2] = new Vector2(playAreaRect.vCorners2.v0, playAreaRect.vCorners2.v2);
                        points[3] = new Vector2(playAreaRect.vCorners3.v0, playAreaRect.vCorners3.v2);
                    }
                    return true;
                }
            }
            else
                Debug.LogWarning("VR chaperone not found. Is HMD active?");

            return false;
        }
#endif

#if ARVI_PROVIDER_OPENXR
        protected virtual bool TryGetPlayAreaRectOpenXR(out Vector2[] points)
        {
            points = default;

            var xrGeneralSettings = XRGeneralSettings.Instance;
            if (xrGeneralSettings)
            {
                var xrManagerSettings = xrGeneralSettings.Manager;
                if (xrManagerSettings)
                {
                    var xrLoader = xrManagerSettings.activeLoader;
                    if (xrLoader)
                    {
                        var inputSubsystem = xrLoader.GetLoadedSubsystem<XRInputSubsystem>();
                        if (inputSubsystem != null)
                        {
                            var boundaryPoints = new List<Vector3>();
                            if (inputSubsystem.TryGetBoundaryPoints(boundaryPoints))
                            {
                                points = new Vector2[boundaryPoints.Count];
                                for (int i = 0; i < points.Length; ++i)
                                    points[i] = new Vector2(boundaryPoints[i].x, boundaryPoints[i].z);

                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
#endif

        private void SetPlayAreaLayerRecursive(Transform transform, Transform except = null)
        {
            var newLayer = LayerMask.NameToLayer(PLAY_AREA_LAYER_NAME);
            if (newLayer < 0)
                return;

            SetLayerRecursive(transform, newLayer, except);
        }

        private void SetLayerRecursive(Transform transform, int newLayer, Transform except = null)
        {
            if (!transform || transform == except)
                return;

            transform.gameObject.layer = newLayer;
            for (int i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursive(transform.GetChild(i), newLayer, except);
            }
        }
    }
}