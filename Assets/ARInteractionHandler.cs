using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARInteractionHandler : MonoBehaviour
{
    [Header("Prefab to Place")]
    public GameObject placementPrefab; // Assign a prefab in the Inspector

    private ARRaycastManager raycastManager;
    private ARCameraManager arCameraManager;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Start()
    {
        // Find required AR components
        raycastManager = FindObjectOfType<ARRaycastManager>();
        arCameraManager = FindObjectOfType<ARCameraManager>();

        // Debugging to ensure AR managers are initialized
        if (raycastManager == null) Debug.LogError("ARRaycastManager is missing.");
        if (arCameraManager == null) Debug.LogError("ARCameraManager is missing.");
    }

    void Update()
    {
        // Check for touch input
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                // Perform raycast
                if (raycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
                {
                    Pose hitPose = hits[0].pose;

                    // Place object at the hit location
                    Instantiate(placementPrefab, hitPose.position, hitPose.rotation);

                    // Start capturing an image 10 frames later
                    StartCoroutine(CaptureImageAfterFrames(10));
                }
            }
        }
    }

    IEnumerator CaptureImageAfterFrames(int frameDelay)
    {
        // Wait for the specified number of frames
        for (int i = 0; i < frameDelay; i++)
        {
            yield return null;
        }

        // Try to acquire the latest CPU image
        if (arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
        {
            // Process the CPU image and save it
            StartCoroutine(ProcessAndSaveImage(cpuImage));
        }
        else
        {
            Debug.LogWarning("Failed to acquire latest CPU image.");
        }
    }

    IEnumerator ProcessAndSaveImage(XRCpuImage cpuImage)
    {
        // Convert the image to a texture
        XRCpuImage.ConversionParams conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
            outputDimensions = new Vector2Int(cpuImage.width, cpuImage.height),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.None
        };

        // Create a texture to store the converted image
        Texture2D texture = new Texture2D(cpuImage.width, cpuImage.height, TextureFormat.RGBA32, false);

        // Asynchronously convert the image
        using (XRCpuImage.AsyncConversion conversion = cpuImage.ConvertAsync(conversionParams))
        {
            while (!conversion.status.IsDone())
            {
                yield return null;
            }

            if (conversion.status == XRCpuImage.AsyncConversionStatus.Ready)
            {
                texture.LoadRawTextureData(conversion.GetData<byte>());
                texture.Apply();

                // Display the captured image (e.g., by applying it to a UI RawImage or material)
                Debug.Log("Image captured successfully.");
                // Example: Apply the texture to a GameObject
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.GetComponent<Renderer>().material.mainTexture = texture;

                // Optionally save the image as a PNG
                byte[] pngData = texture.EncodeToPNG();
                System.IO.File.WriteAllBytes(Application.persistentDataPath + "/CapturedImage.png", pngData);
                Debug.Log("Image saved to: " + Application.persistentDataPath + "/CapturedImage.png");
            }
            else
            {
                Debug.LogError("Image conversion failed: " + conversion.status);
            }
        }

        // Dispose of the CPU image
        cpuImage.Dispose();
    }
}
