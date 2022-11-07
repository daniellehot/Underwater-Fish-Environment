using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

//https://www.youtube.com/watch?v=33hoa_OpjHs

public class fixingBoundingBoxes : MonoBehaviour
{
    [SerializeField] GameObject fishPrefab;

    //int[] control_vector = new int[]{0, 1, 1};
    struct conditionsControl{
        public int background;
        public int fog;
        public int distractors;

        public conditionsControl(int c1, int c2, int c3){
            this.background = c1;
            this.fog = c2;
            this.distractors = c3;
        }
    }
    conditionsControl control;
    int controlIdx = 0;
    List<conditionsControl> controlList = new List<conditionsControl>();

    struct boundingBox{
        public int top;
        public int bottom;
        public int left;
        public int right;
        public int height;
        public int width;
        public bool save;
    }

    Vector2 numFishMinMax = new Vector2(10, 15);
    int numberOfFish;
    int numberOfSwarms = 1;
    Camera mainCam;

    int FPS = 15;
    float deltaTime;
    int fishId; 

    GameObject simArea;
    Renderer simAreaRenderer;
    Bounds simAreaBounds;
    Vector3 simAreaSize = new Vector3(150, 60, 180);
    //Vector3 simAreaSize = Vector3.one*75;

    float animationSpeed = 1f;
    
    int numberOfRandomFish = 0;

    //default boids values
    float boidSpeed = 2.5f; //10f
    float boidSteeringSpeed = 100f; //100
    float boidNoClumpingArea = 10f;
    float boidLocalArea = 100f;
    //default behaviour weights
    float K = 1f;
    float S = 1f;
    float M = 1f;
    float X = 1f;
    //boid class
    public class boidController
    {
        public GameObject parentGo;
        public GameObject go;
        public SkinnedMeshRenderer renderer;

        //identification data
        public int id;
        public int swarmIndex;

        //random movement
        public bool randomBehaviour = false;
        
        //public int elapsedFrames;
        //public int goalFrames;
        //public int framesToMaxSpeed;

        public float elapsedTime;
        public float goalTime;
        public float timeToMaxSpeed;

        public Vector3 randomDirection;
        public float randomWeight;
        public float randomSpeed;
        public float randomSteeringSpeed;

        //original values are used to revert back into non-random behaviour
        public float originalSpeed;
        public float originalSteeringSpeed;

        //default behaviour values, not used for anything yet
        public float noClumpingArea;
        public float localArea;
        public float speed;
        public float steeringSpeed;
    }
    List<boidController> boidsList = new List<boidController>();

    string videoDir = "Assets/videos";
    string[] videoFiles;
    string rootDir;
    string datasetDir = "brackishMOTSynth";
    string imageFolder;
    string gtFolder;
    string gtFile;
    int sequence_number = 0;
    int sequence_image;
    int sequence_goal =5;
    int sequence_length = 50;

    string normalizedFogIntensity;
    string numberOfDistractors;
    string spawnedFish;
    string backgroundSequence;

    int img_height = 544;
    int img_width = 960;
    RenderTexture screenRenderTexture;
    Texture2D screenshotTex;
    
    Mesh bakedMesh;
    VideoPlayer vp;

    //bool moveOtherWay;
    Vector3 fishCenter;

    GameObject targetGo;
    float moveTarget;

    public Vector3 GetRandomPositionInCamera(Camera cam)
    {
        Vector3 world_pos = cam.ViewportToWorldPoint( new Vector3(
            UnityEngine.Random.Range(0, 1.0f), 
            UnityEngine.Random.Range(0, 1.0f), 
            UnityEngine.Random.Range(20f, 60f)));
        return world_pos;
    }

    float updateSpeed(boidController b, float rndSpeed, float initSpeed)
    {
        float deltaSpeed = rndSpeed - initSpeed;
        float speedIncrement;
        float newSpeed;
    
        if (b.elapsedTime < b.timeToMaxSpeed)
        {
            speedIncrement = b.elapsedTime/b.timeToMaxSpeed*deltaSpeed;
        }  
        else 
        {
            speedIncrement = b.elapsedTime/b.goalTime*deltaSpeed*-1f;
        }

        newSpeed = initSpeed + speedIncrement;
        
        return newSpeed;
    }


