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
        private const int PLAY_AREA_MINOR_VERSION = 1;

        [Header("Tracking Components")]

        [Tooltip("Left controller transform")]
        public Transform leftControllerTransform;

        [Tooltip("Right controller transform")]
        public Transform rightControllerTransform;

        [Tooltip("Camera transform. Usually matches the VR camera transform")]
        public Transform cameraTransform;

        [Tooltip("Camera rig transform. Used to synchronize the position and rotation of play area")]
        public Transform cameraRigTransform;

        [Tooltip("Should automatically check tracking components transforms each frame. Set False to do it manually with corresponding methods")]
        public bool autoUpdateTrackingComponents = true;

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
        [Tooltip("An array of headset models that have their own play area. Play area will not be checked for these headsets")]
        private string[] headsetsWithOwnPlayArea;

        [SerializeField]
        [Tooltip("An array of controller models that have their own play area. Play area will not be checked for these controllers")]
        private string[] controllersWithOwnPlayArea;

        [Header("Bounds Detection Settings")]

        [SerializeField]
        [Tooltip("The distance from which the grid for the left controller will start to appear")]
        private float leftControllerDetectionDistance = 0.3f;

        [SerializeField]
        [Tooltip("The distance from which the grid for the right controller will start to appear")]
        private float rightControllerDetectionDistance = 0.3f;

        [SerializeField]
        [Tooltip("Minimum allowed play area size. If both sides are less than this value, the size of play area will be increased to this size")]
        private float minimumPlayAreaSize = 2f;

        [SerializeField]
        private float minDistanceToStartShowCell = 0.4f;

        [SerializeField]
        private float minDistanceToStartShowTextureInsideCell = 0.05f;

        [SerializeField]
        private float offsetDistance = 0.25f;

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

        [SerializeField] [Tooltip("Render play area bounds when OutOfBounds area displayed")]
        private bool renderPlayAreaWhenOutOfBounds = false;

        [Header("Out Of Bounds Fading Settings")]

        [SerializeField]
        private bool canModifyCameraCullingMask = true;

        [SerializeField]
        private bool canModifyCameraClearFlags = true;

        [SerializeField]
        private bool canModifyCameraBackgroundColor = true;

        [SerializeField]
        private bool canModifyCameraPostProcessing = true;

        [Header("Debug")]

        [Tooltip("Do not require ARVI SDK initialization. Useful for debugging in the Editor")]
#pragma warning disable CS0414
        public bool skipSDKInitialization = false;
