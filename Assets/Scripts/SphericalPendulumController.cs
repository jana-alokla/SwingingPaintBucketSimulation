using UnityEngine;

[ExecuteAlways]
public class SphericalPendulumController : MonoBehaviour
{
    [Header("Mass Settings")]
    [Tooltip("Mass of the empty bucket in kilograms")]
    public float bucketEmptyMass = 2.0f;

    [Tooltip("Mass of the paint inside the bucket in kilograms")]
    public float paintMass = 1.0f;

    [Tooltip("Paint mass loss rate in kg/s. Temporary until it is connected to the paint system")]
    public float paintMassFlowRate = 0.02f;

    [Tooltip("Enable paint mass loss over time")]
    public bool simulatePaintLoss = true;

    [Header("Pendulum Physical Settings")]
    public float ropeLength = 4.0f;
    public float gravity = 9.81f;

    [Header("Initial State - Radians")]
    public float thetaDot = 0.0f;

    [Tooltip("0 means released from rest. Use 0.1 - 0.25 for a slight 3D side push.")]
    public float phiDot = 0.0f;

    [Tooltip("Initial displacement angle in radians. Example: 0.55 rad ≈ 31.5 degrees.")]
    public float theta = 0.55f;

    public float phi = 0.0f;

    [Header("Non-Ideal Damping")]
    [Tooltip("Base damping. Use a small value such as 0.003 to 0.01")]
    public float damping = 0.005f;

    [Tooltip("Air resistance. Its effect becomes more noticeable when the mass decreases")]
    public float airResistanceCoefficient = 0.02f;

    [Tooltip("Pivot friction. Its effect depends on the mass and rope length")]
    public float pivotFrictionCoefficient = 0.03f;

    [Header("Paint Sloshing Inside Bucket")]
    [Tooltip("Enable the effect of paint sloshing inside the bucket")]
    public bool enablePaintSloshing = true;

    [Tooltip("Strength of the paint sloshing effect on the bucket motion")]
    public float sloshingStrength = 0.06f;

    [Tooltip("Internal stiffness that pulls the paint back after sloshing")]
    public float sloshingStiffness = 3.0f;

    [Tooltip("Damping of the internal paint sloshing motion")]
    public float sloshingDamping = 0.8f;

    [Tooltip("How much the internal paint is affected by the bucket motion speed")]
    public float sloshingMotionCoupling = 0.35f;

    [Header("Simulation Settings")]
    public float deltaT = 0.02f;

    [Header("References")]
    public Transform pivotPoint;
    public Transform ropeAttachPoint;

    [Header("Bucket Tilt")]
    public bool tiltBucketWithRope = true;

    [Range(0f, 1f)]
    public float tiltAmount = 1.0f;

    [Header("Debug - Motion")]
    public Vector3 attachPointVelocity;

    [Header("Debug - Physical Outputs")]
    public float totalMass;
    public float effectiveDamping;
    public float ropeTension;
    public float kineticEnergy;
    public float potentialEnergy;

    [Header("Debug - Sloshing")]
    public float sloshThetaOffset;
    public float sloshPhiOffset;

    private Vector4 state;
    private Vector3 origin;
    private Vector3 attachLocalOffset;
    private Vector3 previousAttachPosition;
    private float time;
    private bool initialized;

    private float initialPaintMass;
    private float sloshThetaVelocity;
    private float sloshPhiVelocity;