    void simulateMovement(List<boidController> boids, float time)
    {
        fishCenter = Vector3.zero;
        int maxNumOfRandomFish = (int) Random.Range(1f, numberOfFish/2f);

        for (int i = 0; i < boids.Count; i++)
        {
            boidController b_i = boids[i];
            //print("isVisible " + b_i.renderer.isVisible);
            Vector3 steering = Vector3.zero;

            Vector3 separationDirection = Vector3.zero;
            int separationCount = 0;
            Vector3 alignmentDirection = Vector3.zero;
            int alignmentCount = 0;
            Vector3 cohesionDirection = Vector3.zero;
            int cohesionCount = 0;
            Vector3 leaderDirection = Vector3.zero;
            boidController leaderBoid = boids[0];
            float leaderAngle = 180f;
            Vector3 randomDirection = Vector3.zero;
            float randomWeight = 0;

            if (!b_i.randomBehaviour && Random.value > .99f && numberOfRandomFish < maxNumOfRandomFish)
            {
                numberOfRandomFish += 1;
                b_i.randomBehaviour = true;

                b_i.elapsedTime = 0f;
                b_i.goalTime = Random.Range(1f, 2f);
                b_i.timeToMaxSpeed = Random.Range(0.1f, 0.5f);

                b_i.randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                b_i.randomDirection = b_i.randomDirection.normalized;

                b_i.randomWeight = Random.Range(1f, 10f);
                b_i.randomSteeringSpeed = Random.Range(2f, 5f)*b_i.steeringSpeed;
                //b_i.randomSpeed = b_i.randomSteeringSpeed/10f;
                b_i.originalSpeed = b_i.speed;
                b_i.originalSteeringSpeed = b_i.steeringSpeed;
            }

            if (b_i.randomBehaviour)
            {
                //if (b_i.elapsedFrames == b_i.goalFrames)
                if (b_i.elapsedTime > b_i.goalTime)
                {
                    numberOfRandomFish -= 1;
                    b_i.randomBehaviour = false;
                    b_i.speed = b_i.originalSpeed;
                    b_i.steeringSpeed = b_i.originalSteeringSpeed;
                }
                else
                {
                    randomDirection = b_i.randomDirection;
                    randomWeight = b_i.randomWeight;
                    //b_i.speed = updateSpeedTime(b_i, b_i.randomSpeed, b_i.originalSpeed);
                    b_i.steeringSpeed = updateSpeed(b_i, b_i.randomSteeringSpeed, b_i.originalSteeringSpeed);
                    b_i.elapsedTime += time;
                }
            } 

            if (!b_i.randomBehaviour)
            {
                for (int j = 0; j < boids.Count; j++)
                {
                    boidController b_j = boids[j];
                    if (b_i == b_j) continue;

                    float distance = Vector3.Distance(b_j.go.transform.position, b_i.go.transform.position);
                    if (distance < boidNoClumpingArea)
                    {
                        separationDirection += b_j.go.transform.position - b_i.go.transform.position;
                        separationCount++;
                    }

                    if (distance < boidLocalArea && b_j.swarmIndex == b_i.swarmIndex)
                    {
                        alignmentDirection += b_j.go.transform.forward;
                        alignmentCount++;

                        cohesionDirection += b_j.go.transform.position - b_i.go.transform.position;
                        cohesionCount++;

                        //identify leader
                        float angle = Vector3.Angle(b_j.go.transform.position - b_i.go.transform.position, b_i.go.transform.forward);
                        if (angle < leaderAngle && angle < 90f)
                        {
                            leaderBoid = b_j;
                            leaderAngle = angle;
                        }
                    }
                }
            
                if (separationCount > 0) separationDirection /= separationCount;
                separationDirection = -separationDirection;
                separationDirection = separationDirection.normalized;

                if (alignmentCount > 0) alignmentDirection /= alignmentCount;
                alignmentDirection = alignmentDirection.normalized;

                if (cohesionCount > 0) cohesionDirection /= cohesionCount;
                cohesionDirection -= b_i.go.transform.position;
                cohesionDirection = cohesionDirection.normalized;

                if (leaderBoid != null) 
                {
                    leaderDirection = leaderBoid.go.transform.position - b_i.go.transform.position;
                    leaderDirection = leaderDirection.normalized;
                }
            }

            Vector3 cameraDirection = mainCam.transform.position - b_i.go.transform.position;
            cameraDirection = -cameraDirection.normalized;

            if (randomDirection != Vector3.zero && simAreaBounds.Contains(b_i.go.transform.position))
            {
                float cameraDistance = Vector3.Distance(mainCam.transform.position, b_i.go.transform.position);
                steering += cameraDirection*cameraDistance;
                steering += randomDirection*randomWeight;
                
                Debug.DrawRay(b_i.go.transform.position, steering, Color.red);
            } 
            else
            {
                //Vector3 boundsDirection = Vector3.zero;
                //boundsDirection = simAreaBounds.center - b_i.go.transform.position;
                //boundsDirection = boundsDirection.normalized;
                //steering += boundsDirection;
                //steering += cameraDirection;
                steering += separationDirection*S;
                steering += alignmentDirection*M;
                steering += cohesionDirection*K;
                //steering += leaderDirection*X;
                Debug.DrawRay(b_i.go.transform.position, steering, Color.green);
            }

            Vector3 targetDirection = simAreaBounds.center - b_i.go.transform.position;
            float distanceToCenter = Vector3.Distance(simAreaBounds.center, b_i.go.transform.position);
            print("DistanceToCenter " + distanceToCenter.ToString());
            //b_i.go.transform.position = Vector3.MoveTowards(b_i.go.transform.position, simAreaBounds.center, b_i.speed*time);
            //b_i.go.transform.up = simAreaBounds.center - b_i.go.transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            b_i.go.transform.rotation = targetRotation;
            
            //b_i.go.transform.rotation = Quaternion.RotateTowards(b_i.go.transform.rotation, targetRotation, 7500 * time);
            //b_i.go.transform.position += b_i.go.transform.TransformDirection(new Vector3(b_i.speed, 0, 0))* time;


            /*Vector3 targetDirection = simAreaBounds.center - b_i.go.transform.position;
            Vector3 newDirection = Vector3.RotateTowards(b_i.go.transform.forward, targetDirection, b_i.steeringSpeed*time*10, 0.0f);
            Debug.DrawRay(b_i.go.transform.position, newDirection, Color.blue);
            b_i.go.transform.rotation = Quaternion.LookRotation(newDirection);
            b_i.go.transform.position += b_i.go.transform.TransformDirection(new Vector3(b_i.speed, 0, 0))* time;*/

            //b_i.go.SetActive(true);
            /*if (!b_i.renderer.isVisible)
            {
                Vector3 targetDirection = simAreaBounds.center - b_i.go.transform.position;
                Vector3 newDirection = Vector3.RotateTowards(b_i.go.transform.forward, targetDirection, b_i.steeringSpeed*time, 0.0f);
                Debug.DrawRay(b_i.go.transform.position, newDirection, Color.blue);
                b_i.go.transform.rotation = Quaternion.LookRotation(newDirection);
            }
            else 
            {
                //b_i.go.SetActive(true);
                b_i.go.transform.rotation = Quaternion.RotateTowards(
                    b_i.go.transform.rotation, 
                    Quaternion.LookRotation(steering), 
                    b_i.steeringSpeed * time);
            
                b_i.go.transform.position += b_i.go.transform.TransformDirection(new Vector3(b_i.speed, 0, 0))* time;
            }*/
        }
    }

