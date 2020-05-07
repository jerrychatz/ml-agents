using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

public class SmallCubeAgent : Agent
{
    CubeWarSettings m_CubeWarSettings;
    public GameObject area;
    CubeWarArea m_MyArea;
    public GameObject largeAgent;
    LargeCubeAgent m_LargeAgent;
    bool m_Dead;
    bool m_Shoot;
    float m_ShootTime;
    Rigidbody m_AgentRb;
    [HideInInspector]
    public float HitPoints;
    [HideInInspector]
    public SmallCubeAgent Teammate1;
    [HideInInspector]
    public SmallCubeAgent Teammate2;
    [HideInInspector]
    public Rigidbody TeammateRb1;
    [HideInInspector]
    public Rigidbody TeammateRb2;

    // Speed of agent rotation.
    public float turnSpeed;
    float m_Bonus;

    // Speed of agent movement.
    public float moveSpeed;
    public Material normalMaterial;
    public Material weakMaterial;
    public Material deadMaterial;
    public Laser myLaser;
    public GameObject myBody;

    private EnvironmentParameters m_ResetParams;

    public override void Initialize()
    {
        m_AgentRb = GetComponent<Rigidbody>();
        m_MyArea = area.GetComponent<CubeWarArea>();
        m_LargeAgent = largeAgent.GetComponent<LargeCubeAgent>();
        m_CubeWarSettings = FindObjectOfType<CubeWarSettings>();
        m_ResetParams = Academy.Instance.EnvironmentParameters; 
        SetResetParameters();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(System.Convert.ToInt32(m_Shoot));
        sensor.AddObservation(System.Convert.ToInt32(m_Dead));
        sensor.AddObservation(HitPoints);
        // Direction big agent is looking
        Vector3 dirToSelf = transform.position - m_LargeAgent.transform.position;
        float angle = Vector3.Dot(m_LargeAgent.transform.forward.normalized, dirToSelf.normalized);
        sensor.AddObservation(angle);

        //Teammate 1 direction, normalized distance, hitpoints
        Vector3 dirToT1 = Teammate1.transform.position - transform.position;
        sensor.AddObservation(dirToT1.normalized);
        sensor.AddObservation(Vector3.Distance(Teammate1.transform.position, transform.position) / 300f); //roughly normalized ditance
        sensor.AddObservation(Teammate1.HitPoints);

        Vector3 dirToT2 = Teammate2.transform.position - transform.position;
        sensor.AddObservation(dirToT2.normalized);
        sensor.AddObservation(Vector3.Distance(Teammate2.transform.position, transform.position) / 300f); //roughly normalized ditance
        sensor.AddObservation(Teammate2.HitPoints);

        if (m_Dead)
        {
            AddReward(-.001f * m_Bonus);
        }
    }

    public Color32 ToColor(int hexVal)
    {
        var r = (byte)((hexVal >> 16) & 0xFF);
        var g = (byte)((hexVal >> 8) & 0xFF);
        var b = (byte)(hexVal & 0xFF);
        return new Color32(r, g, b, 255);
    }