#pragma warning restore CS0414

        [Tooltip("Always draw play area bounds. Useful for debugging")]
        public bool forceShowPlayArea = false;

        [SerializeField]
        [Tooltip("Use forced play area size. Useful for debugging when play area rect can''t be retrieved from VR provider")]
        private bool useForcedPlayAreaSize = false;

        [SerializeField]
        [Tooltip("Forced play area size if it can''t be retrieved from VR provider")]
        private Vector2 forcedPlayAreaSize = new Vector2(1f, 1f);

        [SerializeField]
        private bool verboseLog = false;

        [Header("Events")]

        [Tooltip("The event is raised when the play area is created and ready to use")]
        public UnityEvent OnPlayAreaReady;

        [Tooltip("The event is raised when the play area was changed")]
        public UnityEvent OnPlayAreaChanged;

        [Tooltip("The event is raised when a player leaves to the play area boundaries")]
        public UnityEvent OnPlayerOutOfBounds;

        [Tooltip("The event is raised when a player returns to the play area boundaries")]
        public UnityEvent OnPlayerInBounds;

        public const string PLAY_AREA_LAYER_NAME = "ARVI Play Area";

        private const string LEFT_CONTROLLER_POSITION_NAME = "_LeftControllerPosition";
        private const string RIGHT_CONTROLLER_POSITION_NAME = "_RightControllerPosition";
        private const string LEFT_CONTROLLER_DETECTION_DISTANCE_NAME = "_LeftControllerSphereRadius";
        private const string RIGHT_CONTROLLER_DETECTION_DISTANCE_NAME = "_RightControllerSphereRadius";
        private const string CLOSEST_SIDE_DIRECTION = "_ClosestSideDirection";
        private const string HEADSET_DIRECTION = "_HeadsetDirection";
        private const string CELL_TEXTURE_SIZE = "_CellTextureSize";
        private const string INSIDE_CELL_TEXTURE_ALPHA = "_InsideCellTextureAlpha";

        private const string PLAY_AREA_MATERIAL_NAME = "ARVI Play Area Material";

        private int leftControllerPositionID;
        private int rightControllerPositionID;
        private int leftControllerDetectionDistanceID;
        private int rightControllerDetectionDistanceID;
        private int closestSideDirectionID;
        private int headsetDirectionID;
        private int cellTextureSizeID;
        private int insideCellTextureAlphaID;

        private InputDevice hmdDevice;
        private InputDevice controllerDevice;

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

        private CameraSettings originalCameraSettings;

        private bool shouldCheckPlayArea;
        private bool shouldUseOutOfBoundsArea;

        private Vector3 playAreaPosition;
        private Quaternion playAreaRotation;

        private Vector3 leftControllerPosition;
        private Vector3 rightControllerPosition;

        private Vector3 cameraPosition;
        private Vector3 cameraForward;

        private class CameraSettings
        {
            public readonly int CullingMask;
            public readonly CameraClearFlags ClearFlags;
            public readonly Color BackgroundColor;
            public readonly bool RenderPostProcessing;

            public CameraSettings(Camera camera)
            {
                if (!camera)
                    return;

                CullingMask = camera.cullingMask;
                ClearFlags = camera.clearFlags;
                BackgroundColor = camera.backgroundColor;
                var cameraData = camera.GetUniversalAdditionalCameraData();
                RenderPostProcessing = cameraData.renderPostProcessing;
            }
        }

        public static PlayArea Instance { get; private set; }

        public InputDevice HMDDevice
        {
            get
            {
                if (hmdDevice.isValid)
                    return hmdDevice;
                // Try to check HMD
                hmdDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (hmdDevice.isValid)
                {
                    if (verboseLog)
                        Debug.Log(string.Format("Headset: {0}", hmdDevice.name));
                }
                return hmdDevice;
            }
        }

        public InputDevice ControllerDevice
        {
            get
            {
                if (controllerDevice.isValid)
                    return controllerDevice;
                // Try to check left controller
                var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                if (leftHand.isValid)
                {
                    controllerDevice = leftHand;
                    if (verboseLog)
                        Debug.Log(string.Format("{0} connected", leftHand.name));
                    return leftHand;
                }
                // Try to check right controller
                var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (rightHand.isValid)
                {
                    controllerDevice = rightHand;
                    if (verboseLog)
                        Debug.Log(string.Format("{0} connected", rightHand.name));
                    return rightHand;
                }

                return default(InputDevice);
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
                // Use DontDestroyOnLoad only if PlayArea component is on the root GameObject
                if (!transform.parent)
                    DontDestroyOnLoad(this);
            }
            else
            {
                Debug.LogWarning("Another instance of ARVI.PlayArea detected and destroyed");
                Destroy(this);
                return;
            }

            leftControllerPositionID = Shader.PropertyToID(LEFT_CONTROLLER_POSITION_NAME);
            rightControllerPositionID = Shader.PropertyToID(RIGHT_CONTROLLER_POSITION_NAME);
            leftControllerDetectionDistanceID = Shader.PropertyToID(LEFT_CONTROLLER_DETECTION_DISTANCE_NAME);
            rightControllerDetectionDistanceID = Shader.PropertyToID(RIGHT_CONTROLLER_DETECTION_DISTANCE_NAME);
            closestSideDirectionID = Shader.PropertyToID(CLOSEST_SIDE_DIRECTION);
            headsetDirectionID = Shader.PropertyToID(HEADSET_DIRECTION);
            cellTextureSizeID = Shader.PropertyToID(CELL_TEXTURE_SIZE);
            insideCellTextureAlphaID = Shader.PropertyToID(INSIDE_CELL_TEXTURE_ALPHA);

            var resourceMaterial = Resources.Load<Material>(PLAY_AREA_MATERIAL_NAME);
            if (resourceMaterial)
                playAreaMaterial = new Material(resourceMaterial);

            if (outOfBoundsArea)
                SetPlayAreaLayerRecursive(outOfBoundsArea.transform);

            ShowOutOfBoundsArea(false);
        }

        protected virtual IEnumerator Start()
        {
            originalCameraSettings = CreateCameraSettings();

            // Should render play area when out of bounds?
            if (renderPlayAreaWhenOutOfBounds)
                gameObject.layer = LayerMask.NameToLayer(PLAY_AREA_LAYER_NAME);

#if ARVI_PROVIDER_OPENVR || ARVI_PROVIDER_OPENXR
            if (!skipSDKInitialization)
            {
                if (verboseLog)
                    Debug.Log("Waiting for ARVI Integration initialization");
                // Wait for integration initialization
                yield return WaitForIntegrationRoutine();
            }
            else
            {
                if (verboseLog)
                    Debug.Log("Skip ARVI Integration initialization");
            }

            if (verboseLog)
                Debug.Log("Waiting for headset");
            // Wait for headset
            yield return WaitForHeadsetRoutine();
#if ARVI_PROVIDER_OPENXR
            if (verboseLog)
                Debug.Log("Waiting for controller");
            // Wait for any controller
            yield return WaitForControllerRoutine();

            var deviceForCheck = ControllerDevice;
            shouldCheckPlayArea = ShouldCheckPlayArea(deviceForCheck);
            shouldUseOutOfBoundsArea = ShouldUseOutOfBoundsArea(deviceForCheck);
#else
            var deviceForCheck = HMDDevice;
            shouldCheckPlayArea = ShouldCheckPlayArea(deviceForCheck);
            shouldUseOutOfBoundsArea = ShouldUseOutOfBoundsArea(deviceForCheck);
#endif
            // Should to check the play area?
            if (shouldCheckPlayArea)
            {
                // Try to get play area points
                if (TryGetPlayAreaRect(out playAreaLocalPoints))
                {
                    // Create play area
                    CreatePlayArea(playAreaLocalPoints, playAreaHeight);

                    playAreaInitialized = true;
                    OnPlayAreaReady?.Invoke();
                    if (verboseLog)
                        Debug.Log("Play area initialized");
                }
                else
                {
                    Debug.LogWarning("Failed to get play area rect");
                    enabled = false;
                }
#if ARVI_PROVIDER_OPENXR
                // For OpenXR subscribe to play area events
                if (!useForcedPlayAreaSize)
                {
                    if (TryGetXRInputSubsystem(out var inputSubsystem))
                    {
                        inputSubsystem.trackingOriginUpdated += HandleInputSubsystemTrackingOriginUpdated;
                    }
                }
#endif
            }
            else
            {
                if (verboseLog)
                    Debug.Log("Play area should not be checked");
                enabled = false;
            }
#else
            Debug.LogError("VR provider is not set. Select your provider in \"Provider\" field");
            enabled = false;
            yield break;
#endif
        }