    Vector3 getBoundsVector(boidController b)
    {
        Vector3 diffMax = simAreaBounds.max - b.parentGo.transform.position;
        Vector3 diffMin = simAreaBounds.min - b.parentGo.transform.position;
        Vector3 boundsDirection = Vector3.zero;

        if (Mathf.Abs(diffMax.x) > Mathf.Abs(diffMin.x)) boundsDirection.x = -diffMin.x;
        if (Mathf.Abs(diffMax.x) < Mathf.Abs(diffMin.x)) boundsDirection.x = -diffMax.x;

        if (Mathf.Abs(diffMax.y) > Mathf.Abs(diffMin.y)) boundsDirection.y = -diffMin.y;
        if (Mathf.Abs(diffMax.y) < Mathf.Abs(diffMin.y)) boundsDirection.y = -diffMax.y;

        if (Mathf.Abs(diffMax.z) > Mathf.Abs(diffMin.z)) boundsDirection.z = -diffMin.z;
        if (Mathf.Abs(diffMax.z) < Mathf.Abs(diffMin.z)) boundsDirection.z = -diffMax.z;

        boundsDirection = boundsDirection.normalized;

        return boundsDirection;
    }

    void simulateMovementV2(List<boidController> boids, float time)
    {
        int maxNumOfRandomFish = (int) Random.Range(1f, numberOfFish/2f);

        moveTarget += time;
        if (moveTarget > 10f)
        {
            targetGo.transform.position = GetRandomPositionInCamera(mainCam);
            moveTarget = 0f;
        }
            

        for (int i = 0; i < boids.Count; i++)
        {
            boidController b_i = boids[i];

            float distanceToTarget = Vector3.Distance(targetGo.transform.position, b_i.parentGo.transform.position);
            if (distanceToTarget < 5f) targetGo.transform.position = GetRandomPositionInCamera(mainCam);

            Vector3 steering = Vector3.zero;

            Vector3 separationDirection = Vector3.zero;
            int separationCount = 0;
            Vector3 alignmentDirection = Vector3.zero;
            int alignmentCount = 0;
            Vector3 cohesionDirection = Vector3.zero;
            int cohesionCount = 0;
            Vector3 leaderDirection = Vector3.zero;
            boidController leaderBoid = boids[0];
            float leaderAngle = 180f;
            Vector3 randomDirection = Vector3.zero;
            float randomWeight = 0;

            if (!b_i.randomBehaviour && Random.value > 1.1f && numberOfRandomFish < maxNumOfRandomFish)
            {
                numberOfRandomFish += 1;
                b_i.randomBehaviour = true;

                b_i.elapsedTime = 0f;
                b_i.goalTime = Random.Range(1f, 2f);
                b_i.timeToMaxSpeed = Random.Range(0.1f, 0.5f);

                b_i.randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                b_i.randomDirection = b_i.randomDirection.normalized;

                b_i.randomWeight = Random.Range(1f, 10f);
                b_i.randomSteeringSpeed = Random.Range(2f, 5f)*b_i.steeringSpeed;
                //b_i.randomSpeed = b_i.randomSteeringSpeed/10f;
                b_i.originalSpeed = b_i.speed;
                b_i.originalSteeringSpeed = b_i.steeringSpeed;
            }

            if (b_i.randomBehaviour)
            {
                //if (b_i.elapsedFrames == b_i.goalFrames)
                if (b_i.elapsedTime > b_i.goalTime)
                {
                    numberOfRandomFish -= 1;
                    b_i.randomBehaviour = false;
                    b_i.speed = b_i.originalSpeed;
                    b_i.steeringSpeed = b_i.originalSteeringSpeed;
                }
                else
                {
                    randomDirection = b_i.randomDirection;
                    randomWeight = b_i.randomWeight;
                    //b_i.speed = updateSpeedTime(b_i, b_i.randomSpeed, b_i.originalSpeed);
                    b_i.steeringSpeed = updateSpeed(b_i, b_i.randomSteeringSpeed, b_i.originalSteeringSpeed);
                    b_i.elapsedTime += time;
                }
            } 

            if (!b_i.randomBehaviour)
            {
                for (int j = 0; j < boids.Count; j++)
                {
                    boidController b_j = boids[j];
                    if (b_i == b_j) continue;

                    float distance = Vector3.Distance(b_j.parentGo.transform.position, b_i.parentGo.transform.position);
                    if (distance < boidNoClumpingArea)
                    {   
                        //S = 10f;
                        separationDirection += b_j.parentGo.transform.position - b_i.parentGo.transform.position;
                        separationCount++;
                        
                    } 
                    else if (distance < boidLocalArea && b_j.swarmIndex == b_i.swarmIndex)
                    {
                        //S = 1f;
                        separationDirection += b_j.parentGo.transform.position - b_i.parentGo.transform.position;
                        separationCount++;

                        alignmentDirection += b_j.parentGo.transform.forward;
                        alignmentCount++;

                        cohesionDirection += b_j.parentGo.transform.position - b_i.parentGo.transform.position;
                        cohesionCount++;

                        //identify leader
                        float angle = Vector3.Angle(b_j.parentGo.transform.position - b_i.parentGo.transform.position, b_i.parentGo.transform.forward);
                        if (angle < leaderAngle && angle < 90f)
                        {
                            leaderBoid = b_j;
                            leaderAngle = angle;
                        }
                    }
                }                        

                if (separationCount > 0) separationDirection /= separationCount;
                separationDirection = -separationDirection;
                separationDirection = separationDirection.normalized;

                if (alignmentCount > 0) alignmentDirection /= alignmentCount;
                alignmentDirection = alignmentDirection.normalized;

                if (cohesionCount > 0) cohesionDirection /= cohesionCount;
                //cohesionDirection -= b_i.go.transform.position;
                cohesionDirection = cohesionDirection.normalized;

                if (leaderBoid != null) 
                {
                    leaderDirection = leaderBoid.go.transform.position - b_i.go.transform.position;
                    leaderDirection = leaderDirection.normalized;
                }
            }


            if (randomDirection != Vector3.zero)
            {
                Vector3 cameraDirection = mainCam.transform.position - b_i.parentGo.transform.position;
                cameraDirection = -cameraDirection.normalized;
                //float cameraDistance = Vector3.Distance(mainCam.transform.position, b_i.parentGo.transform.position);
                //steering += cameraDirection*cameraDistance;
                steering += cameraDirection;
                steering += randomDirection*randomWeight;
                
                Debug.DrawRay(b_i.go.transform.position, steering, Color.red);
            } 
            else
            {
                Vector3 targetDirection = targetGo.transform.position - b_i.parentGo.transform.position;
                targetDirection = targetDirection.normalized;
                steering += targetDirection;
                steering += separationDirection*S;
                steering += alignmentDirection*M;
                steering += cohesionDirection*K;
                steering += leaderDirection*X;

                Debug.DrawRay(b_i.go.transform.position, steering, Color.green);
            }
            
            b_i.parentGo.transform.rotation = Quaternion.RotateTowards(
                b_i.parentGo.transform.rotation, 
                Quaternion.LookRotation(steering), 
                b_i.steeringSpeed * time);
            //b_i.parentGo.transform.RotateAround(simAreaBounds.center, Vector3.up, b_i.speed*time);

            b_i.parentGo.transform.Translate(Vector3.forward * b_i.speed * Time.deltaTime);
        }
    }