    public void MoveAgent(float[] act)
    {
        m_Shoot = false;

        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        if (!m_Dead)
        {
            var shootCommand = false;
            var forwardAxis = (int)act[0];
            var rightAxis = (int)act[1];
            var rotateAxis = (int)act[2];
            var shootAxis = (int)act[3];

            switch (forwardAxis)
            {
                case 1:
                    dirToGo = transform.forward;
                    break;
                case 2:
                    dirToGo = -transform.forward;
                    break;
            }

            switch (rightAxis)
            {
                case 1:
                    dirToGo = transform.right;
                    break;
                case 2:
                    dirToGo = -transform.right;
                    break;
            }

            switch (rotateAxis)
            {
                case 1:
                    rotateDir = -transform.up;
                    break;
                case 2:
                    rotateDir = transform.up;
                    break;
            }
            switch (shootAxis)
            {
                case 1:
                    shootCommand = true;
                    break;
            }
            if (shootCommand)
            {
                if (Time.time > m_ShootTime + .4f)
                {
                    m_Shoot = true;
                    dirToGo *= 0.5f;
                    m_AgentRb.velocity *= 0.9f;
                    m_ShootTime = Time.time;
                }
            }
            transform.Rotate(rotateDir, Time.fixedDeltaTime * turnSpeed);
            m_AgentRb.AddForce(dirToGo * moveSpeed, ForceMode.VelocityChange);
        }

        //if (m_AgentRb.velocity.sqrMagnitude > 25f) // slow it down
        //{
        //    m_AgentRb.velocity *= 0.95f;
        //}

        if (m_Shoot)
        {
            var myTransform = transform;
            var rayDir = 25.0f * myTransform.forward;
            Debug.DrawRay(myTransform.position, rayDir, Color.red, 0f, true);
            RaycastHit hit;
            if (Physics.SphereCast(transform.position, 2f, rayDir, out hit, 28f))
            {
                if (hit.collider.gameObject.CompareTag("StrongSmallAgent") || hit.collider.gameObject.CompareTag("WeakSmallAgent") || hit.collider.gameObject.CompareTag("DeadSmallAgent"))
                {
                    hit.collider.gameObject.GetComponent<SmallCubeAgent>().HealAgent();
                }
                else if (hit.collider.gameObject.CompareTag("StrongLargeAgent") || hit.collider.gameObject.CompareTag("WeakLargeAgent"))
                {
                    hit.collider.gameObject.GetComponent<LargeCubeAgent>().HitAgent(.01f);

                    AddReward(.02f + .5f * m_Bonus);
                }
                myLaser.isFired = true;
            }
        }
        else if (Time.time > m_ShootTime + .25f)
        {
            myLaser.isFired = false;
        }
    }

    public void HitAgent(float damage)
    {
        if (!m_Dead)
        {
            HitPoints -= damage;
            HealthStatus();
        }
    }

    public void HealAgent()
    {
        if (HitPoints < 1f)
        {
            HitPoints = Mathf.Min(HitPoints + .25f, 1f);
            HealthStatus();
        }
    }

    void HealthStatus()
    {
        if (HitPoints <= 1f && HitPoints > .5f)
        {
            m_Dead = false;
            gameObject.tag = "StrongSmallAgent";
            myBody.GetComponentInChildren<Renderer>().material = normalMaterial;
        }

        else if (HitPoints <= .5f && HitPoints > 0.0f)
        {
            m_Dead = false;
            gameObject.tag = "WeakSmallAgent";
            myBody.GetComponentInChildren<Renderer>().material = weakMaterial;

        }
        else // Dead
        {
            AddReward(-.1f * m_Bonus);
            m_Dead = true;
            gameObject.tag = "DeadSmallAgent";
            myBody.GetComponentInChildren<Renderer>().material = deadMaterial;
            m_MyArea.AgentDied();
        }
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        MoveAgent(vectorAction);
    }

    public override void Heuristic(float[] actionsOut)
    {
        if (Input.GetKey(KeyCode.D))
        {
            actionsOut[2] = 2f;
        }
        if (Input.GetKey(KeyCode.W))
        {
            actionsOut[0] = 1f;
        }
        if (Input.GetKey(KeyCode.E))
        {
            actionsOut[1] = 1f;
        }
        if (Input.GetKey(KeyCode.Q))
        {
            actionsOut[1] = 2f;
        }
        if (Input.GetKey(KeyCode.A))
        {
            actionsOut[2] = 1f;
        }
        if (Input.GetKey(KeyCode.S))
        {
            actionsOut[0] = 2f;
        }
        actionsOut[3] = Input.GetKey(KeyCode.Space) ? 1.0f : 0.0f;
    }

    public override void OnEpisodeBegin()
    {
        HitPoints = 1f;
        HealthStatus();
        m_Dead = false;
        m_Shoot = false;
        m_ShootTime = -.5f;
        //m_Bonus = Academy.Instance.FloatProperties.GetPropertyWithDefault("bonus", 0);
        m_Bonus = m_ResetParams.GetWithDefault("bonus", 0);
        m_AgentRb.velocity = Vector3.zero;


        float smallRange = 50f * m_MyArea.range;
        transform.position = new Vector3(Random.Range(-smallRange, smallRange),
            2f,Random.Range(-smallRange, smallRange))
            + area.transform.position;
        transform.rotation = Quaternion.Euler(new Vector3(0f, Random.Range(0, 360)));

        SetResetParameters();
    }

    public bool IsDead()
    {
        return m_Dead;
    }

    public void SetAgentScale()
    {
        float agentScale = 1f;
        gameObject.transform.localScale = new Vector3(agentScale, agentScale, agentScale);
    }

    public void SetResetParameters()
    {
        SetAgentScale();
    }
}