#if ARVI_PROVIDER_OPENXR
        protected virtual void OnDestroy()
        {
            // For OpenXR unsubscribe from play area events
            if (!useForcedPlayAreaSize)
            {
                if (TryGetXRInputSubsystem(out var inputSubsystem))
                {
                    inputSubsystem.trackingOriginUpdated -= HandleInputSubsystemTrackingOriginUpdated;
                }
            }
        }
#endif

#if ARVI_PROVIDER_OPENVR || ARVI_PROVIDER_OPENXR
        protected virtual void CreatePlayArea(Vector2[] points, float height)
        {
            if (verboseLog)
                Debug.Log("Create Play Area object");
            // Create/Update MeshFilter
            var playAreaMesh = CreatePlayAreaMesh(points, height);
            if (!TryGetComponent(out MeshFilter meshFilter))
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = playAreaMesh;
            // Create/Update MeshRenderer
            if (!TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.material = playAreaMaterial;
            }
        }
#endif

        public void UpdatePlayArea(Vector3 position, Quaternion rotation)
        {
            playAreaPosition = position;
            playAreaRotation = rotation;

            transform.SetPositionAndRotation(playAreaPosition, playAreaRotation);
        }

        public void UpdateControllers(Vector3 leftControllerPosition, Vector3 rightControllerPosition)
        {
            this.leftControllerPosition = leftControllerPosition;
            this.rightControllerPosition = rightControllerPosition;
        }

        public void UpdateCamera(Vector3 position, Vector3 forward)
        {
            cameraPosition = position;
            cameraForward = forward;
        }

        protected virtual void Update()
        {
            if (autoUpdateTrackingComponents)
            {
                leftControllerPosition = leftControllerTransform ? leftControllerTransform.position : Vector3.zero;
                rightControllerPosition = rightControllerTransform ? rightControllerTransform.position : Vector3.zero;

                if (cameraTransform)
                {
                    cameraPosition = cameraTransform.position;
                    cameraForward = cameraTransform.forward;
                }
                else
                {
                    cameraPosition = Vector3.zero;
                    cameraForward = Vector3.zero;
                }

                if (cameraRigTransform)
                {
#if UNITY_2021_3_OR_NEWER
                    cameraRigTransform.GetPositionAndRotation(out playAreaPosition, out playAreaRotation);
#else
                    playAreaPosition = cameraRigTransform.position;
                    playAreaRotation = cameraRigTransform.rotation;
#endif
                }
                else
                {
                    playAreaPosition = Vector3.zero;
                    playAreaRotation = Quaternion.identity;
                }
                UpdatePlayArea(playAreaPosition, playAreaRotation);
            }

            if (!playAreaInitialized)
                return;

            if (forceShowPlayArea)
            {
                ForceDrawPlayAreaBounds();
            }
            else
            {
                // Update visibility of play area bounds
                if (shouldCheckPlayArea)
                    CheckPlayAreaBounds();
                // Check if player moves out of play area bounds
                if (shouldUseOutOfBoundsArea)
                    CheckPlayAreaOutOfBounds();
            }
        }
        protected virtual void ForceDrawPlayAreaBounds()
        {
            if (!playAreaMaterial)
                return;
            playAreaMaterial.SetVector(closestSideDirectionID, Vector3.zero);
            playAreaMaterial.SetFloat(cellTextureSizeID, 1f);
            playAreaMaterial.SetFloat(insideCellTextureAlphaID, 1f);
        }

        protected virtual void CheckPlayAreaBounds()
        {
            // Check if headset is inside the safe zone
            var isHeadsetInSafeZone = IsHeadsetInSafeZone();
            // Calculate controllers detection distance taking into account controllers velocity
            calculatedLeftControllerDetectionDistance = Mathf.Lerp(calculatedLeftControllerDetectionDistance, CalculateLeftControllerDetectionDistance(isHeadsetInSafeZone), 2f * Time.unscaledDeltaTime);
            calculatedRightControllerDetectionDistance = Mathf.Lerp(calculatedRightControllerDetectionDistance, CalculateRightControllerDetectionDistance(isHeadsetInSafeZone), 2f * Time.unscaledDeltaTime);
            // Update play area material properties
            if (!playAreaMaterial)
                return;
            // Update controllers detection distance
            playAreaMaterial.SetFloat(leftControllerDetectionDistanceID, calculatedLeftControllerDetectionDistance);
            playAreaMaterial.SetFloat(rightControllerDetectionDistanceID, calculatedRightControllerDetectionDistance);
            // Update left controller position
            playAreaMaterial.SetVector(leftControllerPositionID, leftControllerPosition);
            // Update right controller position
            playAreaMaterial.SetVector(rightControllerPositionID, rightControllerPosition);
            // Update headset position and direction
            playAreaMaterial.SetVector(headsetDirectionID, transform.InverseTransformDirection(cameraForward));
            // Find nearest side
            float distanceToSide;
            Plane side;
            if (!TryGetNearestSide(transform.InverseTransformPoint(cameraPosition), out distanceToSide, out side))
                return;
            distanceToSide -= offsetDistance;
            playAreaMaterial.SetVector(closestSideDirectionID, side.normal);
            playAreaMaterial.SetFloat(cellTextureSizeID, 1f - Mathf.Clamp01(distanceToSide / (minDistanceToStartShowCell - offsetDistance)));
            playAreaMaterial.SetFloat(insideCellTextureAlphaID, Mathf.Clamp01(distanceToSide / (minDistanceToStartShowTextureInsideCell - offsetDistance)));
        }

        protected virtual void CheckPlayAreaOutOfBounds()
        {
            // Check if the player inside the play area
            var headsetPosition = transform.InverseTransformPoint(cameraPosition);
            if (IsPointInPlayArea(headsetPosition))
            {
                // Check if the player near the center of play area
                if (IsHeadsetNearPlayAreaCenter(headsetPosition))
                {
                    // Deactivate OutOfBounds area
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
            if (!cameraTransform)
                return null;

#if UNITY_2019_2_OR_NEWER
            return cameraTransform.TryGetComponent<Camera>(out var cameraComponent) ? new CameraSettings(cameraComponent) : null;
#else
            var cameraComponent = cameraTransform.GetComponent<Camera>();
            return cameraComponent ? new CameraSettings(cameraComponent) : null;
#endif
        }

        protected void SetPlayAreaCulling(bool culled)
        {
            if (originalCameraSettings == null)
                return;
            if (!cameraTransform)
                return;
#if UNITY_2019_2_OR_NEWER
            if (!cameraTransform.TryGetComponent<Camera>(out var cameraComponent))
                return;
#else
            var cameraComponent = cameraTransform.GetComponent<Camera>();
            if (!cameraComponent)
                return;
#endif
            // Culling Mask
            if (canModifyCameraCullingMask)
                cameraComponent.cullingMask = culled ? LayerMask.GetMask(PLAY_AREA_LAYER_NAME) : originalCameraSettings.CullingMask;
            // Clear Flags
            if (canModifyCameraClearFlags)
                cameraComponent.clearFlags = culled ? CameraClearFlags.Color : originalCameraSettings.ClearFlags;
            // Background Color
            if (canModifyCameraBackgroundColor)
                cameraComponent.backgroundColor = culled ? Color.black : originalCameraSettings.BackgroundColor;
            // Post Processing
            if (canModifyCameraPostProcessing)
            {
                var cameraData = cameraComponent.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = culled ? false : originalCameraSettings.RenderPostProcessing;
            }
        }

        private bool IsHeadsetNearPlayAreaCenter(Vector3 headsetPosition)
        {
            return Mathf.Abs(headsetPosition.x) < playAreaCenterThreshold && Mathf.Abs(headsetPosition.z) < playAreaCenterThreshold;
        }

        private static bool PolygonContainsPoint(Vector2[] polygonPoints, Vector2 point)
        {
            var j = polygonPoints.Length - 1;
            var inside = false;

            for (var i = 0; i < polygonPoints.Length; j = i++)
            {
                var pi = polygonPoints[i];
                var pj = polygonPoints[j];
                if (((pi.y <= point.y && point.y < pj.y) || (pj.y <= point.y && point.y < pi.y)) &&  point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
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
            {
                if (OnPlayerOutOfBounds != null)
                    OnPlayerOutOfBounds.Invoke();
            }
            else
            {
                if (OnPlayerInBounds != null)
                    OnPlayerInBounds.Invoke();
            }
        }

        protected bool IsPointInPlayArea(Vector3 point)
        {
            return PolygonContainsPoint(playAreaLocalPoints, new Vector2(point.x, point.z));
        }

        protected virtual void UpdateLeftControllerVelocity()
        {
            leftControllerVelocity = (leftControllerPosition - previousLeftControllerPosition).magnitude / Time.unscaledDeltaTime;
            previousLeftControllerPosition = leftControllerPosition;
        }

        protected virtual void UpdateRightControllerVelocity()
        {
            rightControllerVelocity = (rightControllerPosition - previousRightControllerPosition).magnitude / Time.unscaledDeltaTime;
            previousRightControllerPosition = rightControllerPosition;
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

        protected virtual bool ShouldCheckPlayArea(InputDevice device)
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
                                // Check whether device has its own play area
                                return !HasOwnPlayArea(device);
                        }
                    }
            }
        }

        protected virtual bool ShouldUseOutOfBoundsArea(InputDevice device)
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
                                // Check whether device has its own play area
                                return !HasOwnPlayArea(device);
                        }
                    }
            }
        }

        protected virtual bool HasOwnPlayArea(InputDevice device)
        {
#if ARVI_PROVIDER_OPENVR
            var headsetName = device.name.ToLowerInvariant();
            foreach (var headsetWithOwnPlayArea in headsetsWithOwnPlayArea)
                if (headsetName.Contains(headsetWithOwnPlayArea.ToLowerInvariant()))
                    return true;
#endif

#if ARVI_PROVIDER_OPENXR
            var controllerName = device.name.ToLowerInvariant();
            foreach (var controllerWithOwnPlayArea in controllersWithOwnPlayArea)
                if (controllerName.Contains(controllerWithOwnPlayArea.ToLowerInvariant()))
                    return true;
#endif
            return false;
        }

        private static float GetTotalLength(Vector2[] playAreaPoints)
        {
            var totalLength = 0f;

            for (var i = 0; i < playAreaPoints.Length; i++)
            {
                var startPoint = playAreaPoints[i];
                var endPoint = playAreaPoints[(i + 1) % playAreaPoints.Length];

                totalLength += Vector2.Distance(startPoint, endPoint);
            }

            return totalLength;
        }

        protected virtual Mesh CreatePlayAreaMesh(Vector2[] playAreaPoints, float height)
        {
            var pointsCount = playAreaPoints.Length;
            if (pointsCount < 2)
                return null;

            var totalLength = GetTotalLength(playAreaPoints);
            var accumulatedLength = 0f;

            var verticesList = new List<Vector3>();
            var trianglesList = new List<int>();
            var uvsList = new List<Vector2>();
            var uvs2List = new List<Vector2>();

            for (var i = 0; i < pointsCount; i++)
            {
                var startPoint = playAreaPoints[i];
                var endPoint = playAreaPoints[(i + 1) % pointsCount];

                var vertices = new Vector3[4];
                vertices[0] = new Vector3(startPoint.x, 0, startPoint.y);
                vertices[1] = new Vector3(startPoint.x, height, startPoint.y);
                vertices[2] = new Vector3(endPoint.x, height, endPoint.y);
                vertices[3] = new Vector3(endPoint.x, 0, endPoint.y);

                var triangles = new int[6];
                var baseIndex = verticesList.Count;
                triangles[0] = baseIndex;
                triangles[1] = baseIndex + 1;
                triangles[2] = baseIndex + 2;
                triangles[3] = baseIndex + 2;
                triangles[4] = baseIndex + 3;
                triangles[5] = baseIndex;

                var startUV = accumulatedLength / totalLength;
                var endUV = (accumulatedLength + Vector2.Distance(startPoint, endPoint)) / totalLength;
                var uvs = new Vector2[4];
                uvs[0] = new Vector2(startUV, 0);
                uvs[1] = new Vector2(startUV, height / totalLength);
                uvs[2] = new Vector2(endUV, height / totalLength);
                uvs[3] = new Vector2(endUV, 0);

                var uvs2 = new Vector2[4];
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

            var mesh = new Mesh();
            mesh.SetVertices(verticesList);
            mesh.SetTriangles(trianglesList, 0);
            mesh.SetUVs(0, uvsList);
            mesh.SetUVs(1, uvs2List);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2 CalculateCenter(Vector2[] points)
        {
            var center = Vector2.zero;
            foreach (var point in points)
                center += point;

            center /= points.Length;
            return center;
        }

        private static Plane[] CreatePlanes(Vector2[] points, Vector3 center)
        {
            var pointsCount = points.Length;
            var planes = new Plane[pointsCount];

            for (var i = 0; i < pointsCount; i++)
            {
                var startPoint = points[i];
                var endPoint = points[(i + 1) % pointsCount];

                var start3D = new Vector3(startPoint.x, 0, startPoint.y);
                var end3D = new Vector3(endPoint.x, 0, endPoint.y);

                var dir = (end3D - start3D).normalized;
                var normal = Vector3.Cross(dir, Vector3.up);

                var middlePoint = (start3D + end3D) * 0.5f;
                var directionToCenter = center - middlePoint;
                if (Vector3.Dot(normal, directionToCenter) < 0f)
                    normal = -normal;

                planes[i] = new Plane(normal, start3D);
            }

            return planes;
        }

        private bool TryGetNearestSide(Vector3 position, out float nearestDistance, out Plane side)
        {
            side = default(Plane);
            nearestDistance = float.MaxValue;
            var sideFounded = false;

            for (var i = 0; i < playAreaSides.Length; i++)
            {
                var distance = Mathf.Abs(playAreaSides[i].GetDistanceToPoint(position));
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    side = playAreaSides[i];
                    sideFounded = true;
                }
            }

            return sideFounded;
        }

        protected virtual IEnumerator WaitForHeadsetRoutine()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (!HMDDevice.isValid)
                yield return wait;
        }