    void instantiateFish(int swarmIdx)
    { 
        fishId = 1;

        numberOfFish = (int) Random.Range(numFishMinMax.x, numFishMinMax.y);
        spawnedFish = numberOfFish.ToString();

        for (int i = 0; i < numberOfFish; i++)
        {
            boidController b = new boidController();
            b.go = Instantiate(fishPrefab);
            b.go.transform.position = GetRandomPositionInCamera(mainCam);
            //b.go.transform.rotation = Quaternion.Euler(0, Random.Range(-180f, 180f), 0);
            //b.go.transform.localScale = Vector3.one * Random.Range(0.5f, 1f);
            b.go.GetComponent<Animator>().SetFloat("SpeedFish", animationSpeed);
            b.go.name = "fish_" + fishId.ToString();//Name the prefab clone and then access the fishName script and give the same name to it so this way the cild containing the mesh will have the proper ID
            b.go.GetComponentInChildren<fishName>().fishN = "fish_" + fishId.ToString();
            
            b.parentGo = new GameObject();
            b.parentGo.name = b.go.name + "_parent";
            b.parentGo.transform.position = b.go.transform.position;
            b.parentGo.transform.eulerAngles = new Vector3(0, 90f, 0);
            b.go.transform.parent  = b.parentGo.transform;

            //Visual randomisation
            SkinnedMeshRenderer renderer = b.go.GetComponentInChildren<SkinnedMeshRenderer>();
            renderer.forceMatrixRecalculationPerRender = true;
            b.renderer = renderer;
            float rnd_color_seed = Random.Range(75.0f, 225.0f);
            Color rnd_albedo = new Color(
                rnd_color_seed/255, 
                rnd_color_seed/255, 
                rnd_color_seed/255,
                Random.Range(0.0f, 1.0f));
            renderer.material.color = rnd_albedo;
            renderer.material.SetFloat("_Metalic", Random.Range(0.1f, 0.5f));
            renderer.material.SetFloat("_Metalic/_Glossiness", Random.Range(0.1f, 0.5f));
   
            b.id = fishId;
            fishId++;
            b.swarmIndex = swarmIdx;
            b.speed = boidSpeed;
            b.steeringSpeed = boidSteeringSpeed;
            b.localArea = boidLocalArea;
            b.noClumpingArea = boidNoClumpingArea;
            
            boidsList.Add(b);
        }
    }

