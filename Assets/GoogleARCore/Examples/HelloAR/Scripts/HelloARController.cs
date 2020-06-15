//-----------------------------------------------------------------------
// <copyright file="HelloARController.cs" company="Google">
//
// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.HelloAR
{
    using System.Collections.Generic;
    using System.Collections;
    using GoogleARCore;
    using GoogleARCore.Examples.Common;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.Networking;
    using System.IO;
    using System;
    using UnityEngine.UI;
    using UnityEngine.SceneManagement;



#if UNITY_EDITOR
    // Set up touch input propagation while using Instant Preview in the editor.
    using Input = InstantPreviewInput;
#endif
    //[RequireComponent(typeof(MeshFilter))]
    //[RequireComponent(typeof(MeshRenderer))]
    /// <summary>
    /// Controls the HelloAR example.
    /// </summary>
    public class HelloARController : MonoBehaviour
    {
        /// <summary>
        /// The first-person camera being used to render the passthrough camera image (i.e. AR
        /// background).
        /// </summary>
        public Camera FirstPersonCamera;

        /// <summary>
        /// A prefab to place when a raycast from a user touch hits a vertical plane.
        /// </summary>
        public GameObject GameObjectVerticalPlanePrefab;

        /// <summary>
        /// A prefab to place when a raycast from a user touch hits a horizontal plane.
        /// </summary>
        public GameObject GameObjectHorizontalPlanePrefab;

        /// <summary>
        /// A prefab to place when a raycast from a user touch hits a feature point.
        /// </summary>
        public GameObject GameObjectPointPrefab;

        // Shadow to place under the cones
        public GameObject ShadowPrefab;

        // Material for the walls/fence
        public Material material;

        /// <summary>
        /// The rotation in degrees need to apply to prefab when it is placed.
        /// </summary>
        private const float k_PrefabRotation = 180.0f;

        /// <summary>
        /// True if the app is in the process of quitting due to an ARCore connection error,
        /// otherwise false.
        /// </summary>
        private bool m_IsQuitting = false;

        /// <summary>
        /// Tracks number of cones; once four have been placed, their positions will be uploaded
        /// to a server
        /// </summary>
        private int numCones = 0;

        /// <summary>
        /// True if cone positions have been uploaded
        /// </summary>
        private bool conesUploaded = false;
        private bool conesDownloaded = false;
        private bool wallsMade = false;

        // holds the game objects representing cones
        private GameObject[] coneObjects = new GameObject[4];

        // holds the convex hull of the cone objects
        private GameObject[] hull = new GameObject[8];

        // check what scene is active (feedback or none)
        private string sceneName;

        /// <summary>
        /// The Unity Awake() method.
        /// </summary>
        public void Awake()
        {
            // Enable ARCore to target 60fps camera capture frame rate on supported devices.
            // Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
            Application.targetFrameRate = 60;

            Scene currentScene = SceneManager.GetActiveScene();
            sceneName = currentScene.name;

            if (sceneName == "PhysicalFeedback")
            {
                StartCoroutine(Download());
            }
        }

        /// <summary>
        /// The Unity Update() method.
        /// </summary>
        public void Update()
        {
            _UpdateApplicationLifecycle();

            if (sceneName == "PhysicalFeedback")
            {
                 if (conesDownloaded && !wallsMade)
                {
                    ConvexHull();
                    CreateCube();
                }
                return;
            }

            // If the player has not touched the screen, we are done with this update.
            Touch touch;
            if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
            {
                return;
            }

            // Should not handle input if the player is pointing on UI.
            if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                return;
            }

            // Raycast against the location the player touched to search for planes.
            TrackableHit hit;
            TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                TrackableHitFlags.FeaturePointWithSurfaceNormal;

            if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit) && numCones < 4)
            {
                // Use hit pose and camera pose to check if hittest is from the
                // back of the plane, if it is, no need to create the anchor.
                if ((hit.Trackable is DetectedPlane) &&
                    Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
                        hit.Pose.rotation * Vector3.up) < 0)
                {
                    Debug.Log("Hit at back of the current DetectedPlane");
                }
                else
                {
                    // Choose the prefab based on the Trackable that got hit.
                    GameObject prefab;
                    if (hit.Trackable is FeaturePoint)
                    {
                        prefab = GameObjectPointPrefab;
                    }
                    else if (hit.Trackable is DetectedPlane)
                    {
                        DetectedPlane detectedPlane = hit.Trackable as DetectedPlane;
                        if (detectedPlane.PlaneType == DetectedPlaneType.Vertical)
                        {
                            prefab = GameObjectVerticalPlanePrefab;
                        }
                        else
                        {
                            prefab = GameObjectHorizontalPlanePrefab;
                        }
                    }
                    else
                    {
                        prefab = GameObjectHorizontalPlanePrefab;
                    }

                    // Instantiate prefab at the hit pose.
                    var gameObject = Instantiate(prefab, hit.Pose.position, hit.Pose.rotation);
                    var shadowObject = Instantiate(ShadowPrefab, new Vector3(hit.Pose.position.x, hit.Pose.position.y - 0.12f, hit.Pose.position.z), hit.Pose.rotation);

                    // Compensate for the hitPose rotation facing away from the raycast (i.e.
                    // camera).
                    //gameObject.transform.Rotate(0, k_PrefabRotation, 0, Space.Self);

                    // Create an anchor to allow ARCore to track the hitpoint as understanding of
                    // the physical world evolves.
                    var anchor = hit.Trackable.CreateAnchor(hit.Pose);

                    // Make game object a child of the anchor.
                    gameObject.transform.parent = anchor.transform;
                    shadowObject.transform.parent = gameObject.transform;
                    numCones += 1;

                    coneObjects[numCones - 1] = gameObject;

                }
            }

            if (numCones == 4 && !conesUploaded)
            {
                StartCoroutine(Upload());
            }
        }

        IEnumerator Upload()
        {
            string text = "";
            for (int i = 0; i < 4; i++)
            {
                text += (coneObjects[i].transform.position.x).ToString() + " " + (coneObjects[i].transform.position.y).ToString() + " "
                     + (coneObjects[i].transform.position.z).ToString() + "\n";
            }
            byte[] data = System.Text.Encoding.UTF8.GetBytes(text);

            UnityWebRequest www = UnityWebRequest.Put("http://cs.rhodes.edu/~hudspethm/zone.yaml", data);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
            }
            else
            {
                Debug.Log("Upload complete!");
                conesUploaded = true;
                if (sceneName == "Feedback")
                {
                    ConvexHull();
                    CreateCube();
                }

            }
        }

        IEnumerator Download()
        {
            
            UnityWebRequest www = UnityWebRequest.Get("http://cs.rhodes.edu/~hudspethm/virtualFeedbackZone.yaml");
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
            }
            else
            {   
                // Show results as text
                Debug.Log(www.downloadHandler.text);
                var text = www.downloadHandler.text;
                string[] textPositions = text.Split('\n');
                textPositions = new List<string>(textPositions).GetRange(0, 4).ToArray();

                int n = 0;
                Debug.Log("String length: " + textPositions.Length);
                foreach (string position in textPositions)
                {
                    string[] xyz = position.Split(' ');
                    float x = float.Parse(xyz[0]);
                    float y = float.Parse(xyz[1]) - 1.5f;  // subtract 1.5 to account for height difference between robot and a person
                    float z = float.Parse(xyz[2]);
                    Vector3 conePos = new Vector3(x, y, z);

                    var gameObject = Instantiate(GameObjectVerticalPlanePrefab, conePos, Quaternion.identity);
                    
                    coneObjects[n] = gameObject;
                    n += 1;
                    

                }
                
                conesDownloaded = true;
                
            }
        }


        /*
         Based on: https://en.wikibooks.org/wiki/Algorithm_Implementation/Geometry/Convex_hull/Monotone_chain#C
        */
        private void ConvexHull()
        {

            int n = 4, k = 0;
            //if (n <= 3) return;

            // sort cones by x value, then z if x values are equal
            // https://www.geeksforgeeks.org/insertion-sort/
            for (int i = 1; i < n; ++i)
            {

                int j = i - 1;
                GameObject key = coneObjects[i];
                while (j >= 0 && (coneObjects[j].transform.position.x > key.transform.position.x ||
                        (coneObjects[j].transform.position.x == key.transform.position.x &&
                        coneObjects[j].transform.position.z > key.transform.position.z)))
                {
                    coneObjects[j + 1] = coneObjects[j];
                    j -= 1;
                }

                coneObjects[j + 1] = key;
            }
            

            // build lower hull
            for (int i = 0; i < n; ++i)
            {
                while (k >= 2 && cross(hull[k - 2], hull[k - 1], coneObjects[i]) <= 0) k--;
                hull[k++] = coneObjects[i];
            }
           
            // build upper hull
            for (int i = n-1, t = k+1; i > 0; --i)
            {
                while (k >= t && cross(hull[k - 2], hull[k - 1], coneObjects[i-1]) <= 0) k--;
                hull[k++] = coneObjects[i-1];
            }
            
        }

        private float cross(GameObject o, GameObject a, GameObject b)
        {
            return ((b.transform.position.z - o.transform.position.z) * (a.transform.position.x - o.transform.position.x) -
                      (b.transform.position.x - o.transform.position.x) * (a.transform.position.z - o.transform.position.z));
        }

        private void CreateCube()
        {
            float y = 0.12f;
            Vector3[] vertices = {
            hull[0].transform.position,                                                              // 0
            hull[1].transform.position,                                                              // 1
            new Vector3 (hull[1].transform.position.x, y, hull[1].transform.position.z),      // 2
            new Vector3 (hull[0].transform.position.x, y, hull[0].transform.position.z),      // 3
            new Vector3 (hull[3].transform.position.x, y, hull[3].transform.position.z),      // 4
            new Vector3 (hull[2].transform.position.x, y, hull[2].transform.position.z),      // 5
            hull[2].transform.position,                                                              // 6
            hull[3].transform.position,                                                              // 7

            hull[0].transform.position,                                                              // 8 (0)
            hull[1].transform.position,                                                              // 9 (1)
            new Vector3 (hull[1].transform.position.x, y, hull[1].transform.position.z),      // 10 (2)
            new Vector3 (hull[0].transform.position.x, y, hull[0].transform.position.z),      // 11 (3)
            new Vector3 (hull[3].transform.position.x, y, hull[3].transform.position.z),      // 12 (4)
            new Vector3 (hull[2].transform.position.x, y, hull[2].transform.position.z),      // 13 (5)
            hull[2].transform.position,                                                              // 14 (6)
            hull[3].transform.position                                                               // 15 (7)
        };
            
            int[] triangles = {
            0, 2, 1, //face front
			0, 3, 2,
            //2, 3, 4, //face top
			//2, 4, 5,
            1, 2, 5, //face right
			1, 5, 6,
            0, 7, 4, //face left
			0, 4, 3,
            5, 4, 7, //face back
			5, 7, 6,
            //0, 6, 7, //face bottom
			//0, 1, 6
        };

            Vector2[] uvs = {
            new Vector2(0, 0),      // 0
            new Vector2(1, 0),      // 1
            new Vector2(1, 1),      // 2
            new Vector2(0, 1),      // 3
            new Vector2(1, 1),      // 4
            new Vector2(0, 1),      // 5
            new Vector2(0, 0),      // 6
            new Vector2(1, 0),      // 7
            new Vector2(1, 0),      // 8 (0)
            new Vector2(0, 0),      // 9 (1)
            new Vector2(0, 1),      // 10 (2)
            new Vector2(1, 1),      // 11 (3)
            new Vector2(0, 1),      // 12 (4)
            new Vector2(1, 1),      // 13 (5)
            new Vector2(1, 0),      // 14 (6)
            new Vector2(0, 0),      // 15 (7)
        };
            
            GameObject walls = new GameObject();
            MeshRenderer meshrenderer = walls.AddComponent<MeshRenderer>();
            MeshFilter meshfilter = walls.AddComponent<MeshFilter>();
            meshfilter.mesh.vertices = vertices;
            meshfilter.mesh.triangles = triangles;
            meshfilter.mesh.uv = uvs;

            meshrenderer.materials = new Material[] { material };
              
            foreach (GameObject cone in coneObjects)
            {
                cone.SetActive(false);
            }
            /*
            Debug.Log("hull[]:");
            foreach (GameObject cone in hull)
            {
                cone.transform.position = new Vector3(cone.transform.position.x, cone.transform.position.y, 100.0f);
                Debug.Log(cone.transform.position);
                cone.SetActive(false);
            }
            */
            wallsMade = true;

        }


        /// <summary>
        /// Check and update the application lifecycle.
        /// </summary>
        private void _UpdateApplicationLifecycle()
        {
            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (m_IsQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to
            // appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError())
            {
                _ShowAndroidToastMessage(
                    "ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        /// <summary>
        /// Actually quit the application.
        /// </summary>
        private void _DoQuit()
        {
            Application.Quit();
        }

        /// <summary>
        /// Show an Android toast message.
        /// </summary>
        /// <param name="message">Message string to show in the toast.</param>
        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject =
                        toastClass.CallStatic<AndroidJavaObject>(
                            "makeText", unityActivity, message, 0);
                    toastObject.Call("show");
                }));
            }
        }
    }
}