#if ARVI_PROVIDER_OPENXR
        protected virtual IEnumerator WaitForControllerRoutine()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (!ControllerDevice.isValid)
                yield return wait;
        }
#endif

        protected virtual IEnumerator WaitForIntegrationRoutine()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (!Integration.Initialized)
                yield return wait;
        }

        private bool IsHeadsetInSafeZone()
        {
            var headsetPosition = transform.InverseTransformPoint(cameraPosition);
            return headsetPosition.x < safeZoneWidth && headsetPosition.z < safeZoneDepth;
        }

        protected virtual bool TryGetPlayAreaRect(out Vector2[] points)
        {
            var result = false;

            if (verboseLog)
                Debug.Log("Try to get play area points");

            if (useForcedPlayAreaSize)
            {
                points = GetPlayAreaRectWithSize(forcedPlayAreaSize);
                if (verboseLog)
                    Debug.Log(string.Format("Forced play area size used. Size is {0}x{1}", forcedPlayAreaSize.x, forcedPlayAreaSize.y));
                result = true;
            }
            else
            {
#if ARVI_PROVIDER_OPENVR
                result = TryGetPlayAreaRectOpenVR(out points);
#elif ARVI_PROVIDER_OPENXR
                result = TryGetPlayAreaRectOpenXR(out points);
#else
                points = default(Vector2[]);
                result = false;
#endif
            }
            /*
             * Points should be in this order:
             *          |Y
             *          |
             *     1    |    2
             *----------|----------X
             *          |
             *     0    |    3
             *          |
             */

            if (result && points.Length > 2)
            {
                // Check for minimum play area size
                var playAreaSide1 = Mathf.Abs(points[2].x - points[1].x);
                var playAreaSide2 = Mathf.Abs(points[1].y - points[0].y);

                if (verboseLog)
                    Debug.Log(string.Format("Play area size is {0}x{1}", playAreaSide1, playAreaSide2));

                if (playAreaSide1 < minimumPlayAreaSize && playAreaSide2 < minimumPlayAreaSize)
                {
                    if (verboseLog)
                        Debug.LogWarning(string.Format("Play area size is too small and will be increased to {0}x{0}", minimumPlayAreaSize));
                    points = GetPlayAreaRectWithSize(new Vector2(minimumPlayAreaSize, minimumPlayAreaSize));
                }
            }

            return result;
        }