    void addNewSequence()
    {   
        sequence_number += 1;
        if (sequence_number != sequence_goal + 1){
            //sequence_image = 0;
            sequence_image = 1;

            string seq_name;
            if (sequence_number < 10)
            {
                seq_name = datasetDir + "-0" + sequence_number.ToString();
            }
            else
            {
                seq_name = datasetDir + "-" + sequence_number.ToString();
            }

            string new_sequence = rootDir + "/" + seq_name;


            

            if (System.IO.Directory.Exists(new_sequence))
            {
                System.IO.Directory.Delete(new_sequence, true);
                System.IO.Directory.CreateDirectory(new_sequence);
            } else {
                System.IO.Directory.CreateDirectory(new_sequence);
            }

            imageFolder = new_sequence + "/img1";
            gtFolder = new_sequence + "/gt";
            System.IO.Directory.CreateDirectory(imageFolder);
            System.IO.Directory.CreateDirectory(gtFolder);
            gtFile = gtFolder + "/gt.txt";
            string iniFile = new_sequence + "/seqinfo.ini";
            
            if (control.background == 0) backgroundSequence = "plain";
            if (control.distractors == 0) numberOfDistractors = "0";
            if (control.fog == 0) normalizedFogIntensity = "0";

            string seqInfo = "[Sequence]\n" + 
                "name=" + seq_name.ToString() +"\n" +
                "imDir=img1\n" +
                "frameRate=" + FPS.ToString() + "\n" +
                "seqLength=" + sequence_length.ToString() + "\n" +
                "imWidth=" + img_width.ToString() + "\n" +
                "imHeight=" + img_height.ToString() + "\n" +
                "imExt=.jpg\n" +
                "fogIntensity=" + normalizedFogIntensity + "\n" +
                "numberOfDistractors=" + numberOfDistractors + "\n" +
                "spawnedFish=" + spawnedFish + "\n" +
                "backgroundSequence=" + backgroundSequence + "\n";
            
            using (StreamWriter writer = new StreamWriter(iniFile, true))
            {
                writer.Write(seqInfo);
            }
        }
    }

    void setupFolderStructure()
    {
        string controlString = "";
        //rootDir = "/home/vap/synthData/" + datasetDir;
        rootDir = "synthData/" + datasetDir;

        if (control.background != 0 || control.fog != 0 || control.distractors != 0) controlString += "_";

        if (control.background == 1){
            controlString += "B";
        } 

        if (control.fog == 1){
            controlString += "F";
        }

        if (control.distractors == 1){
            controlString += "D";
        }
        rootDir = rootDir + controlString + "/train/";


        //Create a parent folder, remove the old one if it exists
        if (System.IO.Directory.Exists(rootDir))
        {
            System.IO.Directory.Delete(rootDir, true);
            System.IO.Directory.CreateDirectory(rootDir);
        } else {
             System.IO.Directory.CreateDirectory(rootDir);
        }

    }