    private void Start()
    {
        initialPaintMass = paintMass;
        InitializeSimulation();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            PreviewInitialPoseInEditor();
        }
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!initialized)
        {
            InitializeSimulation();
        }

        float dt = deltaT;

        UpdateMass(dt);
        UpdateEffectiveDamping();
        UpdatePaintSloshing(dt);

        state = state + RK4Step(state, time, dt);
        time += dt;

        Vector3 attachPosition = GetPendulumPosition(state.z, state.w);

        attachPointVelocity = (attachPosition - previousAttachPosition) / dt;
        previousAttachPosition = attachPosition;

        UpdatePhysicalOutputs(state.z);

        MoveBucketByAttachPoint(attachPosition);
    }

    private void InitializeSimulation()
    {
        if (pivotPoint == null || ropeAttachPoint == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("SphericalPendulumController needs PivotPoint and RopeAttachPoint.");
                enabled = false;
            }

            return;
        }

        origin = pivotPoint.position;

        attachLocalOffset = transform.InverseTransformPoint(ropeAttachPoint.position);

        state = new Vector4(thetaDot, phiDot, theta, phi);

        totalMass = bucketEmptyMass + paintMass;
        totalMass = Mathf.Max(0.001f, totalMass);

        UpdateEffectiveDamping();

        sloshThetaOffset = 0f;
        sloshPhiOffset = 0f;
        sloshThetaVelocity = 0f;
        sloshPhiVelocity = 0f;

        Vector3 attachPosition = GetPendulumPosition(state.z, state.w);
        MoveBucketByAttachPoint(attachPosition);

        previousAttachPosition = attachPosition;
        attachPointVelocity = Vector3.zero;

        UpdatePhysicalOutputs(state.z);

        time = 0f;
        initialized = true;
    }

    private void PreviewInitialPoseInEditor()
    {
        if (pivotPoint == null || ropeAttachPoint == null)
        {
            return;
        }

        origin = pivotPoint.position;
        attachLocalOffset = transform.InverseTransformPoint(ropeAttachPoint.position);

        Vector3 initialAttachPosition = GetPendulumPosition(theta, phi);
        MoveBucketByAttachPoint(initialAttachPosition);
    }

    private void UpdateMass(float dt)
    {
        if (simulatePaintLoss)
        {
            paintMass -= paintMassFlowRate * dt;
            paintMass = Mathf.Max(0f, paintMass);
        }

        totalMass = bucketEmptyMass + paintMass;
        totalMass = Mathf.Max(0.001f, totalMass);
    }

    private void UpdateEffectiveDamping()
    {
        /*
         * في النواس المثالي لا تؤثر الكتلة على الحركة.
         * هنا نجعل النظام غير مثالي:
         * مقاومة الهواء واحتكاك نقطة التعليق يصبح تأثيرهما أكبر عندما تقل الكتلة.
         */

        float safeLength = Mathf.Max(0.001f, ropeLength);

        float airDamping = airResistanceCoefficient / totalMass;

        float pivotDamping =
            pivotFrictionCoefficient / (totalMass * safeLength * safeLength);

        effectiveDamping = damping + airDamping + pivotDamping;
    }

    private void UpdatePaintSloshing(float dt)
    {
        if (!enablePaintSloshing || paintMass <= 0.001f)
        {
            sloshThetaOffset = 0f;
            sloshPhiOffset = 0f;
            sloshThetaVelocity = 0f;
            sloshPhiVelocity = 0f;
            return;
        }

        /*
         * تمثيل مبسط لتمايل الطلاء داخل الدلو.
         * عندما يتحرك الدلو بسرعة زاوية، الطلاء الداخلي يتأخر عنه بسبب القصور الذاتي.
         * هذا التأخر يولد تأثيرًا صغيرًا على التسارع الزاوي للدلو.
         */

        float paintRatio = paintMass / totalMass;

        float thetaDriver = -state.x * sloshingMotionCoupling * paintRatio;
        float phiDriver = -state.y * sloshingMotionCoupling * paintRatio;

        float thetaAcceleration =
            thetaDriver
            - sloshingStiffness * sloshThetaOffset
            - sloshingDamping * sloshThetaVelocity;

        float phiAcceleration =
            phiDriver
            - sloshingStiffness * sloshPhiOffset
            - sloshingDamping * sloshPhiVelocity;

        sloshThetaVelocity += thetaAcceleration * dt;
        sloshPhiVelocity += phiAcceleration * dt;

        sloshThetaOffset += sloshThetaVelocity * dt;
        sloshPhiOffset += sloshPhiVelocity * dt;

        sloshThetaOffset = Mathf.Clamp(sloshThetaOffset, -0.25f, 0.25f);
        sloshPhiOffset = Mathf.Clamp(sloshPhiOffset, -0.25f, 0.25f);
    }

    private Vector4 G(Vector4 currentState, float currentTime)
    {
        float th_d = currentState.x;
        float ph_d = currentState.y;
        float th = currentState.z;

        float tanTheta = Mathf.Tan(th);

        if (Mathf.Abs(tanTheta) < 0.001f)
        {
            tanTheta = tanTheta >= 0f ? 0.001f : -0.001f;
        }

        /*
         * معادلات النواس الكروي:
         *
         * theta'' = phi'^2 cos(theta) sin(theta) - (g / L) sin(theta)
         * phi''   = -2 theta' phi' / tan(theta)
         *
         * ثم نضيف:
         * 1) effectiveDamping الذي يتأثر بالكتلة ومقاومة الهواء والاحتكاك
         * 2) تأثير تمايل الطلاء الداخلي
         */

        float paintRatio = totalMass > 0.001f ? paintMass / totalMass : 0f;

        float sloshThetaEffect = enablePaintSloshing
            ? sloshingStrength * paintRatio * sloshThetaOffset
            : 0f;

        float sloshPhiEffect = enablePaintSloshing
            ? sloshingStrength * paintRatio * sloshPhiOffset
            : 0f;

        float thetaDDot =
            (ph_d * ph_d) * Mathf.Cos(th) * Mathf.Sin(th)
            - (gravity / ropeLength) * Mathf.Sin(th)
            - effectiveDamping * th_d
            + sloshThetaEffect;

        float phiDDot =
            -2.0f * th_d * ph_d / tanTheta
            - effectiveDamping * ph_d
            + sloshPhiEffect;

        return new Vector4(thetaDDot, phiDDot, th_d, ph_d);
    }

    private Vector4 RK4Step(Vector4 currentState, float currentTime, float dt)
    {
        Vector4 k1 = G(currentState, currentTime);
        Vector4 k2 = G(currentState + 0.5f * dt * k1, currentTime + 0.5f * dt);
        Vector4 k3 = G(currentState + 0.5f * dt * k2, currentTime + 0.5f * dt);
        Vector4 k4 = G(currentState + dt * k3, currentTime + dt);

        return (dt / 6.0f) * (k1 + 2.0f * k2 + 2.0f * k3 + k4);
    }

    private Vector3 GetPendulumPosition(float th, float ph)
    {
        float x = ropeLength * Mathf.Sin(th) * Mathf.Cos(ph);
        float y = -ropeLength * Mathf.Cos(th);
        float z = ropeLength * Mathf.Sin(th) * Mathf.Sin(ph);

        return origin + new Vector3(x, y, z);
    }

    private void UpdatePhysicalOutputs(float currentTheta)
    {
        float safeLength = Mathf.Max(0.001f, ropeLength);
        float speed = attachPointVelocity.magnitude;

        /*
         * قوة الشد:
         * T = m g cos(theta) + m v² / L
         */
        ropeTension =
            totalMass * gravity * Mathf.Cos(currentTheta)
            + totalMass * speed * speed / safeLength;

        /*
         * ارتفاع الدلو عن أدنى نقطة:
         * h = L(1 - cos(theta))
         */
        float height = safeLength * (1f - Mathf.Cos(currentTheta));

        /*
         * Ep = m g h
         */
        potentialEnergy = totalMass * gravity * height;

        /*
         * Ek = 1/2 m v²
         */
        kineticEnergy = 0.5f * totalMass * speed * speed;
    }

    private void MoveBucketByAttachPoint(Vector3 targetAttachPosition)
    {
        Vector3 ropeDirection = (pivotPoint.position - targetAttachPosition).normalized;

        Quaternion ropeRotation = Quaternion.FromToRotation(Vector3.up, ropeDirection);

        Quaternion targetRotation = tiltBucketWithRope
            ? Quaternion.Slerp(Quaternion.identity, ropeRotation, tiltAmount)
            : Quaternion.identity;

        Vector3 rotatedAttachOffset = targetRotation * attachLocalOffset;

        transform.position = targetAttachPosition - rotatedAttachOffset;
        transform.rotation = targetRotation;
    }

    public void ResetPendulum()
    {
        /*
         * عند Reset نرجع كمية الطلاء للقيمة الابتدائية حتى تبدأ التجربة من جديد.
         */
        paintMass = initialPaintMass;

        initialized = false;
        InitializeSimulation();
    }
}