#if ARVI_PROVIDER_OPENVR
        protected virtual bool TryGetPlayAreaRectOpenVR(out Vector2[] points)
        {
            points = default;
            if (verboseLog)
                Debug.Log("Try to get play area points with OpenVR");
            var chaperone = OpenVR.Chaperone;
            if (chaperone != null)
            {
                HmdQuad_t playAreaRect = default;
                if (chaperone.GetPlayAreaRect(ref playAreaRect))
                {
                    points = new Vector2[]
                    {
                        new Vector2(playAreaRect.vCorners1.v0, playAreaRect.vCorners1.v2),
                        new Vector2(playAreaRect.vCorners2.v0, playAreaRect.vCorners2.v2),
                        new Vector2(playAreaRect.vCorners3.v0, playAreaRect.vCorners3.v2),
                        new Vector2(playAreaRect.vCorners0.v0, playAreaRect.vCorners0.v2),
                    };

                    if (verboseLog)
                        Debug.Log("Play area points retrieved");
                    return true;
                }
                if (verboseLog)
                    Debug.LogWarning("Failed to get play area points with GetPlayAreaRect");
            }
            else
                Debug.LogWarning("VR chaperone not found. Is HMD active?");

            return false;
        }
#endif

#if ARVI_PROVIDER_OPENXR
        private static bool TryGetXRInputSubsystem(out XRInputSubsystem inputSubsystem)
        {
            inputSubsystem = default(XRInputSubsystem);

            var xrGeneralSettings = XRGeneralSettings.Instance;
            if (!xrGeneralSettings)
                return false;
            var xrManagerSettings = xrGeneralSettings.Manager;
            if (!xrManagerSettings)
                return false;
            var xrLoader = xrManagerSettings.activeLoader;
            if (!xrLoader)
                return false;
            inputSubsystem = xrLoader.GetLoadedSubsystem<XRInputSubsystem>();
            return inputSubsystem != null;
        }

        protected virtual bool TryGetPlayAreaRectOpenXR(out Vector2[] points)
        {
            points = default;
            if (verboseLog)
                Debug.Log("Try to get play area points with OpenXR");
            if (!TryGetXRInputSubsystem(out var inputSubsystem))
                return false;
            var pointsList = new List<Vector3>();
            if (inputSubsystem.TryGetBoundaryPoints(pointsList))
            {
                points = new Vector2[pointsList.Count];
                for (var i = 0; i < points.Length; ++i)
                    points[i] = new Vector2(pointsList[i].x, pointsList[i].z);
                if (verboseLog)
                    Debug.Log("Play area points retrieved");
                return true;
            }
            if (verboseLog)
                Debug.LogWarning("Failed to get play area points with TryGetBoundaryPoints");

            return false;
        }

        protected virtual void HandleInputSubsystemTrackingOriginUpdated(XRInputSubsystem inputSubsystem)
        {
            if (verboseLog)
                Debug.Log(string .Format("Play area tracking origin mode updated to {0}", inputSubsystem.GetTrackingOriginMode()));
        }
#endif
        protected virtual Vector2[] GetPlayAreaRectWithSize(Vector2 size)
        {
            return new Vector2[]
            {
                new Vector2(-size.x / 2, -size.y / 2),
                new Vector2(-size.x / 2, size.y / 2),
                new Vector2(size.x / 2, size.y / 2),
                new Vector2(size.x / 2, -size.y / 2)
            };
        }

        private static void SetPlayAreaLayerRecursive(Transform transform, Transform except = null)
        {
            var newLayer = LayerMask.NameToLayer(PLAY_AREA_LAYER_NAME);
            if (newLayer < 0)
                return;

            SetLayerRecursive(transform, newLayer, except);
        }

        private static void SetLayerRecursive(Transform transform, int newLayer, Transform except = null)
        {
            if (!transform || transform == except)
                return;

            transform.gameObject.layer = newLayer;
            for (var i = 0; i < transform.childCount; i++)
                SetLayerRecursive(transform.GetChild(i), newLayer, except);
        }
    }
}