    void generateControlList()
    {
        conditionsControl controlVariant;

        //000
        controlVariant.background = 0; 
        controlVariant.fog = 0;
        controlVariant.distractors = 0;
        controlList.Add(controlVariant);
        
        /*//001
        controlVariant.background = 0; 
        controlVariant.fog = 0;
        controlVariant.distractors = 1;
        controlList.Add(controlVariant);
        
        //010
        controlVariant.background = 0; 
        controlVariant.fog = 1;
        controlVariant.distractors = 0;
        controlList.Add(controlVariant);
        
        //011
        controlVariant.background = 0; 
        controlVariant.fog = 1;
        controlVariant.distractors = 1;
        controlList.Add(controlVariant);

        //100
        controlVariant.background = 1; 
        controlVariant.fog = 0;
        controlVariant.distractors = 0;
        controlList.Add(controlVariant);
        
        //101
        controlVariant.background = 1; 
        controlVariant.fog = 0;
        controlVariant.distractors = 1;
        controlList.Add(controlVariant);
        
        //110
        controlVariant.background = 1; 
        controlVariant.fog = 1;
        controlVariant.distractors = 0;
        controlList.Add(controlVariant);

        //111
        controlVariant.background = 1; 
        controlVariant.fog = 1;
        controlVariant.distractors = 1;
        controlList.Add(controlVariant);*/
    }

    void CleanUp()
    {
        foreach (boidController b in boidsList)
        {
            Destroy(b.go);
        }

        boidsList.Clear();
        numberOfRandomFish = 0;
    }

    void getNewBoidParameters()
    {
        K = Random.Range(0.75f, 1.25f);
        S = Random.Range(0.75f, 1.25f);
        M = Random.Range(0.75f, 1.25f);
        X = Random.Range(0.75f, 1.25f);
        
        //boidSpeed = 10f*Random.Range(0.5f, 1.5f); //10f
        //boidSteeringSpeed = 100f*Random.Range(0.5f, 1.5f); //100
        boidNoClumpingArea = Random.Range(7.5f, 12.5f);
        boidLocalArea = Random.Range(15f, 25f);
    }
    
    void rotateTowardsCenter(List<boidController> boids, float time)
    {
        for (int i = 0; i < boids.Count; i++)
        {
            boidController b_i = boids[i];
            Vector3 targetDirection = targetGo.transform.position - b_i.go.transform.position;
            Debug.DrawRay(b_i.go.transform.position, targetDirection, Color.red);
            //b_i.parentGo.transform.rotation = Quaternion.LookRotation(targetDirection.normalized);
            //b_i.parentGo.transform.rotation = Quaternion.FromToRotation(Vector3.right, targetDirection.normalized);

            if (!simAreaBounds.Contains(b_i.go.transform.position))
            {  
                b_i.parentGo.transform.rotation = Quaternion.RotateTowards(
                    b_i.parentGo.transform.rotation, 
                    Quaternion.LookRotation(targetDirection.normalized), 
                    b_i.steeringSpeed * time);
            }
            

            b_i.parentGo.transform.position += b_i.go.transform.TransformDirection(new Vector3(b_i.speed, 0, 0))* time;
            //targetDirection = Vector3.Cross(targetDirection, new Vector3(0, 0, 1)).normalized;
            //targetDirection = Vector3.Cross(targetDirection, b_i.go.transform.right).normalized;
            //targetDirection = Vector3.Cross(targetDirection, Vector3.right).normalized;
            //b_i.parentGo.transform.rotation = Quaternion.LookRotation(targetDirection,  Vector3.forward);
            
            
            //float hyp = Vector3.Distance(targetGo.transform.position, b_i.go.transform.position);
            //float deltaZ = targetGo.transform.position.z - b_i.go.transform.position.z;
            //float yaw = - Mathf.Asin(deltaZ/hyp) * Mathf.Rad2Deg;
            //print("yaw " + yaw.ToString());

            //float distanceToCenter = Vector3.Distance(simAreaBounds.center, b_i.go.transform.position);
            //print("DistanceToCenter " + distanceToCenter.ToString());
            //b_i.go.transform.position = Vector3.MoveTowards(b_i.go.transform.position, simAreaBounds.center, b_i.speed*time);
            //b_i.go.transform.up = simAreaBounds.center - b_i.go.transform.position;
            //Quaternion targetRotation = Quaternion.LookRotation(targetDirection, new Vector3(1, 0, 0));
            //b_i.go.transform.rotation = targetRotation;
            //b_i.go.transform.LookAt(targetGo.transform);
            //Quaternion toRotation = Quaternion.FromToRotation(b_i.go.transform.right, targetDirection);
            //b_i.go.transform.rotation = Quaternion.Lerp(b_i.go.transform.rotation, toRotation, b_i.steeringSpeed * time);

            //Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            //Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.up);
            //b_i.go.transform.rotation = Quaternion.FromToRotation(b_i.go.transform.right, targetDirection);

            
        }
    }   

