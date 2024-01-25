using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FlockBehaviour : MonoBehaviour
{
    List<Obstacle> mObstacles = new List<Obstacle>();

    [SerializeField]
    GameObject[] Obstacles;

    [SerializeField]
    BoxCollider2D Bounds;

    public float TickDuration = 1.0f;
    public float TickDurationSeparationEnemy = 0.1f;
    public float TickDurationRandom = 1.0f;

    public int BoidIncr = 100;
    public bool useFlocking = false;
    public int BatchSize = 100;

    private Vector3 zeroVector = new Vector3(0, 0, 0); // Vector references as vector.zero can cause overhead
    private Vector3 mZeroVector;

    public List<Flock> flocks = new List<Flock>();

    private int interval = 3; // interval for running Update functions

    void Reset()
    {
        flocks = new List<Flock>()
    {
      new Flock()
    };
    }

    void Start()
    {
        mZeroVector = zeroVector; // we reference the zero vector in start so we can call it later
        if (Obstacles.Length != 0)
        {
            InitializeObstacles(); // only calls the functions if there are obstacles
            StartCoroutine(Coroutine_AvoidObstacles());
        }

        foreach (Flock flock in flocks)
        {
            CreateFlock(flock);
        }

        StartCoroutine(Coroutine_Flocking());

        StartCoroutine(Coroutine_Random());
        StartCoroutine(Coroutine_SeparationWithEnemies());
        StartCoroutine(Coroutine_Random_Motion_Obstacles());

        StartCoroutine(InputHandler()); // calls for the input handler in start so we dont have to call it constantly in Update()
    }

    void InitializeObstacles()
    {
        // Randomize obstacles placement.
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);
            Obstacles[i].transform.position = new Vector3(x, y, 0.0f);
            Obstacle obs = Obstacles[i].AddComponent<Obstacle>();
            Autonomous autono = Obstacles[i].AddComponent<Autonomous>();
            autono.MaxSpeed = 1.0f;
            obs.mCollider = Obstacles[i].GetComponent<CircleCollider2D>();
            mObstacles.Add(obs);
        }
    }

    void CreateFlock(Flock flock)
    {
        for (int i = 0; i < flock.numBoids; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flock);
        }
    }

    void Update()
    {
        if (Time.frameCount % interval == 0) // only calls the functions every 3 frames
        {
            Rule_CrossBorder();
            Rule_CrossBorder_Obstacles();
        }
    }

    IEnumerator InputHandler()
    {
        while (true)
        {
            HandleInputs(); // allows us to call the function
            yield return null;
        }
    }

    void HandleInputs()
    {
        if (EventSystem.current.IsPointerOverGameObject() ||
           enabled == false)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            AddBoids(BoidIncr);
        }
    }

    void AddBoids(int count)
    {
        for (int i = 0; i < count; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flocks[0]);
        }
        flocks[0].numBoids += count;
    }

    void AddBoid(float x, float y, Flock flock)
    {
        GameObject obj = Instantiate(flock.PrefabBoid);
        obj.name = "Boid_" + flock.name + "_" + flock.mAutonomous.Count;
        obj.transform.position = new Vector3(x, y, 0.0f);
        Autonomous boid = obj.GetComponent<Autonomous>();
        flock.mAutonomous.Add(boid);
        boid.MaxSpeed = flock.maxSpeed;
        boid.RotationSpeed = flock.maxRotationSpeed;
    }

    static float Distance(Autonomous a1, Autonomous a2)
    {
        return (a1.transform.position - a2.transform.position).magnitude;
    }

    void Execute(Flock flock, int i)
    {
        Vector3 flockDir = mZeroVector;
        Vector3 separationDir = mZeroVector;
        Vector3 cohesionDir = mZeroVector; // reference the zero vector

        float speed = 0.0f;
        float separationSpeed = 0.0f;
        float visibility = flock.visibility;
        float separationDistance = flock.separationDistance;
        float weightSeparation = flock.weightSeparation; // cache variables that are fixed so we dont have to keep on calling them later on

        int count = 0;
        int separationCount = 0;
        Vector3 steerPos = mZeroVector;

        Autonomous curr = flock.mAutonomous[i];
        Vector3 currentPos = curr.transform.position; // caches more values we call later

        int numberOfBoids = flock.numBoids;
        for (int j = 0; j < numberOfBoids; ++j)
        {
            Autonomous other = flock.mAutonomous[j];
            Vector3 otherPos = other.transform.position; // another cached value
            float dist = (currentPos - otherPos).magnitude; // cached value

            if (i != j) // only checks the if statement once instead of multiple times
            {
                if (dist < visibility)
                {
                    speed += other.Speed;
                    flockDir += other.TargetDirection;
                    steerPos += otherPos;
                    ++count; // we use ++count as we dont need the previous value 
                }

                if (dist < separationDistance)
                {
                    Vector3 targetDirection = (
                          currentPos -
                          otherPos).normalized;

                    separationDir += targetDirection;
                    separationSpeed += dist * weightSeparation;
                }
            }
        }

        if (count > 0)
        {
            speed = speed / count;
            flockDir = (flockDir / count).normalized; // normalizes the vector in the same calculation
            //flockDir.Normalize();

            steerPos = steerPos / count;
        }

        /*if (separationCount > 0)
        {
            separationSpeed = separationSpeed / count;
            separationDir = (separationDir / separationSpeed).normalized;
            //separationDir.Normalize();
        }*/ 

        curr.TargetDirection =
          flockDir * speed * (flock.useAlignmentRule ? flock.weightAlignment : 0.0f) +
          separationDir * separationSpeed * (flock.useSeparationRule ? flock.weightSeparation : 0.0f) +
          (steerPos - curr.transform.position) * (flock.useCohesionRule ? flock.weightCohesion : 0.0f);
    }


    IEnumerator Coroutine_Flocking()
    {
        WaitForSeconds delay = new WaitForSeconds(TickDuration); // we cache the WaitForSeconds call so a new one isnt reated everytime we call it
        while (useFlocking)
        {

            foreach (Flock flock in flocks)
            {
                List<Autonomous> autonomousList = flock.mAutonomous;
                int autonomousCount = autonomousList.Count;
                for (int i = 0; i < autonomousCount; ++i)
                {
                    Execute(flock, i);
                    if (i % BatchSize == 0)
                    {
                        yield return null;
                    }
                }
                yield return null;
            }
            yield return delay;
        }
    }


    void SeparationWithEnemies_Internal(
      List<Autonomous> boids,
      List<Autonomous> enemies,
      float sepDist,
      float sepWeight)
    {
        for (int i = 0; i < boids.Count; ++i)
        {
            for (int j = 0; j < enemies.Count; ++j)
            {
                float dist = (
                  enemies[j].transform.position -
                  boids[i].transform.position).magnitude;
                if (dist < sepDist)
                {
                    Vector3 targetDirection = (
                      boids[i].transform.position -
                      enemies[j].transform.position).normalized;

                    boids[i].TargetDirection += targetDirection;
                    boids[i].TargetDirection.Normalize();

                    boids[i].TargetSpeed += dist * sepWeight;
                    boids[i].TargetSpeed /= 2.0f;
                }
            }
        }
    }

    IEnumerator Coroutine_SeparationWithEnemies()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (!flock.useFleeOnSightEnemyRule || flock.isPredator) continue;

                foreach (Flock enemies in flocks)
                {
                    if (!enemies.isPredator) continue;

                    SeparationWithEnemies_Internal(
                      flock.mAutonomous,
                      enemies.mAutonomous,
                      flock.enemySeparationDistance,
                      flock.weightFleeOnSightEnemy);
                }
                //yield return null;
            }
            yield return null;
        }
    }

    IEnumerator Coroutine_AvoidObstacles()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (!flock.useAvoidObstaclesRule)
                {
                    break;
                }
                List<Autonomous> autonomousList = flock.mAutonomous;
                foreach (Autonomous autonomous in autonomousList)
                {
                    foreach (Obstacle obstacle in mObstacles)
                    {
                        Vector3 ObstaclePos = obstacle.transform.position;
                        Vector3 AutonomousPos = autonomous.transform.position;
                        Vector3 distVec = ObstaclePos - AutonomousPos;
                        float dist = distVec.magnitude;

                        if (dist < obstacle.AvoidanceRadius)
                        {
                            Vector3 targetDirection = (ObstaclePos - AutonomousPos).normalized;

                            autonomous.TargetDirection += targetDirection * flock.weightAvoidObstacles;
                            autonomous.TargetDirection.Normalize();
                        }
                    }
                }
                //yield return null;
            }
            yield return null;
        }
    }
    IEnumerator Coroutine_Random_Motion_Obstacles()
    {
        while (true)
        {
            for (int i = 0; i < Obstacles.Length; ++i)
            {
                Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
                float rand = Random.Range(0.0f, 1.0f);
                autono.TargetDirection.Normalize();
                float angle = Mathf.Atan2(autono.TargetDirection.y, autono.TargetDirection.x);

                if (rand > 0.5f)
                {
                    angle += Mathf.Deg2Rad * 45.0f;
                }
                else
                {
                    angle -= Mathf.Deg2Rad * 45.0f;
                }
                Vector3 dir = Vector3.zero;
                dir.x = Mathf.Cos(angle);
                dir.y = Mathf.Sin(angle);

                autono.TargetDirection += dir * 0.1f;
                autono.TargetDirection.Normalize();
                //Debug.Log(autonomousList[i].TargetDirection);

                float speed = Random.Range(1.0f, autono.MaxSpeed);
                autono.TargetSpeed += speed;
                autono.TargetSpeed /= 2.0f;
            }
            yield return new WaitForSeconds(2.0f);
        }
    }
    IEnumerator Coroutine_Random()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (flock.useRandomRule)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;
                    for (int i = 0; i < autonomousList.Count; ++i)
                    {
                        float rand = Random.Range(0.0f, 1.0f);
                        autonomousList[i].TargetDirection.Normalize();
                        float angle = Mathf.Atan2(autonomousList[i].TargetDirection.y, autonomousList[i].TargetDirection.x);

                        if (rand > 0.5f)
                        {
                            angle += Mathf.Deg2Rad * 45.0f;
                        }
                        else
                        {
                            angle -= Mathf.Deg2Rad * 45.0f;
                        }
                        Vector3 dir = Vector3.zero;
                        dir.x = Mathf.Cos(angle);
                        dir.y = Mathf.Sin(angle);

                        autonomousList[i].TargetDirection += dir * flock.weightRandom;
                        autonomousList[i].TargetDirection.Normalize();
                        //Debug.Log(autonomousList[i].TargetDirection);

                        float speed = Random.Range(1.0f, autonomousList[i].MaxSpeed);
                        autonomousList[i].TargetSpeed += speed * flock.weightSeparation;
                        autonomousList[i].TargetSpeed /= 2.0f;
                    }
                }
                //yield return null;
            }
            yield return new WaitForSeconds(TickDurationRandom);
        }
    }
    void Rule_CrossBorder_Obstacles()
    {
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
            Vector3 pos = autono.transform.position;
            if (autono.transform.position.x > Bounds.bounds.max.x)
            {
                pos.x = Bounds.bounds.min.x;
            }
            if (autono.transform.position.x < Bounds.bounds.min.x)
            {
                pos.x = Bounds.bounds.max.x;
            }
            if (autono.transform.position.y > Bounds.bounds.max.y)
            {
                pos.y = Bounds.bounds.min.y;
            }
            if (autono.transform.position.y < Bounds.bounds.min.y)
            {
                pos.y = Bounds.bounds.max.y;
            }
            autono.transform.position = pos;
        }

        //for (int i = 0; i < Obstacles.Length; ++i)
        //{
        //  Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
        //  Vector3 pos = autono.transform.position;
        //  if (autono.transform.position.x + 5.0f > Bounds.bounds.max.x)
        //  {
        //    autono.TargetDirection.x = -1.0f;
        //  }
        //  if (autono.transform.position.x - 5.0f < Bounds.bounds.min.x)
        //  {
        //    autono.TargetDirection.x = 1.0f;
        //  }
        //  if (autono.transform.position.y + 5.0f > Bounds.bounds.max.y)
        //  {
        //    autono.TargetDirection.y = -1.0f;
        //  }
        //  if (autono.transform.position.y - 5.0f < Bounds.bounds.min.y)
        //  {
        //    autono.TargetDirection.y = 1.0f;
        //  }
        //  autono.TargetDirection.Normalize();
        //}
    }

    void Rule_CrossBorder()
    {
        foreach (Flock flock in flocks)
        {
            List<Autonomous> autonomousList = flock.mAutonomous;
            if (flock.bounceWall)
            {
                for (int i = 0; i < autonomousList.Count; ++i)
                {
                    Vector3 pos = autonomousList[i].transform.position;
                    if (autonomousList[i].transform.position.x + 5.0f > Bounds.bounds.max.x)
                    {
                        autonomousList[i].TargetDirection.x = -1.0f;
                    }
                    if (autonomousList[i].transform.position.x - 5.0f < Bounds.bounds.min.x)
                    {
                        autonomousList[i].TargetDirection.x = 1.0f;
                    }
                    if (autonomousList[i].transform.position.y + 5.0f > Bounds.bounds.max.y)
                    {
                        autonomousList[i].TargetDirection.y = -1.0f;
                    }
                    if (autonomousList[i].transform.position.y - 5.0f < Bounds.bounds.min.y)
                    {
                        autonomousList[i].TargetDirection.y = 1.0f;
                    }
                    autonomousList[i].TargetDirection.Normalize();
                }
            }
            else
            {
                for (int i = 0; i < autonomousList.Count; ++i)
                {
                    Vector3 pos = autonomousList[i].transform.position;
                    if (autonomousList[i].transform.position.x > Bounds.bounds.max.x)
                    {
                        pos.x = Bounds.bounds.min.x;
                    }
                    if (autonomousList[i].transform.position.x < Bounds.bounds.min.x)
                    {
                        pos.x = Bounds.bounds.max.x;
                    }
                    if (autonomousList[i].transform.position.y > Bounds.bounds.max.y)
                    {
                        pos.y = Bounds.bounds.min.y;
                    }
                    if (autonomousList[i].transform.position.y < Bounds.bounds.min.y)
                    {
                        pos.y = Bounds.bounds.max.y;
                    }
                    autonomousList[i].transform.position = pos;
                }
            }
        }
    }
}