    void Awake()
    {
        generateControlList();
        control = controlList[controlIdx];
        setupFolderStructure();

        mainCam = GameObject.Find("Fish Camera").GetComponent<Camera>();
        mainCam.backgroundColor = new Color(
            Random.Range(171f, 191f)/255,  
            Random.Range(192f, 212f)/255, 
            Random.Range(137f, 157f)/255,
            Random.Range(151f, 171f)/255);

        simArea = GameObject.Find("simArea");
        simArea.transform.position = new Vector3(0, 0, simAreaSize.z/2f);
        simArea.transform.localScale = simAreaSize;
        UnityEngine.Physics.SyncTransforms();
        simAreaRenderer = simArea.GetComponent<Renderer>();
        simAreaBounds = simArea.GetComponent<Collider>().bounds;
        simArea.SetActive(false);

        videoFiles = System.IO.Directory.GetFiles(videoDir,"*.mp4");
        vp = GameObject.Find("Video player").GetComponent<VideoPlayer>();

        bakedMesh = new Mesh();
        screenshotTex = new Texture2D(img_width, img_height, TextureFormat.RGB24, false);

        //Set delta time used for animating
        deltaTime = (float) 1/FPS;

        //getNewBoidParameters();

        targetGo = GameObject.Find("testTarget");
        targetGo.transform.position = simAreaBounds.center;
    }

    // Start is called before the first frame update
    void Start()
    {
        //numberOfSwarms = (int) Random.Range(1, 5);
        //numberOfSwarms = 5;

        for (int i = 0; i < numberOfSwarms; i++)
        {
            instantiateFish(i);
        }
        addNewSequence();
        
    }

    // Update is called once per frame
    void Update()
    {
        deltaTime = Time.deltaTime;
        
        /*//print("control background " + control.background.ToString());
        //print("control fog " + control.fog.ToString());
        //print("control distractors " + control.distractors.ToString());

        if (sequence_image == sequence_length)
        {      
            CleanUp();
            for (int i = 0; i < numberOfSwarms; i++)
            {
                instantiateFish(i);
            }
            addNewSequence();

            //sequence_image += 1;
        } 
        else if(vp.isPlaying || control.background == 0)
        {
            sequence_image += 1;

            simulateMovement(boidsList, deltaTime);
        }*/
        
        simulateMovementV2(boidsList, deltaTime);
        //followFlock();
        
        //rotateTowardsCenter(boidsList, deltaTime);
        
    }

    /*void LateUpdate()
    {
        //print("controlIdx " + controlIdx.ToString());
        //print("controlList.Count " + controlList.Count.ToString());
            
        if (sequence_number == sequence_goal+1)
        {  
           
            controlIdx++;
            if (controlIdx == controlList.Count)
            //if (controlList[controlIdx] == controlList.Last())
            {
                Debug.Log("All sequences were generated");
                UnityEditor.EditorApplication.isPlaying = false;
            }
        }
        else
        {
            foreach (boidController b in boidsList)
            {
                boundingBox bb = GetBoundingBoxInCamera(b.go, mainCam);
                SaveAnnotation(bb, b.id);
            }
            SaveCameraView();

            Debug.Log("Sequence Number " + sequence_number.ToString() 
            + " Sequence Image " + sequence_image.ToString() 
            + "/" + sequence_length.ToString());
        }
    }*/

    bool isWithinTheView(GameObject go)
    {
        if (go.transform.position.z < -10f){
            return false;
        }

        Vector3 headPosition = go.transform.Find("Armature/Bone").transform.position;
        Vector3 tailPosition = go.transform.Find("Armature/Bone/Bone.001/Bone.002/Bone.003/Bone.004").transform.position;
        Vector3 viewPosHead = mainCam.WorldToViewportPoint(headPosition);
        Vector3 viewPosTail = mainCam.WorldToViewportPoint(tailPosition);
        float minViewPos = 0.0f;
        float maxViewPos = 1.0f;

        if (viewPosHead.x <= minViewPos && viewPosTail.x <= minViewPos ||  
            viewPosHead.x >= maxViewPos && viewPosTail.x >= maxViewPos ){
            return false;
        }
        
        if (viewPosHead.y <= minViewPos && viewPosTail.y <= minViewPos ||  
            viewPosHead.y >= maxViewPos && viewPosTail.y >= maxViewPos ){
            return false;
        }

        return true;
    }

    Vector3[] GetMeshVertices(GameObject go)
    {
        SkinnedMeshRenderer skinMeshRend = go.GetComponentInChildren<SkinnedMeshRenderer>();
        skinMeshRend.BakeMesh(bakedMesh, true);
        Vector3[] verts_local = bakedMesh.vertices;
        Transform rendererOwner = skinMeshRend.transform;
        //Transform rendererOwner = go.transform;

        for (int j = 0; j < verts_local.Length; j++)
        {
            verts_local[j] = rendererOwner.localToWorldMatrix.MultiplyPoint3x4(verts_local[j]);
        }

        return verts_local;
    }

    boundingBox GetBoundingBoxInCamera(GameObject go, Camera cam)
    {
        //Physics.SyncTransforms();
        bool withinTheView = isWithinTheView(go);
        //bool withinTheView = true;
        //Debug.Log("Within the view " + withinTheView.ToString());
        boundingBox bb = new boundingBox();
        bb.save = false;

        if (withinTheView)
        { 
            //boundingBox bb = new boundingBox();
            bb.save = true;
            Vector3[] verts = GetMeshVertices(go);
            Vector2[] pixelCoordinates = new Vector2[verts.Length];

            /*for (int i = 0; i < verts.Length; i+=1000)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = verts[i];
                sphere.transform.localScale = Vector3.one * 0.1f;
                sphere.GetComponent<Renderer>().material.SetColor("_Color", Color.red);
                //sphere.transform.parent = Fish.transform;
            }*/

            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = cam.WorldToScreenPoint(verts[i]);
                pixelCoordinates[i].x = verts[i].x;
                pixelCoordinates[i].y = verts[i].y;
            }

            Vector2 min = pixelCoordinates[0];
            Vector2 max = pixelCoordinates[0];
            foreach (Vector2 pixel in pixelCoordinates)
            {   
                if (pixel.x >= 0 && pixel.x <= img_width && pixel.y >= 0 && pixel.y <= img_height)
                {
                    min = Vector2.Min(min, pixel);
                    max = Vector2.Max(max, pixel);
                }
            }
            
            float minHeight = min.y;
            float maxHeight = max.y;
            min.y = img_height - maxHeight;
            max.y = img_height - minHeight;

            bb.left = (int) min.x;
            //if (bb.left < 0) bb.left = 0;
            
            bb.right = (int) max.x;
            //if (bb.right > img_width) bb.right = img_width;

            bb.top = (int) min.y;
            //if (bb.top < 0) bb.top = 0;
            
            bb.bottom = (int) max.y;
            //if (bb.bottom  > img_height) bb.bottom = img_height;
            
            bb.height = (int) bb.bottom - bb.top;
            bb.width = (int) bb.right - bb.left;
        }
        return bb;
    }

    void SaveAnnotation(boundingBox bbox, int go_id)
    {   
        if (bbox.save)
        {   
            string frame = sequence_image.ToString();
            string id = go_id.ToString();
            string left = bbox.left.ToString();
            string top = bbox.top.ToString();
            string width = bbox.width.ToString();
            string height = bbox.height.ToString();


            string confidence = "1";
            string class_id = "5";
            string visibility = "1";

            string annotation = frame + ","
                + id + ","
                + left + ","
                + top + ","
                + width + ","
                + height + ","
                + confidence + ","
                + class_id + ","
                + visibility + ","
                + "\n";
            
            //string line = maskObjects[i].name.Split('_')[0] + " " + bboxs[i].x.ToString() + " " + bboxs[i].y.ToString() + " " + bboxs[i].z.ToString() + " " + bboxs[i].w.ToString() + "\n";
            using (StreamWriter writer = new StreamWriter(gtFile, true))
            {
                writer.Write(annotation);
            }
        }
        //Debug.Log(annotation);
    }

    void SaveCameraView()
    {
        string filename;
        if (sequence_image > 99999){
            filename = imageFolder + "/" + sequence_image.ToString() + ".jpg";
        } else if (sequence_image > 9999) {
            filename = imageFolder + "/0" + sequence_image.ToString() + ".jpg";
        } else if (sequence_image > 999) {
            filename = imageFolder + "/00" + sequence_image.ToString() + ".jpg";
        } else if (sequence_image > 99) {
            filename = imageFolder + "/000" + sequence_image.ToString() + ".jpg";
        } else if (sequence_image > 9) {
            filename = imageFolder + "/0000" + sequence_image.ToString() + ".jpg";
        } else {
            filename = imageFolder + "/00000" + sequence_image.ToString() + ".jpg";
        }
        //string filename = dataDir + "/" + Time.frameCount.ToString() + ".png";
        
        screenRenderTexture = RenderTexture.GetTemporary(img_width, img_height, 24);
        mainCam.targetTexture = screenRenderTexture;
        mainCam.Render();
        RenderTexture.active = screenRenderTexture;

        //Texture2D screenshotTex = new Texture2D(img_width, img_height, TextureFormat.RGB24, false);
        screenshotTex.ReadPixels(new Rect(0, 0, img_width, img_height), 0, 0);

        RenderTexture.active = null; // JC: added to avoid errors
        RenderTexture.ReleaseTemporary(screenRenderTexture);
        screenRenderTexture = null;
        Destroy(screenRenderTexture);

        //byte[] byteArray = screenshotTex.EncodeToPNG();
        byte[] byteArray = screenshotTex.EncodeToJPG();
        System.IO.File.WriteAllBytes(filename, byteArray);
        //Destroy(screenshotTex);
    }
}
